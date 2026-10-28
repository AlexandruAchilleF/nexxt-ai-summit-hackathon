using System.Globalization;
using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using HackathonStarter.Tests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>
/// Maria's case (docs/GOLDEN-CASES.md, case 4) in memory: small fakes of the Phase 0 contracts, registered as singletons so a
/// test can change the state between runs (approve, move to day 85, deliver the answer). The rule engine is the real one.
/// </summary>
public sealed class MariaWorld
{
    public const string CaseCode = "CRS-2026-0001";
    public const string ClientCode = "CL-0001";
    public const int PayslipId = 11;
    public const int SelfCertDocumentId = 12;
    public static readonly DateTime OpenedAt = new(2026, 10, 28, 7, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime DueAt = new(2027, 1, 26, 7, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime AnsweredAt = new(2027, 1, 22, 8, 0, 0, DateTimeKind.Utc);

    public MariaWorld()
    {
        Engine = new CrsRuleEngine(Tins.Format);
        Clients = new FakeClients();
        Cases = new FakeCases(Clients, Engine);
        Changes = new FakeChanges(Clients, Engine);
    }

    public FakeTinValidator Tins { get; } = new();
    public FakeAuditLog Audit { get; } = new();
    public CrsRuleEngine Engine { get; }
    public FakeClients Clients { get; }
    public FakeCases Cases { get; }
    public FakeChanges Changes { get; }
    public Case Case => Cases.Case;

    /// <summary>Day 86: the reminder and the escalation happened, and the client's new self-certification arrived as a document.</summary>
    public void DeliverAnswer()
    {
        Case.Status = CaseStatus.Escalated;
        Case.RemindedAt = Case.RemindAt;
        Case.EscalatedAt = Case.RemindAt;
        Clients.AddDocument(SelfCertDocumentId, DocumentKind.SelfCertForm, SyntheticPdfs.SelfCertMaria, AnsweredAt);
    }

    public void Register(IServiceCollection s)
    {
        s.AddSingleton<IClientService>(Clients);
        s.AddSingleton<ICaseService>(Cases);
        s.AddSingleton<IChangeService>(Changes);
        s.AddSingleton<ICrsRuleEngine>(Engine);
        s.AddSingleton<ITinValidator>(Tins);
        s.AddSingleton<IAuditLog>(Audit);
        s.AddSingleton<IHiddenTextShield, HiddenTextShield>();
        s.AddSingleton<IEvidenceVerifier, EvidenceVerifier>();
        s.AddScoped<IShieldedDocuments, ShieldedDocuments>();
        s.AddScoped<StructuredAi>();
    }

    public Task<TestHost> HostAsync(IChatClient? model = null, DemoUser? user = null, Action<IServiceCollection>? more = null) =>
        TestHost.CreateAsync(model, user: user, configure: s =>
        {
            Register(s);
            more?.Invoke(s);
        }, extraAgents: new CrsSentinelAgent());
}

public sealed class FakeClients : IClientService
{
    public Client Maria { get; } = new()
    {
        Id = 1, Code = MariaWorld.ClientCode, FirstName = "Maria", LastName = "Popescu", BirthDate = new DateOnly(1991, 6, 8),
        BirthCountry = "RO", Cnp = "2910608400319", Citizenships = ["RO"],
        Addresses =
        [
            new() { Id = 1, ClientId = 1, Kind = AddressKind.Residence, Line = "Bd. Unirii 15, bl. 7, ap. 22, 030823", City = "București", Country = "RO", IsCurrent = false, ValidFrom = new DateOnly(2019, 3, 14) },
            new() { Id = 2, ClientId = 1, Kind = AddressKind.Residence, Line = "Leopoldstraße 12, 80802", City = "München", Country = "DE", IsCurrent = true, ValidFrom = new DateOnly(2026, 10, 15) },
        ],
        Phones =
        [
            new() { Id = 1, ClientId = 1, Number = "+40721384516", Country = "RO", IsCurrent = false },
            new() { Id = 2, ClientId = 1, Number = "+498945217730", Country = "DE", IsCurrent = true },
        ],
        Accounts =
        [
            new() { Id = 1, ClientId = 1, Iban = "RO19RZBR0000060021458391", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new DateOnly(2019, 3, 14), Balance = 18_450.00m, Currency = "RON" },
        ],
        SelfCertifications =
        [
            new() { Id = 1, ClientId = 1, SignedAt = new DateTime(2019, 3, 14, 0, 0, 0, DateTimeKind.Utc), ReceivedAt = new DateTime(2019, 3, 14, 0, 0, 0, DateTimeKind.Utc), Channel = "ghișeu", Residences = [new() { Country = "RO", Tin = "2910608400319" }] },
        ],
    };

    public List<ClientDocument> Documents { get; } = [];

    public FakeClients() =>
        AddDocument(MariaWorld.PayslipId, DocumentKind.Payslip, SyntheticPdfs.PayslipMaria, new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));

    public void AddDocument(int id, DocumentKind kind, string fileName, DateTime uploadedAt) =>
        Documents.Add(new ClientDocument
        {
            Id = id, ClientId = Maria.Id, Kind = kind, FileName = fileName, Content = SyntheticPdfs.Get(fileName).Bytes, Pages = 1,
            ShieldVerdict = ShieldVerdict.Clean, UploadedAt = uploadedAt,
        });

    public Task<List<Client>> ListAsync(string? search = null) => Task.FromResult(new List<Client> { Maria });
    public Task<Client?> GetAsync(string code) => Task.FromResult(code == Maria.Code ? Maria : null);

    public Task<List<DocumentInfo>> ListDocumentsAsync(string clientCode) => Task.FromResult(
        clientCode == Maria.Code
            ? Documents.Select(d => new DocumentInfo(d.Id, d.ClientId, d.Kind, d.FileName, d.Pages, d.ShieldVerdict, d.UploadedAt)).ToList()
            : []);

    public Task<ClientSnapshot?> SnapshotAsync(int clientId) => Task.FromResult(clientId == Maria.Id ? ClientSnapshot.From(Maria) : null);
    public Task<ClientDocument?> GetDocumentAsync(int documentId) => Task.FromResult(Documents.FirstOrDefault(d => d.Id == documentId));
}

/// <summary>The case transitions of PLAN 9.3, just enough for the agent: invalid transitions throw, like the real service.</summary>
public sealed class FakeCases : ICaseService
{
    public const string RequiredDocument = "Autocertificare nouă privind rezidența fiscală, semnată și datată";

