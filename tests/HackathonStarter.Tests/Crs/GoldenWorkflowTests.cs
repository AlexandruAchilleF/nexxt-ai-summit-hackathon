using System.Globalization;
using System.Security.Cryptography;
using HackathonStarter.Config;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Xunit.Abstractions;
using static System.FormattableString;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// The golden workflow cards end to end (docs/PLAN.md 16.2, "Etalon, flux": cases 4, 16, 17a, 17b, 18; the cards are in
/// docs/GOLDEN-CASES.md 4). The card's change goes through IChangeService, then the case through ICaseService to where the
/// card ends it: closed with proof, reminded and escalated, or expired. R2's test host: temp SQLite, the simulated clock at
/// the card's moment, the demo operator, the real shield and evidence verifier over PDFs generated here. Each card runs
/// twice: with an oracle engine that answers what the card expects (R2 alone) and with the CrsRuleEngine registered by
/// AddRules (R1 + R2), which waits only while every call the run makes returns the phase 0 placeholder. Expected values
/// come from the card, the end states of PLAN 16.1 and the R2 decisions (docs/roluri/R2-spec-faza1.md), never from the
/// code under test.
/// </summary>
public class GoldenWorkflowTests(ITestOutputHelper output)
{
    private const string PlaceholderExplanation = "Rule engine not implemented yet (phase 0 placeholder).";

    /// <summary>The expiry rule of each deadline rule (RuleCatalog: DL-EXPIRED-NEW p. 35-36, DL-EXPIRED-PRE p. 29).</summary>
    private static readonly Dictionary<string, string> ExpiryOf = new() { ["DL-NEW-90"] = "DL-EXPIRED-NEW", ["DL-PRE-YEAR"] = "DL-EXPIRED-PRE" };

    /// <summary>
    /// Where each workflow card ends (PLAN 16.1), pinned by id so a reworded card cannot change what its run checks. An
    /// Expired end names its expiry rule; a close cites the rule of <see cref="ClosingRule"/>.
    /// </summary>
    private static readonly Dictionary<string, (CaseStatus Status, string? ExpiryRule)> EndStates = new()
    {
        ["4"] = (CaseStatus.Closed, null),
        ["16"] = (CaseStatus.Escalated, null),
        ["17a"] = (CaseStatus.Expired, "DL-EXPIRED-NEW"),
        ["17b"] = (CaseStatus.Expired, "DL-EXPIRED-PRE"),
        ["18"] = (CaseStatus.Closed, null),
    };

    /// <summary>SiteConfig.Users[0], the operator: records, drafts, approves and closes in every run.</summary>
    private static DemoUser Operator => SiteConfig.Users[0];

    /// <summary>
    /// The workflow cards: the layer says so, or R2 owns the card (R5 sets the layer when transcribing it). An engine card
    /// with a deadline is not one: its change need not be a workflow input (DocumentUploaded is decided by the shield alone).
    /// </summary>
    private static bool IsWorkflowCard(GoldenCase c) => c.Layer.HasFlag(GoldenLayer.Workflow) || c.Owner == GoldenOwner.R2;

    public static TheoryData<string> WorkflowCases => [.. GoldenCases.All.Where(c => IsWorkflowCard(c) && c.HasInput).Select(c => c.Id)];

    [Fact]
    public void Workflow_cases_waiting_for_input()
    {
        var cards = GoldenCases.All.Where(IsWorkflowCard).ToList();
        // PLAN 16.1 and 16.2 name them, with their end states: a selection that lost one would leave it untested without any failure.
        Assert.Superset(EndStates.Keys.ToHashSet(), cards.Select(c => c.Id).ToHashSet());

        var waiting = cards.Where(c => !c.HasInput).Select(c => $"{c.Id} (no {string.Join(", ", Missing(c))})").ToList();
        output.WriteLine($"The theories run: {string.Join(", ", cards.Where(c => c.HasInput).Select(c => c.Id))}.");
        output.WriteLine(waiting.Count == 0 ? "No workflow card waits for input." : $"Waiting for input (R5): {string.Join("; ", waiting)}.");
    }

    [Theory]
    [MemberData(nameof(WorkflowCases))]
    public async Task Golden_workflow_case_with_the_oracle_engine(string id)
    {
        var card = GoldenCases.Get(id);
        var oracle = new CardEngine(card);
        await using var host = await HostAsync(s => s.AddSingleton<ICrsRuleEngine>(oracle));
        Assert.Same(oracle, host.Root.GetRequiredService<ICrsRuleEngine>());

        await new CardRun(card, host, oracle, output).RunAsync();
    }

    [Theory]
    [MemberData(nameof(WorkflowCases))]
    public async Task Golden_workflow_case_with_the_real_engine(string id)
    {
        await RealEngineRunAsync(GoldenCases.Get(id), s => s.AddRules()); // Program.cs's registration, after the host's scripted engine
    }

    [Fact]
    public async Task Golden_case_12_is_recorded_through_the_change_service_with_the_cards_after_and_outcome()
    {
        // The c/o address becomes the only current one (p. 29 f, 31; not a residence, p. 48): the change ends the Cluj
        // residence, as the card's After, so /changes/new can record the card as is.
        var card = GoldenCases.Get("12");
        var (host, _) = await Sentinel.RealWorld.CreateAsync();
        await using var _ = host;

        var result = await Sentinel.RealWorld.RecordGoldenChangeAsync(host, card.Id);

        var saved = ClientSnapshot.From((await host.Get<IClientService>().GetAsync(card.ClientCode))!);
        Assert.Equal(card.After!.CurrentAddresses.Select(a => (a.Kind, a.Line, a.City, a.Country)),
            saved.CurrentAddresses.Select(a => (a.Kind, a.Line, a.City, a.Country)));
        Assert.Equal(card.ExpectedDecision, result.Outcome.Decision);
        Assert.Equal(card.ExpectedRegime, result.Outcome.Regime);
        Assert.Equal(card.ExpectedHits.Order(), Sentinel.Hits.Of(result.Outcome.Indicia).Order());
        foreach (var cleared in card.ExpectedCleared)
            Assert.Contains(result.Outcome.Cleared, h => h.RuleId == cleared);

        var item = (await host.Get<ICaseService>().GetAsync(result.CaseCode!))!;
        Assert.Equal(card.ExpectedHits.Order(), Sentinel.Hits.Of(item.Indicia).Order());
        var deadline = card.ExpectedDeadline!;
        Assert.Equal((card.ExpectedDeadlineRule, deadline.Preexisting, deadline.DueAt, deadline.RemindAt),
            (item.DeadlineRuleId, item.IsPreexisting, item.DueAt, item.RemindAt));
    }

