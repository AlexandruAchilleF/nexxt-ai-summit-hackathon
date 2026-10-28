using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// EvaluationService with a fake engine, shield and verifier (no network, no model): the numbers come from the
/// expectations written at generation, never from the engine under test.
/// </summary>
public class EvaluationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 19, 30, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyList<SyntheticPdf> Pdfs = [.. SyntheticPdfs.All()];
    private static readonly EvaluationInputs Inputs = new(() => GoldenCases.All, () => SyntheticSnapshots.Create(), () => Pdfs);

    private static EvaluationService Service(ICrsRuleEngine engine, IHiddenTextShield? shield = null, IEvidenceVerifier? verifier = null,
        IConfiguration? configuration = null, EvaluationInputs? inputs = null) =>
        new(engine, new DeadlineCalculator(), shield ?? new BlindShield(), verifier ?? new RejectingVerifier(), new FixedClock(Now),
            configuration, inputs ?? Inputs);

    [Fact]
    public async Task An_engine_that_returns_the_expectations_misses_nothing_and_raises_no_false_alarm()
    {
        var report = await Service(new OracleEngine()).RunAsync();
        var spec = report.Spec;

        Assert.Equal(EvaluationSource.Real, spec.Source.Source);
        Assert.Equal(OracleEngine.Version, spec.EngineVersion);
        Assert.Equal(Now, report.RanAt);
        foreach (var totals in new[] { spec.GoldenTotals, spec.SyntheticTotals })
        {
            Assert.Equal(0, totals.Missed);
            Assert.Equal(0, totals.FalseAlarms);
            Assert.Equal(totals.ActionsExpected, totals.Caught);
        }
        Assert.All(spec.Golden.Concat(spec.Synthetic).Where(r => r.Evaluated), r =>
            Assert.True(r.DecisionMatches && r.IndiciaMatch != false && r.ClearedMatch != false && r.ReportableMatches != false && r.Error is null, r.Id));
        Assert.Equal(spec.GoldenTotals.Evaluated, spec.GoldenTotals.Passed);
        Assert.Equal(spec.SyntheticTotals.Evaluated, spec.SyntheticTotals.Passed);
        Assert.Equal(GoldenCases.All.Count, spec.Golden.Count);
        Assert.Equal(GoldenCases.All.Count(c => c.HasInput && c.ExpectedDecision != Decision.ShieldAlert), spec.GoldenTotals.Evaluated);
        Assert.Equal(50, spec.SyntheticTotals.Evaluated);

        // The deadline goes through IDeadlineCalculator: 90 days on a new account, none for FATCA (Î1).
        var maria = Row(spec, "4").Deadline!;
        Assert.True(maria.Matches);
        Assert.Equal(("DL-NEW-90", GoldenCases.NewAccountDueAt), (maria.Actual!.RuleId, maria.Actual.DueAt));
        Assert.True(Row(spec, "5b").Deadline!.Matches);
        var fatca = Row(spec, "5c").Deadline!;
        Assert.True(fatca.Matches);
        Assert.Equal("FATCA-SC", fatca.Actual!.RuleId);
        Assert.Null(fatca.Actual.DueAt);
        // A self-certification check alone opens a case without a deadline, as CaseService does (Î1).
        var tinFormat = Row(spec, "2").Deadline!;
        Assert.True(tinFormat.Matches);
        Assert.Equal(("SC-TIN-FORMAT", (DateTime?)null), (tinFormat.Actual!.RuleId, tinFormat.Actual.DueAt));
    }

    [Fact]
    public async Task The_registered_engine_passes_every_golden_case_and_every_synthetic_client()
    {
        var services = new ServiceCollection().AddRules().AddTin().BuildServiceProvider();
        var spec = (await Service(services.GetRequiredService<ICrsRuleEngine>()).RunAsync()).Spec;

        Assert.All(spec.Golden.Where(r => r.Evaluated), r => Assert.Equal(CoverageStatus.Passed, r.Status));
        Assert.Equal((0, 0, 0), (spec.GoldenTotals.Failed, spec.GoldenTotals.Missed, spec.GoldenTotals.FalseAlarms));
        Assert.Equal((0, 0, 0), (spec.SyntheticTotals.Failed, spec.SyntheticTotals.Missed, spec.SyntheticTotals.FalseAlarms));
        Assert.Equal(50, spec.SyntheticTotals.Passed);
    }

    [Fact]
    public async Task An_engine_that_always_answers_NoCase_misses_every_expected_action()
    {
        var report = await Service(new NoCaseEngine("0.0-phase0")).RunAsync();
        var spec = report.Spec;

        var goldenActions = GoldenCases.All.Count(c => c.HasInput && c.ExpectedDecision is Decision.Clarification or Decision.FatcaAlert);
        var syntheticActions = SyntheticSnapshots.Create().Count(s => s.Expected is Decision.Clarification or Decision.FatcaAlert);
        Assert.True(goldenActions > 0 && syntheticActions > 0);
        Assert.Equal((goldenActions, 0, 0), (spec.GoldenTotals.Missed, spec.GoldenTotals.Caught, spec.GoldenTotals.FalseAlarms));
        Assert.Equal((syntheticActions, 0, 0), (spec.SyntheticTotals.Missed, spec.SyntheticTotals.Caught, spec.SyntheticTotals.FalseAlarms));

        // A missed case lists what the engine did not find, and the case opens no deadline.
        var maria = Row(spec, "4");
        Assert.Equal(CoverageStatus.Failed, maria.Status);
        Assert.Equal(["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"], maria.MissingIndicia.Order());
        Assert.False(maria.Deadline!.Matches);
        Assert.Null(maria.Deadline.Actual);

        // The phase 0 placeholder is named as such: these numbers are not a result.
        Assert.Equal(EvaluationSource.Phase0Placeholder, spec.Source.Source);
        Assert.False(spec.Source.IsResult);
    }

    [Fact]
    public async Task An_engine_that_always_asks_for_clarification_raises_a_false_alarm_on_every_quiet_case()
    {
        var spec = (await Service(new AlwaysEngine(Decision.Clarification)).RunAsync()).Spec;

        var golden = GoldenCases.All.Where(c => c.HasInput && c.ExpectedDecision != Decision.ShieldAlert).ToList();
        var synthetic = SyntheticSnapshots.Create();
        Assert.True(synthetic.Count(s => s.Expected is Decision.NoCase or Decision.Compliant) > 0);
        Assert.Equal(golden.Count(c => c.ExpectedDecision is Decision.NoCase or Decision.Compliant), spec.GoldenTotals.FalseAlarms);
        Assert.Equal(synthetic.Count(s => s.Expected is Decision.NoCase or Decision.Compliant), spec.SyntheticTotals.FalseAlarms);
        // Clarification where FATCA was expected is another decision: missed, not caught.
        Assert.Equal(golden.Count(c => c.ExpectedDecision == Decision.Clarification), spec.GoldenTotals.Caught);
        Assert.Equal(golden.Count(c => c.ExpectedDecision == Decision.FatcaAlert), spec.GoldenTotals.Missed);
        Assert.Equal(synthetic.Count(s => s.Expected == Decision.FatcaAlert), spec.SyntheticTotals.Missed);
    }

    [Fact]
    public async Task The_claim_implemented_and_tested_holds_only_when_every_row_passes()
    {
        var golden = Inputs with { Golden = () => [GoldenCases.Get("4"), GoldenCases.Get("5b"), GoldenCases.Get("5c")] };

        var passing = (await Service(new OracleEngine(), inputs: golden).RunAsync()).Spec.Source;
        Assert.Equal(EvaluationSource.Real, passing.Source);
        Assert.StartsWith("The guide's rules are implemented and tested: the registered engine (test-oracle) on 3 golden cases with input and 50 synthetic clients", passing.Reason);
        Assert.StartsWith("Regulile din ghid sunt implementate și testate: motorul înregistrat (test-oracle) pe 3 cazuri-etalon cu date de intrare și 50 de clienți sintetici", passing.ReasonRo);

        var failing = (await Service(new OracleEngine(wrongCase: "4"), inputs: golden).RunAsync()).Spec.Source;
        Assert.Equal(EvaluationSource.Real, failing.Source);
        Assert.DoesNotContain("implemented and tested", failing.Reason);
        Assert.DoesNotContain("implementate și testate", failing.ReasonRo);
        Assert.StartsWith("1 golden case does not match the specification yet (see the failed rows)", failing.Reason);
        Assert.StartsWith("1 caz-etalon nu se potrivește încă cu specificația (vezi rândurile picate)", failing.ReasonRo);
    }

    [Fact]
    public async Task A_new_self_certification_goes_to_EvaluateSelfCertification_and_without_one_the_case_has_no_input()
    {
        var maria = GoldenCases.Get("4");
        var form = new SelfCertificationSnapshot(Now, [new TaxResidenceSnapshot("DE", "86095174223")], DeclaresUsPerson: false);
        GoldenCase[] golden =
        [
            maria with { Id = "sc", Change = maria.Change! with { Kind = ChangeKind.SelfCertSubmitted, SelfCertification = form } },
            maria with { Id = "sc-empty", Change = maria.Change! with { Kind = ChangeKind.SelfCertSubmitted, SelfCertification = null } },
        ];
        var engine = new RecordingEngine();
        var spec = (await Service(engine, inputs: Inputs with { Golden = () => golden, Synthetic = () => [] }).RunAsync()).Spec;

        Assert.Equal([nameof(ICrsRuleEngine.EvaluateSelfCertification)], engine.Calls.Select(c => c.Method));
        Assert.Same(maria.After, engine.Calls[0].Client);
        Assert.Same(form, engine.Calls[0].Form);
        Assert.Equal(CoverageStatus.Failed, Row(spec, "sc").Status); // NoCase where a clarification was expected
        Assert.Equal(CoverageStatus.NoInput, Row(spec, "sc-empty").Status);
    }

    [Fact]
    public async Task An_engine_that_throws_fails_the_row_with_the_exception_type_and_counts_as_missed()
    {
        var spec = (await Service(new ThrowingEngine()).RunAsync()).Spec;

        var maria = Row(spec, "4");
        Assert.Equal(CoverageStatus.Failed, maria.Status);
        Assert.Equal(nameof(InvalidOperationException), maria.Error);
        Assert.Null(maria.Actual);
        foreach (var totals in new[] { spec.GoldenTotals, spec.SyntheticTotals })
        {
            Assert.Equal((totals.Evaluated, 0), (totals.Failed, totals.Passed));
            Assert.Equal((totals.ActionsExpected, 0, 0), (totals.Missed, totals.Caught, totals.FalseAlarms));
        }

        // The reason is that every evaluation threw, not that there was no input.
        Assert.Equal(EvaluationSource.Unavailable, spec.Source.Source);
        Assert.Contains("threw an exception on every evaluation", spec.Source.Reason);
        Assert.Contains("a aruncat o excepție la fiecare evaluare", spec.Source.ReasonRo);
        Assert.DoesNotContain("no golden case has input", spec.Source.Reason);
    }

    [Fact]
    public async Task A_case_without_input_is_reported_as_such_and_case_10_belongs_to_the_shield()
    {
        GoldenCase[] golden =
        [
            new("x1", "Not transcribed yet", "CL-0099", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-ADDR:DE"], [], null, ""),
            GoldenCases.Get("10"),
            GoldenCases.Get("4"),
        ];
        var report = await Service(new NoCaseEngine("test"), inputs: Inputs with { Golden = () => golden }).RunAsync();

        var noInput = Row(report.Spec, "x1");
        Assert.Equal(CoverageStatus.NoInput, noInput.Status);
        Assert.Null(noInput.Actual);
        Assert.False(noInput.Evaluated || noInput.Missed || noInput.FalseAlarm);
        Assert.Equal(CoverageStatus.Shield, Row(report.Spec, "10").Status);
        Assert.Equal((3, 1, 1, 1), (report.Spec.GoldenTotals.Cases, report.Spec.GoldenTotals.NoInput, report.Spec.GoldenTotals.Evaluated,
            report.Spec.GoldenTotals.Missed));
    }

    [Fact]
    public async Task The_synthetic_label_comes_from_the_scenario_not_from_the_engine()
    {
        var oracle = (await Service(new OracleEngine()).RunAsync()).Spec.Synthetic;
        var noCase = (await Service(new NoCaseEngine("test")).RunAsync()).Spec.Synthetic;

        var generated = SyntheticSnapshots.Create().Select(s => (s.Code, s.Label, s.LabelEn, s.Expected)).ToList();
        Assert.Equal(generated, oracle.Select(r => (r.Id, r.Label!, r.LabelEn!, r.Expected)));
        Assert.Equal(generated, noCase.Select(r => (r.Id, r.Label!, r.LabelEn!, r.Expected)));
        Assert.NotEqual(oracle.Select(r => r.Actual), noCase.Select(r => r.Actual));
    }

    [Fact]
    public async Task The_expected_rules_on_each_row_come_from_the_data_not_from_the_engine()
    {
        var spec = (await Service(new NoCaseEngine("test")).RunAsync()).Spec;

        Assert.Equal(["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"], Row(spec, "4").ExpectedIndicia.Order());
        Assert.Equal(["CRS-IND-SO:DE"], Row(spec, "5a").ExpectedCleared);
        Assert.Equal(["CRS-IND-ADDR:DE/SelfCertification"], Row(spec, "9").ExpectedIndicia);
        var generated = SyntheticSnapshots.Create().ToDictionary(s => s.Code);
        Assert.All(spec.Synthetic, r => Assert.Equal(
            (generated[r.Id].ExpectedHits.Count, generated[r.Id].ExpectedCleared.Count), (r.ExpectedIndicia.Count, r.ExpectedCleared.Count)));
    }

    [Fact]
    public void The_time_saved_is_compared_only_when_both_times_are_a_result()
    {
        SourceNote real = new(EvaluationSource.Real, "", ""), unavailable = new(EvaluationSource.Unavailable, "", "");

        var both = new EfficiencyReport(real, 4.5, real, 22.5m);
        Assert.Equal(18m, both.SavedMinutesPerCase);
        Assert.Equal(EvaluationSource.Real, both.Saved.Source);
        Assert.Equal(-2.5m, new EfficiencyReport(real, 25, real, 22.5m).SavedMinutesPerCase);

        var agentMissing = new EfficiencyReport(unavailable, 4.5, real, 22.5m);
        Assert.Null(agentMissing.SavedMinutesPerCase);
        Assert.Equal(EvaluationSource.Unavailable, agentMissing.Saved.Source);
        Assert.Null(new EfficiencyReport(real, 4.5, unavailable, null).SavedMinutesPerCase);
    }

    [Fact]
    public void Every_golden_title_has_a_Romanian_translation_for_metrics() =>
        Assert.All(GoldenCases.All, c => Assert.True(Lang.Translations.ContainsKey(c.Title), c.Title));

    [Fact]
    public async Task A_shield_that_sees_nothing_misses_every_kind_of_concealment()
    {
        var shield = (await Service(new NoCaseEngine("test")).RunAsync()).Uncertainty.Shield;

        Assert.Equal(EvaluationSource.Real, shield.Source.Source);
        Assert.Equal([Concealment.White, Concealment.Tiny, Concealment.OffPage, Concealment.Invisible], shield.ByConcealment.Select(t => t.Concealment));
        Assert.All(shield.ByConcealment, t =>
        {
            Assert.True(t.Hidden > 0, $"no {t.Concealment} document");
            Assert.Equal(0, t.Detected);
            Assert.Equal(t.Hidden, t.Missed);
        });
        Assert.Equal(Pdfs.Count(p => p.Hidden.Count == 0), shield.CleanDocuments);
        Assert.Equal(0, shield.FalseAlarms);
    }

    [Fact]
    public async Task The_registered_shield_and_verifier_on_the_generated_pdfs()
    {
        var report = await Service(new NoCaseEngine("test"), new HiddenTextShield(), new EvidenceVerifier()).RunAsync();
        var (shield, quotes) = (report.Uncertainty.Shield, report.Uncertainty.Quotes);

        Assert.All(shield.ByConcealment, t => Assert.Equal(t.Hidden, t.Detected));
        Assert.Equal(0, shield.FalseAlarms);
        Assert.Equal(Pdfs.Sum(p => p.MockQuotes.Count), quotes.True.Quotes);
        Assert.Equal(100.0, quotes.True.VerifiedPercent);
        Assert.Equal(100.0, quotes.Hidden.RejectedPercent);
        Assert.Equal(100.0, quotes.Invented.RejectedPercent);
        Assert.True(quotes.Invented.Quotes > 3 * Pdfs.Count, "the near misses are counted");
    }

    [Fact]
    public async Task An_invented_quote_that_the_generated_truth_contains_is_not_counted_as_invented()
    {
        var declaration = EvaluationService.InventedQuotes[2];
        var pdf = new SyntheticPdf("test.pdf", "CL-0099", [], [new PdfQuote(1, "Sold final: 15.491,20 RON")],
            ["DECLAR CA SUNT REZIDENT FISCAL IN GERMANIA DIN 01.09.2026."], []);

        var invented = EvaluationService.InventedFor(pdf).ToList();
        Assert.DoesNotContain(declaration, invented);
        Assert.Equal([EvaluationService.InventedQuotes[0], EvaluationService.InventedQuotes[1], "Sold final: 15.491,21 RON"], invented);

        // A verifier that finds the declaration in the visible text is not charged with accepting an invented quote.
        var quotes = (await Service(new NoCaseEngine("test"), verifier: new AcceptingVerifier(), inputs: Inputs with { Documents = () => [pdf] })
            .RunAsync()).Uncertainty.Quotes;
        Assert.Equal((3, 3), (quotes.Invented.Quotes, quotes.Invented.Verified));
    }

    [Fact]
    public async Task A_verifier_that_accepts_everything_rejects_no_invented_quote()
    {
        var quotes = (await Service(new NoCaseEngine("test"), verifier: new AcceptingVerifier()).RunAsync()).Uncertainty.Quotes;

        Assert.Equal(0, quotes.Invented.Rejected);
        Assert.Equal(0.0, quotes.Invented.RejectedPercent);
        Assert.Equal(0.0, quotes.Hidden.RejectedPercent);
    }

    [Fact]
    public async Task Without_a_live_run_the_extraction_and_the_agent_time_are_unavailable()
    {
        var report = await Service(new OracleEngine()).RunAsync();

        Assert.Equal(EvaluationSource.Unavailable, report.Uncertainty.Extraction.Source.Source);
        Assert.Null(report.Uncertainty.Extraction.Fields);
        Assert.Equal(EvaluationSource.Unavailable, report.Efficiency.Agent.Source);
        Assert.Null(report.Efficiency.AgentMinutesPerCase);
    }

    [Fact]
    public async Task The_manual_time_waits_for_the_mentor_until_it_is_configured()
    {
        var empty = (await Service(new OracleEngine()).RunAsync()).Efficiency;
        Assert.Equal(EvaluationSource.Unavailable, empty.Manual.Source);
        Assert.Contains("mentor", empty.Manual.Reason);
        Assert.Null(empty.ManualMinutesPerCase);

        var configured = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [EvaluationService.ManualMinutesKey] = "22.5" }).Build();
        var set = (await Service(new OracleEngine(), configuration: configured).RunAsync()).Efficiency;
        Assert.Equal(EvaluationSource.Real, set.Manual.Source);
        Assert.Equal(22.5m, set.ManualMinutesPerCase);
    }

    [Theory]
    [InlineData("22,5")]
    [InlineData("1,000")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task A_manual_time_that_is_not_a_positive_number_with_a_decimal_point_is_not_used(string value)
    {
        var configured = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [EvaluationService.ManualMinutesKey] = value }).Build();
        var manual = (await Service(new OracleEngine(), configuration: configured).RunAsync()).Efficiency;

        Assert.Equal(EvaluationSource.Unavailable, manual.Manual.Source);
        Assert.Contains("is not a positive number of minutes", manual.Manual.Reason);
        Assert.Null(manual.ManualMinutesPerCase);
    }

    [Theory]
    [InlineData(0, "0 cazuri-etalon")]
    [InlineData(1, "1 caz-etalon")]
    [InlineData(2, "2 cazuri-etalon")]
    [InlineData(19, "19 cazuri-etalon")]
    [InlineData(20, "20 de cazuri-etalon")]
    [InlineData(50, "50 de cazuri-etalon")]
    [InlineData(100, "100 de cazuri-etalon")]
    [InlineData(101, "101 cazuri-etalon")]
    [InlineData(119, "119 cazuri-etalon")]
    [InlineData(120, "120 de cazuri-etalon")]
    public void Romanian_counts_take_de_from_twenty_on(int n, string expected) =>
        Assert.Equal(expected, EvaluationService.CountRo(n, "caz-etalon", "cazuri-etalon"));

    [Fact]
    public async Task No_signal_changes_a_decision_because_the_engine_takes_none()
    {
        var signals = (await Service(new OracleEngine()).RunAsync()).Uncertainty.Signals;

        Assert.False(signals.EngineTakesSignals);
        Assert.Equal(0, signals.ChangedDecision);
        Assert.Equal(EvaluationSource.Real, signals.Source.Source);
        Assert.Contains("4", signals.CasesWithSignal); // Maria's payslip: "Angajator: Müller Logistik GmbH"
        Assert.Equal(2, signals.CasesWithSignal.Count(id => id.StartsWith("CL-1", StringComparison.Ordinal))); // the foreign-salary traps
    }

    private static CoverageRow Row(SpecCoverage spec, string id) => spec.Golden.Concat(spec.Synthetic).Single(r => r.Id == id);

    /// <summary>Answers exactly what the golden cases and the scenarios expect, except NoCase on <paramref name="wrongCase"/>.</summary>
    private sealed class OracleEngine(string? wrongCase = null) : ICrsRuleEngine
    {
        public const string Version = "test-oracle";
        private readonly Dictionary<string, SyntheticSnapshotCase> _synthetic = SyntheticSnapshots.Create().ToDictionary(s => s.Code);

        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => Expected(after);
        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => Expected(client);
        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) => Expected(client);

        private RuleOutcome Expected(ClientSnapshot after)
        {
            if (GoldenCases.All.FirstOrDefault(c => ReferenceEquals(c.After, after)) is { } c)
                return c.Id == wrongCase
                    ? Outcome(Decision.NoCase, [], [], [])
                    : Outcome(c.ExpectedDecision, c.ExpectedIndicia.Select(h => Hit(h.RuleId, h.Jurisdiction, h.CoveredBy)),
                        c.ExpectedClearedRules.Select(h => Hit(h.RuleId, h.Jurisdiction, Coverage.None)), c.ExpectedReportableFor ?? []);
            var s = _synthetic[after.Code];
            return Outcome(s.Expected, s.ExpectedHits.Select(h => Hit(h.RuleId, h.Jurisdiction, Coverage.None)),
                s.ExpectedCleared.Select(h => Hit(h.RuleId, h.Jurisdiction, Coverage.None)), []);
        }

        private static RuleOutcome Outcome(Decision decision, IEnumerable<RuleHit> indicia, IEnumerable<RuleHit> cleared, IReadOnlyList<string> reportable) =>
            new(decision, [.. indicia], [.. cleared], reportable, "oracle", "oracol", Version);

        private static RuleHit Hit(string ruleId, string? jurisdiction, Coverage coveredBy) =>
            new(ruleId, RuleCatalog.Get(ruleId).Regime, jurisdiction ?? "", "", "", RuleCatalog.Get(ruleId).GuidePage, "", coveredBy);
    }

    private sealed class NoCaseEngine(string version) : ICrsRuleEngine
    {
        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;
        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => NoCase();
        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => NoCase();
        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) => NoCase();
        private RuleOutcome NoCase() => new(Decision.NoCase, [], [], [], "", "", version);
    }

    private sealed class AlwaysEngine(Decision decision) : ICrsRuleEngine
    {
        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;
        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => Answer();
        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => Answer();
        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) => Answer();
        private RuleOutcome Answer() => new(decision, [], [], [], "", "", "test");
    }

    private sealed class RecordingEngine : ICrsRuleEngine
    {
        public List<(string Method, ClientSnapshot Client, SelfCertificationSnapshot? Form)> Calls { get; } = [];
        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => Record(nameof(Evaluate), after, null);
        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => Record(nameof(EvaluateCurrent), client, null);
        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) =>
            Record(nameof(EvaluateSelfCertification), client, selfCertification);

        private RuleOutcome Record(string method, ClientSnapshot client, SelfCertificationSnapshot? form)
        {
            Calls.Add((method, client, form));
            return new(Decision.NoCase, [], [], [], "", "", "test");
        }
    }

    private sealed class ThrowingEngine : ICrsRuleEngine
    {
        public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;
        public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => throw new InvalidOperationException();
        public RuleOutcome EvaluateCurrent(ClientSnapshot client) => throw new InvalidOperationException();
        public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) =>
            throw new InvalidOperationException();
    }

    private sealed class BlindShield : IHiddenTextShield
    {
        public ShieldResult Scan(byte[] pdf) => new(ShieldVerdict.Clean, [], []);
    }

    private sealed class RejectingVerifier : IEvidenceVerifier
    {
        public EvidenceCheck Verify(string quote, ShieldResult document, int? page = null) => new(false, null, "fake");
    }

    private sealed class AcceptingVerifier : IEvidenceVerifier
    {
        public EvidenceCheck Verify(string quote, ShieldResult document, int? page = null) => new(true, page ?? 1, "fake");
    }

    private sealed class FixedClock(DateTime now) : ISimClock
    {
        public DateTime UtcNow => now;
        public TimeSpan Offset => TimeSpan.Zero;
        public event Action? Changed { add { } remove { } }
        public void Advance(TimeSpan by) { }
        public void Reset() { }
    }
}