    private readonly FakeClients _clients;
    private readonly CrsRuleEngine _engine;
    private readonly HiddenTextShield _shield = new();
    private readonly EvidenceVerifier _verifier = new();

    public FakeCases(FakeClients clients, CrsRuleEngine engine)
    {
        _clients = clients;
        _engine = engine;
        var outcome = engine.EvaluateCurrent(ClientSnapshot.From(clients.Maria));
        Case = new Case
        {
            Id = 1, Code = MariaWorld.CaseCode, ClientId = 1, ClientCode = MariaWorld.ClientCode, TriggerChangeId = 1, Regime = Regime.Crs,
            Status = CaseStatus.Open, IsPreexisting = false, OpenedAt = MariaWorld.OpenedAt, DueAt = MariaWorld.DueAt,
            RemindAt = MariaWorld.DueAt.AddDays(-5), DeadlineRuleId = "DL-NEW-90", DeadlineGuidePage = "20-21, 35-36",
            Indicia = [.. outcome.Uncovered.Select((h, i) => new Indicium
            {
                Id = i + 1, CaseId = 1, ChangeEventId = 1, RuleId = h.RuleId, Regime = h.Regime, Jurisdiction = h.Jurisdiction,
                Explanation = h.Explanation, ExplanationRo = h.ExplanationRo, GuidePage = h.GuidePage, SourceField = h.SourceField,
                AddedAt = MariaWorld.OpenedAt,
            })],
        };
    }

    public Case Case { get; }

    public Task<List<Case>> QueueAsync(CaseQueueFilter? filter = null) => Task.FromResult(
        filter?.ClientCode is { } code && code != Case.ClientCode ? [] : new List<Case> { Case });

    public Task<Case?> GetAsync(string code) => Task.FromResult(code == Case.Code ? Case : null);
    public Task<Case> OpenAsync(ChangeEvent change, RuleOutcome outcome, Func<int, Task>? logChange = null) => throw new NotSupportedException();

