using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Evaluation;

/// <summary>Who owns the golden case in the tests (docs/GOLDEN-CASES.md, column "Strat"): R1 engine, R2 workflow, R3 shield.</summary>
public enum GoldenOwner { R1, R2, R3 }

/// <summary>
/// One golden case: the expected result written BEFORE the code (docs/GOLDEN-CASES.md, docs/PLAN.md 16.1). Owner of the
/// data: R5; R1 and R2 read it in tests and ask R5 for corrections. Phase 0: id, client, expectation only; R5 adds the input
/// data (Before / After snapshots, the change, documents) as new properties.
/// </summary>
/// <param name="ExpectedHits">Compared exactly as RuleId:Jurisdiction (with coverage in R5's full data).</param>
/// <param name="ExpectedCleared">Compared as a subset: the trap rule whose explanation matters.</param>
public record GoldenCase(
    string Id,
    string Title,
    string ClientCode,
    GoldenOwner Owner,
    Decision ExpectedDecision,
    string[] ExpectedHits,
    string[] ExpectedCleared,
    string? ExpectedDeadlineRule,
    string ExpectedOutcome);

public static class GoldenCases
{
    public static readonly IReadOnlyList<GoldenCase> All =
    [
        new("1", "RO phone replaced by another RO phone", "CL-0015", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], null, "No case"),
        new("2", "Self-certification with an Italian TIN in the wrong format", "CL-0002", GoldenOwner.R1, Decision.Clarification, ["SC-TIN-FORMAT:IT"], [], null, "Clarification: TIN format invalid"),
        new("3", "Self-certification without a TIN for Austria, with a reason", "CL-0003", GoldenOwner.R1, Decision.Compliant, ["SC-TIN-MISSING:AT"], [], null, "Compliant, reason recorded"),
        new("4", "Maria: Munich, German phone, no RO phone, new account", "CL-0001", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"], [], "DL-NEW-90", "Case opened, 90-day deadline, then closed"),
        new("5a", "Standing order to Germany from a current account", "CL-0005", GoldenOwner.R1, Decision.NoCase, [], ["CRS-IND-SO"], null, "No case: not an indicium on depository accounts (p. 29)"),
        new("5b", "Same standing order from a custodial account", "CL-0005", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-SO:DE"], [], "DL-NEW-90", "Clarification: indicium"),
        new("5c", "Standing order to the US from a current account", "CL-0005", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-SO:US"], ["CRS-IND-SO"], null, "FATCA alert, no deadline in the guide"),
        new("6", "Passport with place of birth in the US", "CL-0006", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-POB:US"], [], null, "FATCA alert"),
        new("7", "US citizenship (second passport)", "CL-0007", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-CIT:US"], [], null, "FATCA alert"),
        new("8", "Andrei: salary from a German company", "CL-0013", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], null, "No case; one weak signal, explained"),
        new("9", "Dual residence RO + DE declared, address in DE", "CL-0009", GoldenOwner.R1, Decision.Compliant, ["CRS-IND-ADDR:DE"], [], null, "Compliant, reportable for DE"),
        new("10", "PDF with hidden white text", "CL-0010", GoldenOwner.R3, Decision.ShieldAlert, [], [], null, "Red signal from the shield; the text never reaches the model"),
        new("11", "Power of attorney for a person with an address in Austria", "CL-0011", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-POA:AT"], [], "DL-NEW-90", "Clarification: indicium"),
        new("12", "In-care-of address in Italy, the only address (preexisting, low value)", "CL-0012", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-CO:IT"], ["FATCA-IND-CO"], "DL-PRE-YEAR", "Clarification: indicium"),
        new("13", "Andrei: German phone next to the RO phone", "CL-0013", GoldenOwner.R1, Decision.NoCase, [], ["CRS-IND-PHONE"], null, "No case"),
        new("14", "US phone next to the RO phone", "CL-0014", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-PHONE:US"], ["CRS-IND-PHONE"], null, "FATCA alert"),
        new("15", "Cluj to Iasi", "CL-0015", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], null, "No case, with an explanation"),
        new("16", "Move to Spain, new account, no answer, day 85", "CL-0016", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:ES"], [], "DL-NEW-90", "Reminded, then escalated"),
        new("17a", "Same case after the deadline", "CL-0016", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:ES"], [], "DL-EXPIRED-NEW", "Expired: resident in RO and ES"),
        new("17b", "Move to Spain in March, preexisting account", "CL-0017", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:ES"], [], "DL-PRE-YEAR", "Deadline 31 Dec; on expiry resident at least in ES (DL-EXPIRED-PRE)"),
        new("18", "Answer in time: student", "CL-0018", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR:DE"], [], "DL-NEW-90", "Compliant on re-evaluation, closed with proof; not reportable for DE"),
    ];
}

public record EvaluationRow(string CaseId, string Title, string Expected, string Actual, bool Passed, bool FalseAlarm, TimeSpan Duration);

public record EvaluationReport(DateTime RanAt, IReadOnlyList<EvaluationRow> Rows)
{
    public int Passed => Rows.Count(r => r.Passed);
    public int FalseAlarms => Rows.Count(r => r.FalseAlarm);
}

/// <summary>Runs the rules on the golden cases and the synthetic clients; the only source of the numbers on /metrics. Owner: R5.</summary>
public interface IEvaluationService
{
    Task<EvaluationReport> RunAsync(CancellationToken ct = default);
}

/// <summary>Phase 0 placeholder. R5 replaces it.</summary>
public sealed class EvaluationService : IEvaluationService
{
    public Task<EvaluationReport> RunAsync(CancellationToken ct = default) =>
        Task.FromResult(new EvaluationReport(DateTime.MinValue, []));
}

public static class EvaluationSetup
{
    public static IServiceCollection AddEvaluation(this IServiceCollection services) =>
        services.AddScoped<IEvaluationService, EvaluationService>();
}
