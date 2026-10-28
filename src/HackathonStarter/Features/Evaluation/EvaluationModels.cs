using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;

namespace HackathonStarter.Features.Evaluation;

/// <summary>Where a part's numbers come from (PLAN 16.3). Only <see cref="Real"/> numbers are a result.</summary>
public enum EvaluationSource { Real, Phase0Placeholder, Unavailable }

/// <summary>The source of one part and why, in both languages (show with Lang.Pick).</summary>
public sealed record SourceNote(EvaluationSource Source, string Reason, string ReasonRo)
{
    public bool IsResult => Source == EvaluationSource.Real;
}

/// <summary>
/// One run of <see cref="IEvaluationService"/>, in memory only (never in the database): client codes, rule IDs and numbers,
/// no personal data. The three parts are shown separately (PLAN 16.3).
/// </summary>
/// <param name="RanAt">From ISimClock, the business date.</param>
public sealed record EvaluationReport(DateTime RanAt, TimeSpan Duration, SpecCoverage Spec, UncertaintyReport Uncertainty,
                                      EfficiencyReport Efficiency);

/// <summary>
/// Part 1: the rule engine on the golden cases and the synthetic clients. Deterministic code on data written to the same
/// rules, so everything is expected to pass: <c>Source</c> says "the guide's rules are implemented and tested" only
/// when every evaluated row passes, otherwise how many do not match yet. Never "100% accuracy".
/// </summary>
/// <param name="EngineVersion">RuleOutcome.EngineVersion of the registered engine.</param>
public sealed record SpecCoverage(
    SourceNote Source, string EngineVersion,
    IReadOnlyList<CoverageRow> Golden, CoverageTotals GoldenTotals,
    IReadOnlyList<CoverageRow> Synthetic, CoverageTotals SyntheticTotals,
    TimeSpan Duration);

/// <summary>Shield = case 10, measured in part 2. NoInput = not transcribed yet: neither passed nor failed.</summary>
public enum CoverageStatus { Passed, Failed, NoInput, Shield }

/// <summary>One golden case or synthetic client against the engine (GOLDEN-CASES.md, 1). A check left null was not compared.</summary>
/// <param name="Id">The golden case ("5a") or the synthetic client's code. Titles carry first names, so the report keeps none:
/// <c>GoldenCases.Get(Id).Title</c>.</param>
/// <param name="Label">The scenario chosen at generation (synthetic clients only), never the engine's result.</param>
/// <param name="Workflow">Cases 16-18: only the decision and the deadline are compared here; GoldenWorkflowTests check the rest.</param>
/// <param name="IndiciaMatch">Exact on (RuleId, Jurisdiction, CoveredBy).</param>
/// <param name="ClearedMatch">The expected cleared rules are a subset of the engine's.</param>
/// <param name="MissingIndicia">Expected, not returned: "RuleId:Jurisdiction", plus "/Coverage" when covered.</param>
/// <param name="Error">The exception type, when the engine threw.</param>
public sealed record CoverageRow(
    string Id, string ClientCode, string? Label, string? LabelEn, bool Workflow, CoverageStatus Status,
    Decision Expected, Decision? Actual,
    bool? IndiciaMatch, bool? ClearedMatch, bool? ReportableMatches, DeadlineCheck? Deadline,
    IReadOnlyList<string> MissingIndicia, IReadOnlyList<string> UnexpectedIndicia, IReadOnlyList<string> MissingCleared,
    string? Error, TimeSpan Duration)
{
    public bool Evaluated => Status is CoverageStatus.Passed or CoverageStatus.Failed;
    public bool DecisionMatches => Actual == Expected;
    public bool ExpectsAction => CoverageTotals.IsAction(Expected);
    public bool Caught => Evaluated && ExpectsAction && Actual == Expected;
    public bool Missed => Evaluated && ExpectsAction && Actual != Expected;
    public bool FalseAlarm => Evaluated && !ExpectsAction && Actual is { } actual && CoverageTotals.IsAction(actual);

    /// <summary>The expected indicia, keyed like <see cref="MissingIndicia"/>: from the data, never from the engine.</summary>
    public IReadOnlyList<string> ExpectedIndicia { get; init; } = [];
    /// <summary>The expected cleared rules ("verified, does not apply"), keyed like <see cref="MissingCleared"/>.</summary>
    public IReadOnlyList<string> ExpectedCleared { get; init; } = [];
}

/// <summary>The deadline of an opened case, through IDeadlineCalculator.</summary>
/// <param name="Actual">Null when the engine opened no case.</param>
/// <param name="Matches">Null when the rule is not the calculator's (DL-EXPIRED-*: CaseService, GoldenWorkflowTests).</param>
public sealed record DeadlineCheck(GoldenDeadline Expected, DeadlineResult? Actual, bool? Matches);

