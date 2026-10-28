namespace HackathonStarter.Features.Rules;

/// <summary>Collects hits and Cleared entries while the detectors run; one entry per (rule, jurisdiction).</summary>
internal sealed class Findings
{
    public List<RuleHit> Hits { get; } = [];
    public List<RuleHit> Cleared { get; } = [];

    public void Hit(string ruleId, string jurisdiction, string sourceField, string ro, string en,
        RuleBasis basis = RuleBasis.Verified)
    {
        var rule = RuleCatalog.Get(ruleId);
        var hit = new RuleHit(ruleId, rule.Regime, jurisdiction, ro, rule.GuidePage, sourceField, Coverage.None, en, basis);
        var index = Hits.FindIndex(h => h.RuleId == ruleId && h.Jurisdiction == jurisdiction);
        if (index < 0) Hits.Add(hit);
        else if (basis == RuleBasis.Verified && Hits[index].Basis != RuleBasis.Verified) Hits[index] = hit; // keep the firmest ground
    }

    public void Clear(string ruleId, string jurisdiction, string sourceField, string ro, string en,
        RuleBasis basis = RuleBasis.Verified)
    {
        if (Cleared.Any(c => c.RuleId == ruleId && c.Jurisdiction == jurisdiction)) return;
        var rule = RuleCatalog.Get(ruleId);
        Cleared.Add(new RuleHit(ruleId, rule.Regime, jurisdiction, ro, rule.GuidePage, sourceField, Coverage.None, en, basis));
    }

    /// <summary>A rule that hit for a jurisdiction is not also "cleared" for it (e.g. two orders, only one from a deposit account).</summary>
    public IReadOnlyList<RuleHit> ClearedWithoutHits() =>
        [.. Cleared.Where(c => !Hits.Any(h => h.RuleId == c.RuleId && h.Jurisdiction == c.Jurisdiction))];
}

/// <summary>Account facts the detectors need: which regime treats the client's accounts as preexisting.</summary>
internal sealed record AccountScope(bool CrsPreexisting, bool FatcaPreexisting, bool FatcaLowerValue)
{
    public static AccountScope Of(RuleSubject s)
    {
        var accounts = s.Accounts;
        var crs = accounts.Any(a => AccountStatus.IsCrsPreexisting(a.OpenedOn));
        var fatca = accounts.Any(a => AccountStatus.IsFatcaPreexisting(a.OpenedOn));

        // Lower value: every account FATCA-preexisting and the aggregate at most USD 1,000,000 (unknown currency = not lower value).
        var usd = accounts.Select(a => AccountStatus.UsdUpperBound(a.Balance, a.Currency)).ToList();
        var lowerValue = accounts.Count > 0 && accounts.All(a => AccountStatus.IsFatcaPreexisting(a.OpenedOn))
            && usd.All(v => v is not null) && usd.Sum(v => v!.Value) <= AccountStatus.FatcaLowerValueLimitUsd;

        return new AccountScope(crs, fatca, lowerValue);
    }

    public RuleBasis CrsBasis => CrsPreexisting ? RuleBasis.Verified : RuleBasis.Interpretation;
    public RuleBasis FatcaBasis => FatcaPreexisting ? RuleBasis.Verified : RuleBasis.Interpretation;
}

/// <summary>Shared phrases for the explanations (RO / EN), so every detector says the same thing the same way.</summary>
internal static class Phrases
{
    public static string Place(string code, bool en = false) => Jurisdictions.Display(code, en);

    public const string NewAccountRo =
        " Cont nou: indiciul e folosit ca «motiv să știe» că autocertificarea e contrazisă (GEN-NEW-ACCOUNT, interpretare de design).";
    public const string NewAccountEn =
        " New account: the indicium is used as 'reason to know' the self-certification is contradicted (GEN-NEW-ACCOUNT, design interpretation).";

    public static (string Ro, string En) WithBasis(string ro, string en, RuleBasis basis) =>
        basis == RuleBasis.Interpretation ? (ro + NewAccountRo, en + NewAccountEn) : (ro, en);
}