    [Fact]
    public async Task The_real_engine_run_waits_only_while_every_call_it_makes_is_the_placeholder()
    {
        var card = GoldenCases.Get("4"); // its run calls Evaluate, EvaluateSelfCertification and EvaluateCurrent

        Assert.False(await RealEngineRunAsync(card, s => s.AddSingleton<ICrsRuleEngine>(new HalfEngine(card, realEvaluate: false))));

        // R1 half merged: Evaluate is real, EvaluateCurrent still the placeholder. The run must not wait: it runs the card and
        // fails on it (the re-evaluation before closing gives the placeholder's NoCase, not the card's Compliant).
        var failure = await Record.ExceptionAsync(() => RealEngineRunAsync(card, s => s.AddSingleton<ICrsRuleEngine>(new HalfEngine(card, realEvaluate: true))));
        Assert.NotNull(failure); // a run that waits returns false and throws nothing
        output.WriteLine($"half-merged engine: the card failed with {failure.GetType().Name}: {failure.Message.Split('\n')[0]}");
    }

    /// <summary>
    /// The card with the engine <paramref name="register"/> puts in the container (the theory: AddRules). It waits for R1
    /// (false, no step run) only while every call the run makes returns the phase 0 placeholder, so a half-merged engine
    /// runs the card and fails on it instead of closing a case on a placeholder NoCase.
    /// </summary>
    private async Task<bool> RealEngineRunAsync(GoldenCase card, Action<IServiceCollection> register)
    {
        await using var host = await HostAsync(register);
        var engine = host.Root.GetRequiredService<ICrsRuleEngine>();
        Assert.IsNotType<ScriptedRuleEngine>(engine); // the host's stand-in must not answer for R1
        if (EngineCalls(card, engine).All(outcome => outcome.Explanation == PlaceholderExplanation))
        {
            output.WriteLine($"case {card.Id}, real engine: waits for R1; {engine.GetType().Name} answers every call with the phase 0 placeholder, no step ran.");
            return false;
        }

        await new CardRun(card, host, null, output).RunAsync();
        return true;
    }

    /// <summary>The calls a run of the card makes, on the card's data: the change, the re-evaluation, each self-certification it submits.</summary>
    private static IEnumerable<RuleOutcome> EngineCalls(GoldenCase card, ICrsRuleEngine engine)
    {
        yield return engine.Evaluate(card.Before!, card.After!, card.Change!.Kind);
        yield return engine.EvaluateCurrent(card.After!);
        foreach (var selfCertification in new[] { card.Change!.SelfCertification, card.Response?.SelfCertification }.OfType<SelfCertificationSnapshot>())
            yield return engine.EvaluateSelfCertification(card.After!, selfCertification);
    }

    /// <summary>R2's host with the given engine and the real shield (ShieldedDocuments over HiddenTextShield) instead of its fake.</summary>
    private static Task<R2WorkflowHost> HostAsync(Action<IServiceCollection> engine) => R2WorkflowHost.CreateAsync(s =>
    {
        engine(s);
        s.AddScoped<IShieldedDocuments, ShieldedDocuments>();
    });

    /// <summary>
    /// The rule the case opens with. The card's deadline column may name the expiry instead (17a: "DL-EXPIRED-NEW", the
    /// deadline being case 16's); then it is the deadline rule that expiry belongs to. Null: the guide gives none (p. 38).
    /// </summary>
    private static string? DeadlineRule(GoldenDeadline deadline)
    {
        foreach (var (rule, expiry) in ExpiryOf)
            if (deadline.RuleId == expiry) return rule;
        return deadline.RuleId;
    }

    /// <summary>
    /// The rule a close cites (the decision of 28.10, after the validated card of case 4): a reasonable explanation
    /// RESP-EXPLANATION (p. 37); a new self-certification SC-VALID (p. 34) on a CRS case, FATCA-SC (p. 38) on a FATCA one.
    /// No other close is decided.
    /// </summary>
    private static string ClosingRule(ResponseKind kind, Regime regime) => (kind, regime) switch
    {
        (ResponseKind.ReasonableExplanation, _) => "RESP-EXPLANATION",
        (ResponseKind.NewSelfCertification, Regime.Crs) => "SC-VALID",
        (ResponseKind.NewSelfCertification, Regime.Fatca) => "FATCA-SC",
        _ => throw new NotSupportedException($"No closing rule is decided for a {kind} on a {regime} case."),
    };

    private static IEnumerable<string> Missing(GoldenCase c)
    {
        if (c.Before is null) yield return nameof(c.Before);
        if (c.After is null) yield return nameof(c.After);
        if (c.Change is null) yield return nameof(c.Change);
        if (c.ReceivedAt is null) yield return nameof(c.ReceivedAt);
    }

    /// <summary>One card on one host: its steps in the order of its timeline, each checked against the card.</summary>
    private sealed class CardRun
    {
        private static readonly string OperatorActor = AuditActors.User(Operator.Id);

        /// <summary>The state changes: each one leaves exactly one entry on the case's audit log.</summary>
        private static readonly string[] StateChanges =
        [
            AuditKinds.CaseOpened, AuditKinds.RequestApproved, AuditKinds.CaseReminded, AuditKinds.CaseEscalated,
            AuditKinds.ResponseRegistered, AuditKinds.CaseClosed, AuditKinds.CaseExpired,
        ];

