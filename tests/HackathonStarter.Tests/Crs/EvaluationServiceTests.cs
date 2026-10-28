using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using Microsoft.Extensions.Configuration;

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
            Assert.Equal(totals.Evaluated, totals.Passed);
        }
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

    /// <summary>Answers exactly what the golden cases and the scenarios expect.</summary>
    private sealed class OracleEngine : ICrsRuleEngine
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
                return Outcome(c.ExpectedDecision, c.ExpectedIndicia.Select(h => Hit(h.RuleId, h.Jurisdiction, h.CoveredBy)),
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
