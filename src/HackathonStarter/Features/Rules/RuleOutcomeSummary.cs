namespace HackathonStarter.Features.Rules;

public sealed record RuleFinding(string RuleId, string Jurisdiction, string Title, string Explanation, string GuidePage,
    string LegalBasis, Coverage CoveredBy, RuleBasis Basis);

/// <summary>
/// A small, model-friendly view of a <see cref="RuleOutcome"/> in one language: what the agent tool returns and what
/// the case page shows. The model may explain it, never change it.
/// </summary>
public sealed record RuleOutcomeSummary(
    Decision Decision,
    Regime? Regime,
    IReadOnlyList<RuleFinding> OpenIndicia,
    IReadOnlyList<RuleFinding> CoveredIndicia,
    IReadOnlyList<RuleFinding> Cleared,
    IReadOnlyList<string> ReportableFor,
    string Explanation,
    string EngineVersion,
    bool RestsOnInterpretation);

public static class RuleOutcomeSummaries
{
    private const int MaxPerList = 10;

    public static RuleOutcomeSummary Summarize(this RuleOutcome outcome, bool en = false)
    {
        RuleFinding Finding(RuleHit h)
        {
            var rule = RuleCatalog.Get(h.RuleId);
            return new RuleFinding(h.RuleId, h.Jurisdiction, en ? rule.Title : rule.TitleRo,
                en ? h.Explanation : h.ExplanationRo, h.GuidePage, rule.LegalBasis, h.CoveredBy, h.Basis);
        }

        return new RuleOutcomeSummary(
            outcome.Decision,
            outcome.Regime,
            [.. outcome.Indicia.Where(h => h.CoveredBy == Coverage.None).Take(MaxPerList).Select(Finding)],
            [.. outcome.Indicia.Where(h => h.CoveredBy != Coverage.None).Take(MaxPerList).Select(Finding)],
            [.. outcome.Cleared.Take(MaxPerList).Select(Finding)],
            outcome.ReportableFor,
            en ? outcome.Explanation : outcome.ExplanationRo,
            outcome.EngineVersion,
            outcome.Indicia.Any(h => h.CoveredBy == Coverage.None && h.Basis != RuleBasis.Verified));
    }
}