        private readonly GoldenCase _card;
        private readonly R2WorkflowHost _host;
        private readonly CardEngine? _oracle;
        /// <summary>The engine the services use: the oracle, or the one AddRules registered.</summary>
        private readonly ICrsRuleEngine _engine;
        private readonly ITestOutputHelper _output;
        private readonly ClientSnapshot _before, _after;
        private readonly ChangeRequest _change;
        private readonly DateTime _receivedAt;
        private readonly Session _operator, _viewer;
        /// <summary>The entries the case's audit log must have, in this order (others may sit between them).</summary>
        private readonly List<(string Kind, string Actor)> _trail = [];
        private int _clientId;
        private string _code = "";

        public CardRun(GoldenCase card, R2WorkflowHost host, CardEngine? oracle, ITestOutputHelper output)
        {
            if (!card.HasInput) throw new ArgumentException($"Case {card.Id} has no input yet.", nameof(card));
            (_card, _host, _oracle, _output) = (card, host, oracle, output);
            (_before, _after, _change, _receivedAt) = (card.Before!, card.After!, card.Change!, card.ReceivedAt!.Value);
            _engine = host.Root.GetRequiredService<ICrsRuleEngine>();
            _operator = host.As(Operator);
            _viewer = host.As(R2WorkflowHost.Viewer);
        }

        public async Task RunAsync()
        {
            Assert.Equal(Roles.Operator, Operator.Role);
            Assert.True(EndStates.TryGetValue(_card.Id, out var end), $"case {_card.Id}: no end state from PLAN 16.1 in GoldenWorkflowTests.EndStates");
            var deadline = _card.ExpectedDeadline;
            Assert.NotNull(deadline); // a workflow card opens a case
            _host.SetNow(_receivedAt);
            Log($"{_card.Title}; {_change.Kind} at {Iso(_receivedAt)}");

            await SeedAsync();
            await ChangeAsync(deadline);
            foreach (var document in _card.Documents) await EvidenceAsync(document, (await UploadAsync(document)).Id);
            await RequestAsync();

            var answer = _card.Response;
            var waiting = CaseStatus.AwaitingClient;
            if (deadline.RemindAt is { } remindAt && (answer is null || answer.ReceivedAt >= remindAt))
            {
                await ReminderAsync(remindAt);
                waiting = CaseStatus.Escalated;
            }
            if (answer is not null) await AnswerAndCloseAsync(answer, deadline);
            else await WaitAsync(deadline, waiting, end.ExpiryRule);

            await AuditTrailAsync();

            // The end PLAN 16.1 gives the card, whatever its text says.
            var final = await CaseAsync();
            Assert.Equal(end.Status, final.Status);
            if (end.ExpiryRule is { } expiry) Assert.Equal(expiry, final.OutcomeRuleId);
            Log($"ends {final.Status}{(final.OutcomeRuleId is null ? "" : $" on {final.OutcomeRuleId}")}, as PLAN 16.1 says");
        }

        /// <summary>The card's Before in the database; the helper's own check: it reads back as the card's snapshot.</summary>
        private async Task SeedAsync()
        {
            var client = ClientFromSnapshot(_before);
            await using (var db = await _host.DbAsync())
            {
                db.Clients.Add(client);
                await db.SaveChangesAsync();
            }
            _clientId = client.Id;

            var saved = await _viewer.Clients.SnapshotAsync(_clientId);
            Assert.NotNull(saved);
            Assert.Equal(Canonical(_before), Canonical(saved));
        }

        /// <summary>The card's change: code decides, and the case opens with the card's deadline and uncovered indicia.</summary>
        private async Task ChangeAsync(GoldenDeadline deadline)
        {
            var result = await _operator.Changes.RecordAsync(_change);

            // One assertion, so a failure prints the expected and actual decision with its rules.
            Assert.Equal(Verdict(_card.ExpectedDecision, Keys(_card.ExpectedIndicia, coverage: true)),
                Verdict(result.Outcome.Decision, Keys(result.Outcome.Indicia, coverage: true)));
            foreach (var cleared in _card.ExpectedClearedRules) // a subset (GOLDEN-CASES.md 1)
                Assert.Contains(result.Outcome.Cleared, h => h.RuleId == cleared.RuleId && (cleared.Jurisdiction is null || h.Jurisdiction == cleared.Jurisdiction));
            Assert.Equal(_receivedAt, result.Change.ReceivedAt);
            if (_oracle is not null)
            {
                // What the code handed the engine: the card's Before and After, so the real engine decides on the card's data.
                var (before, after, kind) = Assert.Single(_oracle.Changes);
                Assert.Equal(_change.Kind, kind);
                Assert.Equal(Canonical(_before), Canonical(before));
                Assert.Equal(Canonical(_after), Canonical(after));
            }

            Assert.NotNull(result.CaseCode);
            _code = result.CaseCode;
            var item = await CaseAsync();
            Assert.Equal(CaseStatus.Open, item.Status);
            Assert.Equal((_card.ClientCode, result.Change.Id, _receivedAt), (item.ClientCode, item.TriggerChangeId, item.OpenedAt));
            if (_card.ExpectedRegime is { } regime) Assert.Equal(regime, item.Regime);
            Assert.Equal(deadline.DueAt, item.DueAt);
            Assert.Equal(deadline.RemindAt, item.RemindAt);
            Assert.Equal(deadline.Preexisting, item.IsPreexisting);
            if (DeadlineRule(deadline) is { } rule) Assert.Equal(rule, item.DeadlineRuleId);
            Assert.Equal(Keys(_card.ExpectedIndicia.Where(h => h.CoveredBy == Coverage.None)), Keys(item.Indicia));
            foreach (var indicium in item.Indicia)
            {
                var expected = _card.ExpectedIndicia.First(h => h.RuleId == indicium.RuleId && h.Jurisdiction == indicium.Jurisdiction);
                Assert.Equal(RuleCatalog.Get(indicium.RuleId).Regime, indicium.Regime);
                Assert.False(string.IsNullOrWhiteSpace(indicium.GuidePage), $"case {_card.Id}: {indicium.RuleId} has no guide page");
                Assert.False(string.IsNullOrWhiteSpace(indicium.Explanation) || string.IsNullOrWhiteSpace(indicium.ExplanationRo),
                    $"case {_card.Id}: {indicium.RuleId} lacks its explanation in English or Romanian");
                Assert.Equal(result.Change.Id, indicium.ChangeEventId);
                Assert.Equal(_receivedAt, indicium.AddedAt);
                if (expected.SourceField is { } field) Assert.Equal(field, indicium.SourceField);
            }

            Trail(OperatorActor, AuditKinds.ChangeReceived, AuditKinds.CaseOpened);
            Log($"{result.Outcome.Decision}: opened {_code} ({item.Regime}) on {string.Join(", ", Keys(item.Indicia))}; " +
                $"{item.DeadlineRuleId}, preexisting {item.IsPreexisting}, due {Iso(item.DueAt)}, reminder {Iso(item.RemindAt)}");
        }

