using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Evaluation;

/// <summary>Who owns the golden case in the tests: R1 (rule decision), R2 (deadline / lifecycle), R3 (shield).</summary>
public enum GoldenOwner { R1, R2, R3 }

/// <summary>
/// One golden case: the expected result written BEFORE the code (docs/PLAN.md 16.1). Owner of the data: R5; R1 and R2
/// read it in tests and ask R5 for corrections. Phase 0: id, title and expectation only; R5 adds the input data
/// (client snapshot before/after, change, documents) as new properties.
/// </summary>
public record GoldenCase(
    string Id,
    string Title,
    GoldenOwner Owner,
    Decision? ExpectedDecision,
    string[] ExpectedIndicia,
    string[] ExpectedCleared,
    string ExpectedOutcome);

public static class GoldenCases
{
    public static readonly IReadOnlyList<GoldenCase> All =
    [
        new("1", "RO resident, all data in Romania", GoldenOwner.R1, Decision.Compliant, [], [], "Compliant"),
        new("2", "Italian TIN in the wrong format", GoldenOwner.R1, Decision.Clarification, ["SC-TIN-FORMAT"], [], "Clarification: TIN format invalid"),
        new("3", "Missing TIN for Austria, with a valid reason", GoldenOwner.R1, Decision.Compliant, [], ["SC-TIN-MISSING"], "Compliant, reason recorded"),
        new("4", "Move to Munich, German phone, no RO phone (Maria)", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-ADDR", "CRS-IND-PHONE"], [], "Case opened, deadline started, clarification"),
        new("5a", "Standing order to a German IBAN from a current account", GoldenOwner.R1, Decision.Compliant, [], ["CRS-IND-SO"], "Compliant: not an indicium on depository accounts (p. 29)"),
        new("5b", "Same standing order from a custodial account", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-SO"], [], "Clarification: indicium"),
        new("6", "Passport with place of birth in the US", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-POB"], [], "FATCA alert"),
        new("7", "US citizenship in the passport", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-CIT"], [], "FATCA alert"),
        new("8", "Trap: RO resident with a salary from a German company (Andrei)", GoldenOwner.R1, Decision.Compliant, [], [], "Compliant; weak signal, explained"),
        new("9", "Dual residence RO + DE, declared correctly", GoldenOwner.R1, Decision.Compliant, [], ["SC-CONSISTENT"], "Compliant; reportable for DE"),
        new("10", "PDF with hidden white text", GoldenOwner.R3, Decision.ShieldAlert, [], [], "Red signal from the shield; the text never reaches the model"),
        new("11", "Power of attorney for a person with an address in Austria", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-POA"], [], "Clarification: indicium"),
        new("12", "In-care-of address in a reportable jurisdiction, the only address", GoldenOwner.R1, Decision.Clarification, ["CRS-IND-CO"], [], "Clarification: indicium"),
        new("13", "Trap: foreign (non-US) phone and a RO phone", GoldenOwner.R1, Decision.Compliant, [], ["CRS-IND-PHONE"], "Compliant"),
        new("14", "US phone, even with a RO phone", GoldenOwner.R1, Decision.FatcaAlert, ["FATCA-IND-PHONE"], [], "FATCA alert"),
        new("15", "Trap: move from Cluj to Iasi", GoldenOwner.R1, Decision.NoCase, [], ["GEN-DOMESTIC"], "No case, with an explanation"),
        new("16", "Move to Spain, no answer, a few days before the deadline", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR"], [], "Reminder and escalation"),
        new("17", "Same case after the deadline", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR"], [], "Expired: the exact consequence from the guide, per account age"),
        new("18", "Answer in time with a reasonable explanation (student)", GoldenOwner.R2, Decision.Clarification, ["CRS-IND-ADDR"], [], "Closed with channel, moment and proof"),
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