    public Task<Evidence> AddEvidenceAsync(NewEvidence e)
    {
        var doc = _clients.Documents.FirstOrDefault(d => d.Id == e.DocumentId) ?? throw new InvalidOperationException($"No document {e.DocumentId}.");
        var check = _verifier.Verify(e.Quote, _shield.Scan(doc.Content), e.Page);
        var evidence = new Evidence
        {
            Id = Case.Evidence.Count + 1, ClientId = Case.ClientId, CaseId = Case.Id, Source = e.Source, DocumentId = e.DocumentId,
            Page = e.Page, Quote = e.Quote, Verified = check.Verified, VerificationNote = check.Note, LinkedRuleId = e.LinkedRuleId,
            RecordedBy = e.RecordedBy, At = MariaWorld.OpenedAt,
        };
        Case.Evidence.Add(evidence);
        return Task.FromResult(evidence);
    }

    public Task<ContextSignal> AddSignalAsync(NewSignal s)
    {
        var signal = new ContextSignal
        {
            Id = Case.Signals.Count + 1, ClientId = Case.ClientId, CaseId = Case.Id, Description = s.Description, EvidenceId = s.EvidenceId,
            Strength = s.Strength, At = MariaWorld.OpenedAt,
        };
        Case.Signals.Add(signal);
        return Task.FromResult(signal);
    }

    public Task<List<Evidence>> ListEvidenceAsync(string clientCode) => Task.FromResult(Case.Evidence.ToList());
    public Task<List<ContextSignal>> ListSignalsAsync(string clientCode) => Task.FromResult(Case.Signals.ToList());
    public Task<IReadOnlyList<string>> RequiredDocumentsAsync(string caseCode) => Task.FromResult<IReadOnlyList<string>>([RequiredDocument]);

    public Task<ClientRequest> DraftRequestAsync(string caseCode, string language, string body, RequestKind kind = RequestKind.Initial,
                                                 string? actor = null)
    {
        Expect(caseCode, CaseStatus.Open, CaseStatus.ResponseReceived);
        var request = new ClientRequest
        {
            Id = Case.Requests.Count + 1, CaseId = Case.Id, Kind = kind, Language = language, Body = body, RequestedDocuments = [RequiredDocument],
            Status = RequestStatus.Draft, DraftedBy = actor ?? "agent", CreatedAt = MariaWorld.OpenedAt,
        };
        Case.Requests.Add(request);
        return Task.FromResult(request);
    }

    public Task<ClientRequest> ApproveRequestAsync(string caseCode, int requestId, string channel)
    {
        Expect(caseCode, CaseStatus.Open, CaseStatus.ResponseReceived);
        var request = Case.Requests.Single(r => r.Id == requestId && r.Status == RequestStatus.Draft);
        request.Status = RequestStatus.Sent;
        request.Channel = channel;
        request.ApprovedBy = "admin";
        request.ApprovedAt = MariaWorld.OpenedAt.AddHours(1);
        Case.Status = CaseStatus.AwaitingClient;
        return Task.FromResult(request);
    }

    public Task<ClientRequest> RejectRequestAsync(string caseCode, int requestId, string reason) => throw new NotSupportedException();

    public Task<ClientResponse> RegisterResponseAsync(string caseCode, NewResponse r)
    {
        Expect(caseCode, CaseStatus.AwaitingClient, CaseStatus.Reminded, CaseStatus.Escalated);
        var response = new ClientResponse
        {
            Id = Case.Responses.Count + 1, CaseId = Case.Id, Kind = r.Kind, Category = r.Category, Channel = r.Channel,
            ReceivedAt = MariaWorld.AnsweredAt, DocumentId = r.DocumentId, SelfCertificationId = r.SelfCertificationId,
            HasDocumentaryEvidence = r.HasDocumentaryEvidence, Summary = r.Summary,
        };
        Case.Responses.Add(response);
        Case.Status = CaseStatus.ResponseReceived;
        return Task.FromResult(response);
    }

