using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Components.Crs;

public enum PromotedRuleRole { Uncovered, Covered, Cleared }

public sealed record PromotedRule(RuleHit Hit, PromotedRuleRole Role);

/// <summary>Display selection only: decisions and deadlines remain the engine's and case service's values.</summary>
public static class ChangeVerdict
{
    public static PromotedRule? Select(RuleOutcome outcome)
    {
        var (hit, role) = outcome.Decision switch
        {
            Decision.FatcaAlert => (outcome.Uncovered.FirstOrDefault(h => h.Regime == Regime.Fatca), PromotedRuleRole.Uncovered),
            Decision.Clarification => (outcome.Uncovered.FirstOrDefault(h => h.Regime == Regime.Crs), PromotedRuleRole.Uncovered),
            Decision.Compliant => (outcome.Indicia.FirstOrDefault(h => h.CoveredBy != Coverage.None), PromotedRuleRole.Covered),
            Decision.NoCase => (outcome.Cleared.FirstOrDefault(), PromotedRuleRole.Cleared),
            _ => ((RuleHit?)null, PromotedRuleRole.Uncovered),
        };
        return hit is null ? null : new(hit, role);
    }

    // Case exposes no deadline-start anchor after extension; OpenedAt is safe only for its opening change.
    public static int? DeadlineDays(Case item, int recordedChangeId) =>
        recordedChangeId > 0 && item.TriggerChangeId == recordedChangeId && item.DueAt is { } due
            ? CrsUi.DaysBetween(item.OpenedAt, due)
            : null;
}