        /// <summary>The document as the bank stores it: a generated PDF with the card's quotes.</summary>
        private async Task<(int Id, byte[] Bytes)> UploadAsync(GoldenDocument document)
        {
            var bytes = QuotePdf(document);
            var saved = new ClientDocument
            {
                ClientId = _clientId, Kind = document.Kind, FileName = document.FileName, Content = bytes, Pages = 1, UploadedAt = _host.Clock.UtcNow,
            };
            await using var db = await _host.DbAsync();
            db.ClientDocuments.Add(saved);
            await db.SaveChangesAsync();
            return (saved.Id, bytes);
        }

        /// <summary>Each quote as evidence, verified by code on the visible text; a signal quote also as a context signal. The case does not change.</summary>
        private async Task EvidenceAsync(GoldenDocument document, int documentId)
        {
            var before = State(await CaseAsync());
            foreach (var quote in document.Quotes)
            {
                var evidence = await _operator.Cases.AddEvidenceAsync(new NewEvidence(_card.ClientCode, _code, EvidenceSource.Document, quote.Text,
                    documentId, LinkedRuleId: quote.EvidenceFor));
                Assert.True(evidence.Verified, $"case {_card.Id}: \"{quote.Text}\" is not verified in {document.FileName}: {evidence.VerificationNote}");
                Assert.Equal(1, evidence.Page);
                Assert.Equal(quote.EvidenceFor, evidence.LinkedRuleId);
                Trail(OperatorActor, AuditKinds.EvidenceRecorded);
                if (!quote.IsSignal) continue;

                var signal = await _operator.Cases.AddSignalAsync(new NewSignal(_card.ClientCode, _code, $"Context signal in {document.FileName}", evidence.Id));
                Assert.Equal(evidence.Id, signal.EvidenceId);
                Trail(OperatorActor, AuditKinds.SignalRecorded);
            }

            // A context signal never changes a decision and never opens a case (AGENTS.md); evidence neither.
            Assert.Equal(before, State(await CaseAsync()));
            Assert.Single(await _viewer.Cases.QueueAsync(new CaseQueueFilter(OpenOnly: false, ClientCode: _card.ClientCode)));
            Log($"{document.FileName}: {document.Quotes.Count} quote(s) verified, {document.Quotes.Count(q => q.IsSignal)} signal(s), case unchanged");
        }

        /// <summary>The request to the client, drafted and approved by the operator: the case waits for the client.</summary>
        private async Task RequestAsync()
        {
            var draft = await _operator.Cases.DraftRequestAsync(_code, "ro", "Bună ziua, vă rugăm să ne trimiteți documentele de mai jos.");
            Assert.NotEmpty(draft.RequestedDocuments); // listed by code, from the case

            var sent = await _operator.Cases.ApproveRequestAsync(_code, draft.Id, "email");

            Assert.Equal(RequestStatus.Sent, sent.Status);
            Assert.Equal(Operator.Name, sent.ApprovedBy); // the user's display name
            Assert.Equal(_receivedAt, sent.ApprovedAt);
            Assert.Equal(CaseStatus.AwaitingClient, (await CaseAsync()).Status);
            Trail(OperatorActor, AuditKinds.RequestDrafted, AuditKinds.RequestApproved);
            Log($"request approved by {sent.ApprovedBy} via {sent.Channel}: {string.Join("; ", sent.RequestedDocuments)}");
        }

        /// <summary>The reminder day with no answer (PLAN 9.4): Reminded, then Escalated in the same tick, with one draft; only once.</summary>
        private async Task ReminderAsync(DateTime remindAt)
        {
            await TickAsync(remindAt.AddSeconds(-1), new DeadlineTick(0, 0, 0));
            await TickAsync(remindAt, new DeadlineTick(1, 1, 0));

            var item = await CaseAsync();
            Assert.Equal(CaseStatus.Escalated, item.Status);
            Assert.Equal(remindAt, item.RemindedAt);
            Assert.Equal(remindAt, item.EscalatedAt);
            var reminder = Assert.Single(item.Requests, r => r.Kind == RequestKind.Reminder);
            Assert.Equal(RequestStatus.Draft, reminder.Status); // a person approves it
            Assert.Equal(AuditActors.System, reminder.DraftedBy);
            Assert.NotEmpty(reminder.RequestedDocuments);
            await TickAsync(remindAt, new DeadlineTick(0, 0, 0)); // a second tick repeats neither

            Trail(AuditActors.System, AuditKinds.CaseReminded, AuditKinds.CaseEscalated);
            Log($"reminded and escalated at {Iso(remindAt)}, one reminder drafted; the second tick changed nothing");
        }