    public Task<Case> CloseAsync(string caseCode, ConfirmationChannel channel,
        ConfirmationEvidenceKind evidenceKind = ConfirmationEvidenceKind.SignedDocument, string? collectionSteps = null)
    {
        Expect(caseCode, CaseStatus.ResponseReceived);
        var outcome = _engine.EvaluateCurrent(ClientSnapshot.From(_clients.Maria));
        if (outcome.Decision != Decision.Compliant) throw new InvalidOperationException($"Not compliant: {outcome.Decision}.");
        var response = Case.Responses.Last();
        Case.Status = CaseStatus.Closed;
        Case.ClosedAt = MariaWorld.AnsweredAt.AddHours(1);
        Case.ReportableFor = [.. outcome.ReportableFor];
        Case.Outcome = outcome.Explanation;
        Case.OutcomeRo = outcome.ExplanationRo;
        Case.OutcomeRuleId = "SC-CONSISTENT";
        Case.Proof = new ConfirmationProof
        {
            Id = 1, CaseId = Case.Id, Channel = channel, ConfirmedAt = Case.ClosedAt.Value,
            DeclarationDatedAt = new DateTime(2027, 1, 21, 0, 0, 0, DateTimeKind.Utc), DeclarationReceivedAt = response.ReceivedAt,
            CollectionSteps = collectionSteps ?? "", TransactionOrDocumentId = response.DocumentId?.ToString(CultureInfo.InvariantCulture),
            EvidenceKind = evidenceKind, EvidenceSha256 = new string('a', 64), SignedBy = "Maria Popescu", ApprovedBy = "admin",
        };
        return Task.FromResult(Case);
    }

    public Task<Case> EscalateAsync(string caseCode, string reason)
    {
        Expect(caseCode, CaseStatus.AwaitingClient, CaseStatus.Reminded);
        Case.Status = CaseStatus.Escalated;
        Case.EscalatedAt = MariaWorld.DueAt.AddDays(-5);
        return Task.FromResult(Case);
    }

    public Task<DeadlineTick> TickAsync(DateTime now) => Task.FromResult(new DeadlineTick(0, 0, 0));

    private void Expect(string caseCode, params CaseStatus[] allowed)
    {
        if (caseCode != Case.Code) throw new KeyNotFoundException($"No case {caseCode}.");
        if (!allowed.Contains(Case.Status)) throw new InvalidOperationException($"Invalid transition from {Case.Status}.");
    }
}

/// <summary>Only what the agent sends: a new self-certification, applied to the client and checked by the real engine.</summary>
public sealed class FakeChanges(FakeClients clients, CrsRuleEngine engine) : IChangeService
{
    public List<ChangeRequest> Requests { get; } = [];

    public Task<ChangeResult> RecordAsync(ChangeRequest request)
    {
        if (request.Kind != ChangeKind.SelfCertSubmitted || request.SelfCertification is not { } sc)
            throw new NotSupportedException($"The fake records only SelfCertSubmitted, not {request.Kind}.");
        Requests.Add(request);

        var client = clients.Maria;
        foreach (var old in client.SelfCertifications) old.Status = SelfCertStatus.Superseded;
        client.SelfCertifications.Add(new SelfCertification
        {
            Id = client.SelfCertifications.Max(s => s.Id) + 1, ClientId = client.Id, SignedAt = sc.SignedAt, ReceivedAt = MariaWorld.AnsweredAt,
            Channel = request.Channel, DeclaresUsPerson = sc.DeclaresUsPerson,
            Residences = [.. sc.Residences.Select(r => new TaxResidence { Country = r.Country, Tin = r.Tin, NoTinReason = r.NoTinReason, NoTinExplanation = r.NoTinExplanation })],
        });

        var outcome = engine.EvaluateSelfCertification(ClientSnapshot.From(client), sc);
        var change = new ChangeEvent
        {
            Id = Requests.Count + 1, ClientId = client.Id, Kind = request.Kind, SourceChannel = request.Channel, ReceivedAt = MariaWorld.AnsweredAt,
        };
        return Task.FromResult(new ChangeResult(change, outcome, request.CaseCode));
    }

    public Task<List<ChangeEvent>> ListAsync(string? clientCode = null, int take = 50) => Task.FromResult(new List<ChangeEvent>());
}

/// <summary>DE TINs of 11 digits have a valid format (local rules); everything else is not checked, unless a test says otherwise.</summary>
public sealed class FakeTinValidator : ITinValidator
{
    public Func<string, string, TinCheck>? Override { get; set; }
    public ITinFormat Format { get; } = new DeFormat();

