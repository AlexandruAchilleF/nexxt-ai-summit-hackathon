using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Tin;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// Phase 0 placeholder: compiles and answers NoCase, so other modules can be wired. R1 replaces the bodies.
/// <paramref name="tinFormat"/> is the synchronous, offline TIN format check (SC-TIN-FORMAT, SC-TIN-MISSING).
/// </summary>
public sealed class CrsRuleEngine(ITinFormat tinFormat) : ICrsRuleEngine
{
    public const string Version = "0.0-phase0";

    public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

    // Used by SC-TIN-FORMAT / SC-TIN-MISSING (R1). Kept as a field until then.
    private readonly ITinFormat _tinFormat = tinFormat;

    public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) => Placeholder();
    public RuleOutcome EvaluateCurrent(ClientSnapshot client) => Placeholder();
    public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) => Placeholder();

    private static RuleOutcome Placeholder() => new(Decision.NoCase, [], [], [],
        "Rule engine not implemented yet (phase 0 placeholder).", "Motorul de reguli nu e gata încă (faza 0).", Version);
}

public static class RulesSetup
{
    public static IServiceCollection AddRules(this IServiceCollection services) =>
        services.AddSingleton<ICrsRuleEngine, CrsRuleEngine>();
}