        /// <summary>The card's answer in time: a new self-certification through IChangeService first, then the response, then the close.</summary>
        private async Task AnswerAndCloseAsync(GoldenResponse answer, GoldenDeadline deadline)
        {
            AdvanceTo(answer.ReceivedAt);
            int? documentId = null;
            byte[]? document = null;
            if (answer.Document is { } golden)
            {
                var uploaded = await UploadAsync(golden);
                (documentId, document) = (uploaded.Id, uploaded.Bytes);
                await EvidenceAsync(golden, uploaded.Id);
            }

            int? selfCertificationId = null;
            if (answer.Kind == ResponseKind.NewSelfCertification)
            {
                Assert.NotNull(answer.SelfCertification);
                var before = State(await CaseAsync());
                var result = await _operator.Changes.RecordAsync(new ChangeRequest(_card.ClientCode, ChangeKind.SelfCertSubmitted, answer.Channel,
                    SelfCertification: answer.SelfCertification, DocumentId: documentId, CaseCode: _code));

                Assert.Equal(_code, result.CaseCode); // it answers this case and opens none
                Assert.Empty(result.Outcome.Uncovered); // valid (p. 34): nothing new for the case
                Assert.Equal(before, State(await CaseAsync()));
                if (_oracle is not null)
                    Assert.True(Same(answer.SelfCertification, Assert.Single(_oracle.SelfCertifications).SelfCertification),
                        $"case {_card.Id}: the engine checked another self-certification than the card's answer");
                selfCertificationId = await AnswerSelfCertificationAsync(answer.SelfCertification);
                Trail(OperatorActor, AuditKinds.ChangeReceived);
            }

            var response = await _operator.Cases.RegisterResponseAsync(_code, new NewResponse(answer.Kind, answer.Category, answer.Channel,
                $"Golden case {_card.Id}: {answer.Kind}", documentId, SelfCertificationId: selfCertificationId));
            Assert.Equal(answer.ReceivedAt, response.ReceivedAt);
            Assert.Equal(CaseStatus.ResponseReceived, (await CaseAsync()).Status);
            Trail(OperatorActor, AuditKinds.ResponseRegistered);
            Log($"{answer.Kind} ({answer.Category}) received at {Iso(answer.ReceivedAt)} via {answer.Channel}");

            // The operator closes an hour later: ConfirmedAt keeps the client's moment (p. 34), ClosedAt the operator's.
            var closeAt = answer.ReceivedAt.AddHours(1);
            AdvanceTo(closeAt);
            await _operator.Cases.CloseAsync(_code, answer.ProofChannel);
            Trail(OperatorActor, AuditKinds.CaseClosed);

            var item = await CaseAsync();
            Assert.Equal(answer.ExpectedStatus, item.Status);
            Assert.Equal(closeAt, item.ClosedAt);
            // Code re-evaluated the client with the answer before closing: the card's decision after it.
            Assert.Contains($"\"decision\":\"{answer.ExpectedDecision}\"", Assert.Single(_host.Audit.OfKind(AuditKinds.CaseClosed)).PayloadJson);
            Assert.Equal(Sorted(answer.ExpectedReportableFor), Sorted(item.ReportableFor));
            // The rule the close cites is the decided one; the card's, when it names one, must be that one too.
            var decided = ClosingRule(answer.Kind, _card.ExpectedRegime ?? item.Regime);
            var rule = string.IsNullOrWhiteSpace(answer.RuleId) ? decided : answer.RuleId;
            Assert.Equal(decided, rule);
            Assert.Equal(rule, item.OutcomeRuleId);
            Assert.Contains(rule, item.Outcome); // the code's text cites the rule it closed on, in both languages
            Assert.Contains(rule, item.OutcomeRo);

            var proof = item.Proof;
            Assert.NotNull(proof);
            Assert.Equal(answer.ProofChannel, proof.Channel);
            Assert.Equal(answer.ReceivedAt, proof.ConfirmedAt);
            Assert.Equal(_before.FullName, proof.SignedBy);
            Assert.Equal(SignerCapacity.AccountHolder, proof.SignerCapacity);
            Assert.Equal(Operator.Name, proof.ApprovedBy);
            Assert.Matches("^[0-9a-f]{64}$", proof.EvidenceSha256);
            if (document is not null)
            {
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(document)), proof.EvidenceSha256); // the answer's PDF
                Assert.Equal($"DOC-{documentId}", proof.TransactionOrDocumentId);
            }
            // p. 34: dated at the latest on the date of receipt; an explanation is dated and received when it arrives.
            Assert.Equal(answer.SelfCertification?.SignedAt ?? answer.ReceivedAt, proof.DeclarationDatedAt);
            Assert.Equal(answer.ReceivedAt, proof.DeclarationReceivedAt);
            Assert.True(proof.DeclarationDatedAt <= proof.DeclarationReceivedAt, $"case {_card.Id}: dated after it was received (p. 34)");

            await AcceptedExplanationsAsync(answer);

            // The saved client, evaluated again by the engine: the card's outcome after the answer, exactly, with CoveredBy
            // (GOLDEN-CASES.md 1).
            var saved = await _viewer.Clients.SnapshotAsync(_clientId);
            Assert.NotNull(saved);
            var again = _engine.EvaluateCurrent(saved);
            Assert.Equal(Verdict(answer.ExpectedDecision, Keys(answer.ExpectedIndicia, coverage: true)),
                Verdict(again.Decision, Keys(again.Indicia, coverage: true)));
            Assert.Equal(Sorted(answer.ExpectedReportableFor), Sorted(again.ReportableFor));
            Log($"closed at {Iso(closeAt)} on {item.OutcomeRuleId}, reportable for [{string.Join(", ", item.ReportableFor)}], " +
                $"proof {proof.Channel} {proof.TransactionOrDocumentId} confirmed {Iso(proof.ConfirmedAt)}; " +
                $"re-evaluated {Verdict(again.Decision, Keys(again.Indicia, coverage: true))}");

