using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// What code concluded for one change (docs/PLAN.md 7.2). A model never sets it.
/// NoCase = no indicium rule applied (the traps land here, with Cleared). Compliant = indicia exist, all covered
/// (self-certification or accepted explanation). Clarification / FatcaAlert = at least one uncovered CRS / FATCA indicium.
/// ShieldAlert = only from the shield (ChangeService on DocumentUploaded); the rule engine never returns it.
/// Precedence when several apply: ShieldAlert &gt; FatcaAlert &gt; Clarification &gt; Compliant &gt; NoCase.
/// </summary>
public enum Decision { NoCase, Compliant, Clarification, FatcaAlert, ShieldAlert }

public enum Regime { Crs, Fatca, Both }

/// <summary>One rule of the catalog. <see cref="GuidePage"/> is the page of the ANAF guide (29.12.2025), or "NEVERIFICAT".</summary>
public record RuleDefinition(string Id, Regime Regime, string Title, string TitleRo, string GuidePage, int Version = 1);

/// <summary>
/// A rule that was checked: an indicium (it applies) or a cleared rule (it does not, with the reason). Explanations are
/// code templates in both languages, so the outcome never depends on the caller's culture; show them with Lang.Pick.
/// </summary>
/// <param name="CoveredBy">None = uncovered: it starts an action. SelfCertification = the jurisdiction is declared (reportable
/// for it). AcceptedExplanation = explained (not reportable for it). A Cleared rule did not apply at all.</param>
public record RuleHit(string RuleId, Regime Regime, string Jurisdiction, string Explanation, string ExplanationRo, string GuidePage,
                      string SourceField, Coverage CoveredBy = Coverage.None);

public enum Coverage { None, SelfCertification, AcceptedExplanation }

/// <param name="Indicia">Every indicium found; those with CoveredBy = None start actions and open a case.</param>
/// <param name="Cleared">Rules checked that do not apply, each with the reason (the traps: depository account, RO phone next to a foreign one).</param>
/// <param name="ReportableFor">Jurisdictions the account is reportable for, e.g. ["DE"] for a correctly declared dual residence;
/// never the explained ones.</param>
/// <param name="Explanation">Text built by code from templates (English), never by a model.</param>
/// <param name="ExplanationRo">The same text in Romanian.</param>
public record RuleOutcome(
    Decision Decision,
    IReadOnlyList<RuleHit> Indicia,
    IReadOnlyList<RuleHit> Cleared,
    IReadOnlyList<string> ReportableFor,
    string Explanation,
    string ExplanationRo,
    string EngineVersion)
{
    public IEnumerable<RuleHit> Uncovered => Indicia.Where(i => i.CoveredBy == Coverage.None);

    /// <summary>The regime of the uncovered indicia (the case's regime), or null when nothing is uncovered.</summary>
    public Regime? Regime
    {
        get
        {
            var open = Uncovered.ToList();
            if (open.Count == 0) return null;
            if (open.All(i => i.Regime == Rules.Regime.Crs)) return Rules.Regime.Crs;
            if (open.All(i => i.Regime == Rules.Regime.Fatca)) return Rules.Regime.Fatca;
            return Rules.Regime.Both;
        }
    }
}

/// <summary>
/// CRS/FATCA rules as pure code (docs/PLAN.md section 8): no database, no model, no HTTP.
/// The same snapshots always give the same outcome.
/// </summary>
public interface ICrsRuleEngine
{
    /// <summary>A change: what changed between the two snapshots (Cluj to Iasi = NoCase, GEN-DOMESTIC).</summary>
    RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind);

    /// <summary>The client's current state, with no change: the agent's evaluateRules tool, re-evaluation before closing, metrics.</summary>
    RuleOutcome EvaluateCurrent(ClientSnapshot client);

    /// <summary>A new self-certification checked against the bank's data (TIN format, missing TIN reason, consistency).</summary>
    RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification);

    IReadOnlyList<RuleDefinition> Catalog { get; }
}
