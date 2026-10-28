using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// What code concluded for one change. A model never sets it. The rule engine returns NoCase .. FatcaAlert;
/// ShieldAlert is set by ChangeService when a document's shield verdict is HiddenTextRemoved (the engine never sees documents).
/// </summary>
public enum Decision { NoCase, Compliant, Clarification, FatcaAlert, ShieldAlert }

public enum Regime { Crs, Fatca, Both }

/// <summary>One rule of the catalog. <see cref="GuidePage"/> is the page of the ANAF guide (29.12.2025), or "NEVERIFICAT".</summary>
public record RuleDefinition(string Id, Regime Regime, string Title, string TitleRo, string GuidePage, int Version = 1);

/// <summary>
/// A rule that was checked: an indicium (it applies) or a cleared rule (it does not, with the reason). Explanations are
/// code templates in both languages, so the outcome never depends on the caller's culture; show them with Lang.Pick.
/// </summary>
public record RuleHit(string RuleId, Regime Regime, string Jurisdiction, string Explanation, string ExplanationRo, string GuidePage, string SourceField);

/// <param name="Indicia">Rules that apply; only these can open a case.</param>
/// <param name="Cleared">Rules checked that do not apply, each with the reason (the traps: depository account, RO phone next to a foreign one).</param>
/// <param name="ReportableFor">Jurisdictions the account is reportable for, e.g. ["DE"] for a correctly declared dual residence.</param>
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
    public Regime? Regime => Indicia.Count == 0 ? null
        : Indicia.All(i => i.Regime == Rules.Regime.Crs) ? Rules.Regime.Crs
        : Indicia.All(i => i.Regime == Rules.Regime.Fatca) ? Rules.Regime.Fatca
        : Rules.Regime.Both;
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