/// <param name="Caught">An action was expected and the engine gave it.</param>
/// <param name="Missed">An action was expected; the engine gave NoCase, Compliant or another decision.</param>
/// <param name="FalseAlarms">NoCase or Compliant was expected; the engine opened a case.</param>
/// <param name="Passed">Decision and rules match.</param>
public sealed record CoverageTotals(int Cases, int NoInput, int Evaluated, int Passed, int Failed, int ActionsExpected,
                                    int Caught, int Missed, int FalseAlarms)
{
    /// <summary>A decision that opens a case or raises an alert.</summary>
    public static bool IsAction(Decision d) => d is Decision.Clarification or Decision.FatcaAlert or Decision.ShieldAlert;

    public static CoverageTotals Of(IReadOnlyList<CoverageRow> rows) => new(
        rows.Count,
        rows.Count(r => r.Status == CoverageStatus.NoInput),
        rows.Count(r => r.Evaluated),
        rows.Count(r => r.Status == CoverageStatus.Passed),
        rows.Count(r => r.Status == CoverageStatus.Failed),
        rows.Count(r => r.Evaluated && r.ExpectsAction),
        rows.Count(r => r.Caught),
        rows.Count(r => r.Missed),
        rows.Count(r => r.FalseAlarm));
}

/// <summary>Part 2: where there is uncertainty (the shield, quotes, the AI).</summary>
public sealed record UncertaintyReport(ShieldBench Shield, VerifierBench Quotes, ExtractionReport Extraction, SignalReport Signals);

/// <summary>The registered shield on every generated PDF, against <see cref="SyntheticPdf.Hidden"/> (the truth from generation).</summary>
/// <param name="FalseAlarms">Clean documents the shield did not call clean.</param>
public sealed record ShieldBench(SourceNote Source, IReadOnlyList<ShieldRow> Documents, IReadOnlyList<ConcealmentTally> ByConcealment,
                                 int CleanDocuments, int FalseAlarms, TimeSpan Duration);

/// <param name="Document">Index in the evaluated documents (SyntheticPdfs.FileNames): file names carry first names.</param>
/// <param name="Hidden">How the document hides text, from generation; None = clean.</param>
/// <param name="Found">Every reason the shield reported.</param>
/// <param name="Error">The exception type, when the shield could not read the file.</param>
public sealed record ShieldRow(int Document, string ClientCode, Concealment Hidden, ShieldVerdict Verdict, HiddenReason Found, string? Error)
{
    public bool FalseAlarm => Hidden == Concealment.None && Error is null && (Verdict != ShieldVerdict.Clean || Found != HiddenReason.None);
}

/// <param name="Hidden">Hidden fragments concealed this way (a fragment both white and tiny counts for both).</param>
/// <param name="Detected">Reported with the matching reason on the fragment's page.</param>
public sealed record ConcealmentTally(Concealment Concealment, int Hidden, int Detected)
{
    public int Missed => Hidden - Detected;
}

/// <summary>
/// The evidence verifier's test bench (code), not the model's: fixed quotes on the generated PDFs. True quotes should be
/// verified; quotes found only in the hidden text, and invented ones, rejected.
/// </summary>
public sealed record VerifierBench(SourceNote Source, QuoteTally True, QuoteTally Hidden, QuoteTally Invented, TimeSpan Duration)
{
    public const string Name = "Evidence verifier test bench";
    public const string NameRo = "Banca de test a verificatorului";
}

/// <param name="FoundOnlyInHiddenText">Rejected with the red flag. Hidden text is masked (PiiVault), so a hidden quote with
/// personal data can come back "not found" instead: still rejected.</param>
public sealed record QuoteTally(int Quotes, int Verified, int FoundOnlyInHiddenText)
{
    public int Rejected => Quotes - Verified;
    public double? VerifiedPercent => Percent(Verified);
    public double? RejectedPercent => Percent(Rejected);

    private double? Percent(int part) => Quotes == 0 ? null : Math.Round(100.0 * part / Quotes, 1);
}

/// <summary>R4's extraction (self-certification, passport), field by field against generation. Only from a real, live run.</summary>
public sealed record ExtractionReport(SourceNote Source, int? Fields, int? Correct);

/// <param name="EngineTakesSignals">Checked on every run: can an input of the engine carry a ContextSignal?</param>
/// <param name="CasesWithSignal">Golden cases with a signal quote, synthetic clients with a payslip (the foreign-salary trap).</param>
/// <param name="ChangedDecision">0 by construction when the engine takes no signal; null when that does not hold.</param>
public sealed record SignalReport(SourceNote Source, bool EngineTakesSignals, IReadOnlyList<string> CasesWithSignal, int? ChangedDecision);

/// <summary>Part 3: the agent's time per case against the mentor's manual estimate. No number is made up.</summary>
public sealed record EfficiencyReport(SourceNote Agent, double? AgentMinutesPerCase, SourceNote Manual, decimal? ManualMinutesPerCase)
{
    /// <summary>Manual minus agent minutes, only when both are a result; negative when the agent is slower.</summary>
    public decimal? SavedMinutesPerCase =>
        Agent.IsResult && Manual.IsResult && AgentMinutesPerCase is { } agent && ManualMinutesPerCase is { } manual ? manual - (decimal)agent : null;

    public SourceNote Saved => SavedMinutesPerCase is null
        ? new(EvaluationSource.Unavailable, "Compared only when the agent's time and the manual time both have a source.",
            "Se compară doar când timpul agentului și timpul manual au amândouă o sursă.")
        : new(EvaluationSource.Real, "The manual time minus the agent's time, both with a source.",
            "Timpul manual minus timpul agentului, amândouă cu sursă.");
}