            // Closed: the watcher leaves it alone, whatever the date.
            if (deadline.DueAt is { } due)
            {
                await TickAsync(due.AddSeconds(1), new DeadlineTick(0, 0, 0));
                Assert.Equal(answer.ExpectedStatus, (await CaseAsync()).Status);
            }
        }

        /// <summary>The answer is the client's only valid self-certification; the one the change made unreliable is superseded (p. 35-36).</summary>
        private async Task<int> AnswerSelfCertificationAsync(SelfCertificationSnapshot expected)
        {
            await using var db = await _host.DbAsync();
            var valid = await db.SelfCertifications.AsNoTracking().Where(s => s.ClientId == _clientId && s.Status == SelfCertStatus.Valid).ToListAsync();
            var current = Assert.Single(valid);
            Assert.True(Same(expected, SelfCertificationSnapshot.From(current)), $"case {_card.Id}: the valid self-certification is not the card's answer");
            Assert.Equal(_host.Clock.UtcNow, current.ReceivedAt);
            return current.Id;
        }

        /// <summary>An explanation is saved per explained jurisdiction (the card's snapshot after the answer); a self-certification saves none.</summary>
        private async Task AcceptedExplanationsAsync(GoldenResponse answer)
        {
            await using var db = await _host.DbAsync();
            var accepted = await db.AcceptedExplanations.AsNoTracking().Where(a => a.ClientId == _clientId).ToListAsync();
            if (answer.Kind != ResponseKind.ReasonableExplanation)
            {
                Assert.Empty(accepted);
                return;
            }

            var explained = Sorted(answer.ExpectedIndicia.Where(h => h.CoveredBy == Coverage.AcceptedExplanation).Select(h => h.Jurisdiction ?? ""));
            Assert.Equal(explained, Sorted(accepted.Select(a => a.Jurisdiction)));
            Assert.All(accepted, a =>
            {
                Assert.Equal(answer.Category, a.Category);
                Assert.Equal(_code, a.CaseCode);
                Assert.Equal(DateOnly.FromDateTime(answer.ReceivedAt), DateOnly.FromDateTime(a.AcceptedAt)); // the card: the day of the answer
            });
            // The next evaluation reads it: the jurisdiction stays explained, so not reportable.
            var snapshot = await _viewer.Clients.SnapshotAsync(_clientId);
            Assert.NotNull(snapshot);
            Assert.Equal(explained, Sorted(snapshot.AcceptedExplanations.Select(e => e.Jurisdiction)));
        }

        /// <summary>No answer: nothing happens up to the deadline; a card that ends Expired then expires on its pinned rule.</summary>
        private async Task WaitAsync(GoldenDeadline deadline, CaseStatus waiting, string? expiry)
        {
            if (deadline.DueAt is not { } due)
            {
                // No deadline in the guide (FATCA, p. 38): the case never reminds and never expires.
                Assert.Null(expiry);
                await TickAsync(_host.Clock.UtcNow.AddDays(400), new DeadlineTick(0, 0, 0));
                Assert.Equal(waiting, (await CaseAsync()).Status);
                Log("no deadline: 400 days later the case still waits");
                return;
            }

            await TickAsync(due.AddSeconds(-1), new DeadlineTick(0, 0, 0));
            Assert.Equal(waiting, (await CaseAsync()).Status);
            Log($"still {waiting} at {Iso(due.AddSeconds(-1))}, one second before the deadline");
            if (expiry is not null) await ExpireAsync(due, expiry);
        }

        /// <summary>Past the deadline with no answer: Expired, with the consequence PLAN 16.1 gives the card (p. 29 or p. 35-36).</summary>
        private async Task ExpireAsync(DateTime due, string rule)
        {
            var at = due.AddSeconds(1); // the card of 17a: TickAsync(DueAt + 1 s)
            await TickAsync(at, new DeadlineTick(0, 0, 1));

            var item = await CaseAsync();
            Assert.Equal(CaseStatus.Expired, item.Status);
            Assert.Equal(at, item.ExpiredAt);
            Assert.Equal(rule, item.OutcomeRuleId);
            Assert.Contains(rule, item.Outcome);
            Assert.Contains(rule, item.OutcomeRo);
            Assert.DoesNotContain("RO", item.ReportableFor); // the card (17a): RO is never reportable for an RO resident
            if (_card.ExpectedReportableFor is { } reportable)
            {
                if (rule == "DL-EXPIRED-PRE") Assert.Superset(reportable.ToHashSet(), item.ReportableFor.ToHashSet()); // "at least" (p. 29)
                else Assert.Equal(Sorted(reportable), Sorted(item.ReportableFor));
            }
            await TickAsync(at, new DeadlineTick(0, 0, 0)); // a second tick changes nothing

            Trail(AuditActors.System, AuditKinds.CaseExpired);
            Log($"expired at {Iso(at)} on {rule}, reportable for [{string.Join(", ", item.ReportableFor)}]");
        }

        /// <summary>Every step left its entry on the case's audit log, in order and by its actor, and no state change twice.</summary>
        private async Task AuditTrailAsync()
        {
            var caseId = (await CaseAsync()).Id;
            var entries = _host.Audit.Entries.Where(e => e.CaseId == caseId).Select(e => (e.Kind, e.Actor)).ToList();
            var found = 0;
            foreach (var entry in entries)
                if (found < _trail.Count && entry == _trail[found]) found++;
            if (found < _trail.Count)
                Assert.Fail($"case {_card.Id}: the case's audit log lacks {_trail[found]} after the entries before it.\n" +
                            $"Expected in order: {string.Join(", ", _trail)}\nActual: {string.Join(", ", entries)}");

            var counts = StateChanges.Where(k => entries.Count(e => e.Kind == k) != _trail.Count(t => t.Kind == k))
                .Select(k => $"{k}: {entries.Count(e => e.Kind == k)} entries, expected {_trail.Count(t => t.Kind == k)}")
                .ToList();
            Assert.Empty(counts);
            Log($"audit log of {_code}: {entries.Count} entries, the {_trail.Count} expected ones in order");
        }

        private async Task TickAsync(DateTime at, DeadlineTick expected)
        {
            AdvanceTo(at);
            Assert.Equal(expected, await _viewer.Cases.TickAsync(_host.Clock.UtcNow)); // the watcher's call: no role needed
        }

        /// <summary>The demo way: the simulated clock's offset grows; the card's timeline never goes back.</summary>
        private void AdvanceTo(DateTime at)
        {
            var by = at - _host.Clock.UtcNow;
            if (by < TimeSpan.Zero) Assert.Fail($"case {_card.Id}: the timeline goes back, from {Iso(_host.Clock.UtcNow)} to {Iso(at)}");
            _host.Advance(by);
        }

        private async Task<Case> CaseAsync()
        {
            var item = await _viewer.Cases.GetAsync(_code);
            Assert.NotNull(item);
            return item;
        }

        private void Trail(string actor, params string[] kinds)
        {
            foreach (var kind in kinds) _trail.Add((kind, actor));
        }

        private void Log(string step) => _output.WriteLine($"case {_card.Id}, {(_oracle is null ? "real" : "oracle")} engine: {step}");
    }

    /// <summary>
    /// The card as the rule engine (R2 alone, no R1): the change gets the card's outcome; once the card's answer is in the
    /// client's snapshot (its new self-certification, or an accepted explanation in its category for every uncovered CRS
    /// jurisdiction) every call gets the covered outcome the card states. Records the change and self-certification calls.
    /// </summary>
    private sealed class CardEngine(GoldenCase card) : ICrsRuleEngine
    {
        public List<(ClientSnapshot Before, ClientSnapshot After, ChangeKind Kind)> Changes { get; } = [];
        public List<(ClientSnapshot Client, SelfCertificationSnapshot SelfCertification)> SelfCertifications { get; } = [];

        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind)
        {
            Changes.Add((before, after, kind));
            return AtTheChange();
        }

        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => Answered(client) ? AfterTheAnswer() : AtTheChange();

        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification)
        {
            SelfCertifications.Add((client, selfCertification));
            return card.Response?.SelfCertification is { } answer && Same(answer, selfCertification) ? AfterTheAnswer() : AtTheChange();
        }

        /// <summary>The card's outcome. ReportableFor: the declared jurisdictions (PLAN 7.2); the card's of 17a and 17b is the case's after expiry.</summary>
        private RuleOutcome AtTheChange() => Outcome(card.ExpectedDecision, card.ExpectedIndicia, card.ExpectedClearedRules,
            card.ExpectedIndicia.Where(h => h.CoveredBy == Coverage.SelfCertification).Select(h => h.Jurisdiction ?? ""));

        private RuleOutcome AfterTheAnswer()
        {
            var answer = card.Response!;
            return Outcome(answer.ExpectedDecision, answer.ExpectedIndicia, [], answer.ExpectedReportableFor);
        }

        private bool Answered(ClientSnapshot client) => card.Response switch
        {
            { Kind: ResponseKind.NewSelfCertification, SelfCertification: { } answer } => client.SelfCertification is { } current && Same(answer, current),
            { Kind: ResponseKind.ReasonableExplanation, Category: var category } => card.ExpectedIndicia
                .Where(h => h.CoveredBy == Coverage.None && RuleCatalog.Get(h.RuleId).Regime != Regime.Fatca)
                .All(h => client.AcceptedExplanations.Any(e => e.Jurisdiction == h.Jurisdiction && e.Category == category)),
            _ => false,
        };

        private RuleOutcome Outcome(Decision decision, IEnumerable<GoldenHit> indicia, IEnumerable<GoldenHit> cleared, IEnumerable<string> reportableFor) =>
            new(decision, [.. indicia.Select(Hit)], [.. cleared.Select(Hit)], Sorted(reportableFor.Distinct()),
                $"Golden card {card.Id}: {decision}.", $"Fișa-etalon {card.Id}: {decision}.", "golden-card");

        private static RuleHit Hit(GoldenHit hit)
        {
            var rule = RuleCatalog.Get(hit.RuleId);
            var jurisdiction = hit.Jurisdiction ?? "";
            return new RuleHit(rule.Id, rule.Regime, jurisdiction, $"{rule.Title} ({jurisdiction})", $"{rule.TitleRo} ({jurisdiction})",
                hit.GuidePage ?? rule.GuidePage, hit.SourceField ?? "", hit.CoveredBy);
        }
    }

    /// <summary>
    /// R1 half merged: Evaluate answers as the card expects (or gives the placeholder too), while EvaluateCurrent and
    /// EvaluateSelfCertification still give the phase 0 placeholder, word for word.
    /// </summary>
    private sealed class HalfEngine(GoldenCase card, bool realEvaluate) : ICrsRuleEngine
    {
        private readonly CardEngine _oracle = new(card);

        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) =>
            realEvaluate ? _oracle.Evaluate(before, after, kind) : Placeholder();

        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => Placeholder();

        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) => Placeholder();

        private static RuleOutcome Placeholder() =>
            new(Decision.NoCase, [], [], [], PlaceholderExplanation, "Motorul de reguli nu e gata încă (faza 0).", "0.0-phase0");
    }

    /// <summary>
    /// The card's Before as entities: everything the snapshot holds, with its client id, so the snapshots compare. What a
    /// snapshot does not hold takes the "standard RO resident" values (GOLDEN-CASES.md 1): the self-certification received
    /// when signed, at the counter; the CNP is the RO TIN; addresses valid since the first account.
    /// </summary>
    private static Client ClientFromSnapshot(ClientSnapshot s)
    {
        var since = s.Accounts.Count == 0 ? DateOnly.MinValue : s.Accounts.Min(a => a.OpenedOn);
        var space = s.FullName.LastIndexOf(' ');
        var selfCertifications = new List<SelfCertification>();
        if (s.SelfCertification is { } sc)
            selfCertifications.Add(new SelfCertification
            {
                SignedAt = sc.SignedAt, ReceivedAt = sc.SignedAt, Channel = "ghișeu", DeclaresUsPerson = sc.DeclaresUsPerson,
                Residences = [.. sc.Residences.Select(r => new TaxResidence { Country = r.Country, Tin = r.Tin, NoTinReason = r.NoTinReason, NoTinExplanation = r.NoTinExplanation })],
            });

        return new Client
        {
            Id = s.ClientId,
            Code = s.Code,
            FirstName = space < 0 ? s.FullName : s.FullName[..space],
            LastName = space < 0 ? "" : s.FullName[(space + 1)..],
            BirthDate = s.BirthDate,
            BirthCountry = s.BirthCountry,
            Cnp = s.SelfCertification?.Residences.FirstOrDefault(r => r.Country == "RO")?.Tin,
            Citizenships = [.. s.Citizenships],
            Addresses = [.. s.Addresses.Select(a => new Address { Kind = a.Kind, Line = a.Line, City = a.City, Country = a.Country, IsCurrent = a.IsCurrent, ValidFrom = since })],
            Phones = [.. s.Phones.Select(p => new Phone { Number = p.Number, Country = p.Country, IsCurrent = p.IsCurrent })],
            Accounts =
            [
                .. s.Accounts.Select(a => new Account
                {
                    Iban = a.Iban, Type = a.Type, OpenedOn = a.OpenedOn, Balance = a.Balance, Currency = a.Currency,
                    StandingOrders = [.. a.StandingOrders.Select(o => new StandingOrder { BeneficiaryAccount = o.BeneficiaryAccount, BeneficiaryCountry = o.BeneficiaryCountry, IsActive = o.IsActive })],
                }),
            ],
            PowersOfAttorney = [.. s.PowersOfAttorney.Select(p => new PowerOfAttorney { HolderName = p.HolderName, HolderCountry = p.HolderCountry, IsActive = p.IsActive, ValidFrom = since })],
            SelfCertifications = selfCertifications,
            AcceptedExplanations = [.. s.AcceptedExplanations.Select(e => new AcceptedExplanation { Jurisdiction = e.Jurisdiction, Category = e.Category, CaseCode = e.CaseCode, AcceptedAt = e.AcceptedAt })],
        };
    }

    /// <summary>A snapshot as text, field by field (its records hold lists, which compare by reference), in the invariant culture.</summary>
    private static string Canonical(ClientSnapshot s)
    {
        var lines = new List<string>
        {
            Invariant($"{s.ClientId} {s.Code} {s.FullName}, born {s.BirthDate:yyyy-MM-dd} in {s.BirthCountry ?? "-"}, citizen of {string.Join(",", s.Citizenships)}"),
        };
        lines.AddRange(s.Addresses.Select(a => Invariant($"address {a.Kind}: {a.Line} | {a.City} | {a.Country}{(a.IsCurrent ? " (current)" : "")}")));
        lines.AddRange(s.Phones.Select(p => Invariant($"phone {p.Number} {p.Country}{(p.IsCurrent ? " (current)" : "")}")));
        foreach (var a in s.Accounts)
        {
            lines.Add(Invariant($"account {a.Iban} {a.Type}, opened {a.OpenedOn:yyyy-MM-dd}, {a.Balance:0.00} {a.Currency}"));
            lines.AddRange(a.StandingOrders.Select(o => Invariant($"  standing order to {o.BeneficiaryAccount} ({o.BeneficiaryCountry}){(o.IsActive ? "" : " inactive")}")));
        }
        lines.AddRange(s.PowersOfAttorney.Select(p => Invariant($"power of attorney: {p.HolderName} ({p.HolderCountry}){(p.IsActive ? "" : " inactive")}")));
        lines.Add(s.SelfCertification is { } sc
            ? Invariant($"self-certification signed {Iso(sc.SignedAt)}, US person {sc.DeclaresUsPerson}: {string.Join("; ", sc.Residences.Select(r => $"{r.Country} {r.Tin ?? "-"} {r.NoTinReason} {r.NoTinExplanation}"))}")
            : "no self-certification");
        lines.AddRange(s.AcceptedExplanations.Select(e => Invariant($"accepted explanation {e.Jurisdiction} {e.Category} {e.CaseCode} {Iso(e.AcceptedAt)}")));
        return string.Join("\n", lines);
    }

    private const double LineLeft = 50, LineWidth = 495, FontSize = 10;

    /// <summary>A one-page synthetic PDF with the document's quotes, one per line, in Noto Sans (ă, ș, ț and ß survive extraction).</summary>
    private static byte[] QuotePdf(GoldenDocument document)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddTrueTypeFont(SyntheticPdfs.RegularFont);
        var page = builder.AddPage(PageSize.A4);
        var y = 780.0;
        string[] texts = [$"{document.FileName}: document sintetic, date fictive (test)", .. document.Quotes.Select(q => q.Text)];
        foreach (var text in texts)
            foreach (var line in Wrap(page, font, text))
            {
                page.AddText(line, FontSize, new PdfPoint(LineLeft, y), font);
                y -= 16;
            }
        return builder.Build();
    }

    /// <summary>Breaks between words, so no letter falls off the page (the shield would remove it as off-page text).</summary>
    private static IEnumerable<string> Wrap(PdfPageBuilder page, PdfDocumentBuilder.AddedFont font, string text)
    {
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Width(page, font, candidate) > LineWidth)
            {
                yield return line;
                candidate = word;
            }
            line = candidate;
        }
        yield return line;
    }

    private static double Width(PdfPageBuilder page, PdfDocumentBuilder.AddedFont font, string text)
    {
        var letters = page.MeasureText(text, FontSize, new PdfPoint(0, 0), font);
        return letters.Count == 0 ? 0 : letters[^1].EndBaseLine.X;
    }

    /// <summary>The same declaration: date, US person flag and residences (country, TIN, reason) in order.</summary>
    private static bool Same(SelfCertificationSnapshot a, SelfCertificationSnapshot b) =>
        a.SignedAt == b.SignedAt && a.DeclaresUsPerson == b.DeclaresUsPerson && a.Residences.SequenceEqual(b.Residences);

    /// <summary>What a decision is made of on a case: status, regime, deadline and indicia.</summary>
    private static string State(Case c) =>
        Invariant($"{c.Status} {c.Regime}, due {Iso(c.DueAt)}, reminder {Iso(c.RemindAt)}, preexisting {c.IsPreexisting}, indicia {string.Join(",", Keys(c.Indicia))}");

    private static string Verdict(Decision decision, IEnumerable<string> rules) => $"{decision}: {string.Join(", ", rules)}";

    private static string[] Keys(IEnumerable<GoldenHit> hits, bool coverage = false) =>
        Sorted(hits.Select(h => coverage ? $"{h.Key} {h.CoveredBy}" : h.Key));

    private static string[] Keys(IEnumerable<RuleHit> hits, bool coverage = false) =>
        Sorted(hits.Select(h => Key(h.RuleId, h.Jurisdiction) + (coverage ? $" {h.CoveredBy}" : "")));

    private static string[] Keys(IEnumerable<Indicium> indicia) => Sorted(indicia.Select(i => Key(i.RuleId, i.Jurisdiction)));

    private static string Key(string ruleId, string? jurisdiction) => string.IsNullOrEmpty(jurisdiction) ? ruleId : $"{ruleId}:{jurisdiction}";

    private static string[] Sorted(IEnumerable<string> values) => [.. values.Order(StringComparer.Ordinal)];

    private static string Iso(DateTime? at) => at?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) ?? "none";
}