    public Task<TinCheck> CheckAsync(string country, string tin, CancellationToken ct = default) =>
        Task.FromResult(Override?.Invoke(country, tin) ?? Format.Check(country, tin));

    private sealed class DeFormat : ITinFormat
    {
        public TinCheck Check(string country, string tin) =>
            country.Equals("DE", StringComparison.OrdinalIgnoreCase)
                ? new("DE", tin.Trim(), tin.Trim().Length == 11 && tin.Trim().All(char.IsDigit), TinCheckSource.LocalRules, "DE: 11 digits.")
                : new(country.ToUpperInvariant(), tin.Trim(), false, TinCheckSource.NotChecked, "No rules for this country.");

        public bool? IssuesTinToAllResidents(string country) => null;
    }
}

/// <summary>The entries the tools append, in memory (the real hash chain is tested over RealWorld and in AuditLogTests).</summary>
public sealed class FakeAuditLog : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    public Task<AuditEntry> AppendAsync(string actor, string kind, object? payload = null, int? caseId = null)
    {
        var e = new AuditEntry
        {
            Seq = Entries.Count + 1, Actor = actor, Kind = kind, CaseId = caseId,
            PayloadJson = payload is null ? "{}" : JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
        };
        Entries.Add(e);
        return Task.FromResult(e);
    }

    public Task<List<AuditEntry>> ListAsync(int? caseId = null, int take = 200) =>
        Task.FromResult(Entries.Where(e => caseId is null || e.CaseId == caseId).OrderByDescending(e => e.Seq).Take(take).ToList());

    public Task<AuditVerification> VerifyAsync() => Task.FromResult(new AuditVerification(true, Entries.Count, null, null, null));
    public Task TamperAsync(long seq) => throw new NotSupportedException();
}

/// <summary>A shield result chosen by the test (e.g. with hidden text).</summary>
public sealed class FakeShieldedDocuments(ShieldedDocument document) : IShieldedDocuments
{
    public Task<ShieldedDocument?> GetAsync(int documentId) => Task.FromResult(documentId == document.DocumentId ? document : null);
}

/// <summary>Sets the UI language for the rest of a test (Services.Lang reads the current UI culture).</summary>
public sealed class UiLanguage : IDisposable
{
    private readonly CultureInfo _before = CultureInfo.CurrentUICulture;

    public UiLanguage(string culture) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    public void Dispose() => CultureInfo.CurrentUICulture = _before;
}

public static class SentinelTestTools
{
    /// <summary>Calls one tool of the agent exactly as the runner does, and returns its JSON result.</summary>
    public static async Task<JsonElement> ToolAsync(this TestHost host, string name, object args, DemoUser? user = null)
    {
        var agent = (ToolAgent)host.Agent("crs-sentinel");
        var ctx = new ToolContext(user ?? SiteConfig.Users[0], [], host.Services);
        var tool = agent.Tools(ctx).Single(t => t.Name == name);
        var json = JsonSerializer.SerializeToElement(args, RunStore.Json);
        var arguments = new AIFunctionArguments(json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())) { Services = host.Services };
        var result = await tool.Function.InvokeAsync(arguments);
        return result is JsonElement je ? je : JsonSerializer.SerializeToElement(result, RunStore.Json);
    }

    /// <summary>Every string value in a JSON tree (to prove something never appears anywhere in a result).</summary>
    public static IEnumerable<string> Strings(this JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => [e.GetString()!],
        JsonValueKind.Object => e.EnumerateObject().SelectMany(p => p.Value.Strings()),
        JsonValueKind.Array => e.EnumerateArray().SelectMany(x => x.Strings()),
        _ => [],
    };

    public static List<string> ToolCalls(this AgentRun run) =>
        [.. run.Steps.Where(s => s.Kind == StepKind.ToolCall).Select(s => s.Tool!)];

    public static string Answer(this AgentRun run) =>
        string.Join("\n\n", run.Steps.Where(s => s.Kind == StepKind.Text).Select(s => s.Text));
}
