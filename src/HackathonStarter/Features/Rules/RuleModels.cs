using System.Text.Json.Serialization;

namespace HackathonStarter.Features.Rules;

// Contracts from docs/PLAN.md 8.1 and 7.2. Extra members are trailing and optional, so the PLAN shapes still compile.

public enum Regime { Crs, Fatca, Both }

// NoCase = no indicium applied (traps land here, in Cleared). Compliant = indicia exist, all covered.
// Clarification / FatcaAlert = at least one uncovered CRS / FATCA indicium. ShieldAlert comes only from the shield.
// Precedence: ShieldAlert > FatcaAlert > Clarification > Compliant > NoCase.
public enum Decision { NoCase, Compliant, Clarification, FatcaAlert, ShieldAlert }

public enum Coverage { None, SelfCertification, AcceptedExplanation }

/// <summary>
/// How solid the ground under a rule or a hit is. Verified = the text is in the law or the guide. Interpretation = our
/// reading where the sources are silent or ambiguous (shown in the UI). Policy = a bank choice, not a legal obligation.
/// </summary>
public enum RuleBasis { Verified, Interpretation, Policy }

public record RuleDefinition(string Id, Regime Regime, string Title, string Description, string GuidePage, int Version,
    string TitleEn = "", string LegalBasis = "", RuleBasis Basis = RuleBasis.Verified, string DescriptionEn = "");

public record RuleHit(string RuleId, Regime Regime, string Jurisdiction, string Explanation, string GuidePage, string SourceField,
    Coverage CoveredBy = Coverage.None, string ExplanationEn = "", RuleBasis Basis = RuleBasis.Verified);

public record RuleOutcome(
    Decision Decision,
    IReadOnlyList<RuleHit> Indicia,
    IReadOnlyList<RuleHit> Cleared,
    IReadOnlyList<string> ReportableFor,
    string Explanation,
    string EngineVersion,
    string ExplanationEn = "")
{
    /// <summary>The indicia that still need action (no self-certification or accepted explanation covers them).</summary>
    [JsonIgnore]
    public IReadOnlyList<RuleHit> Uncovered => [.. Indicia.Where(h => h.CoveredBy == Coverage.None)];

    /// <summary>Regime of the case R2 opens: Both when CRS and FATCA indicia are uncovered; null when there is no case.</summary>
    public Regime? CaseRegime
    {
        get
        {
            var crs = Uncovered.Any(h => h.Regime == Regime.Crs);
            var fatca = Uncovered.Any(h => h.Regime == Regime.Fatca);
            return crs && fatca ? Regime.Both : fatca ? Regime.Fatca : crs ? Regime.Crs : null;
        }
    }
}
