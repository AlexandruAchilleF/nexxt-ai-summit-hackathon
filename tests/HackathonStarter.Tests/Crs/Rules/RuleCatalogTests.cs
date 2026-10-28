using System.Reflection;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Tests.Crs.Rules;

/// <summary>The catalog is what the UI, the audit log and the agent cite: every rule needs its source.</summary>
public class RuleCatalogTests
{
    private static IEnumerable<string> DeclaredIds() => typeof(RuleIds)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Rule_ids_are_unique()
    {
        var ids = RuleCatalog.All.Select(r => r.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Every_declared_id_is_in_the_catalog_and_back()
    {
        Assert.Equal(DeclaredIds().Order(), RuleCatalog.All.Select(r => r.Id).Order());
    }

    [Fact]
    public void Every_rule_has_titles_descriptions_legal_basis_and_a_known_page()
    {
        foreach (var rule in RuleCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Title), rule.Id);
            Assert.False(string.IsNullOrWhiteSpace(rule.TitleEn), rule.Id);
            Assert.False(string.IsNullOrWhiteSpace(rule.Description), rule.Id);
            Assert.False(string.IsNullOrWhiteSpace(rule.DescriptionEn), rule.Id);
            Assert.False(string.IsNullOrWhiteSpace(rule.LegalBasis), rule.Id);
            Assert.False(string.IsNullOrWhiteSpace(rule.GuidePage), rule.Id);
            Assert.NotEqual("?", rule.GuidePage);
            Assert.True(rule.Version >= 1, rule.Id);
        }
    }

    [Fact]
    public void Rule_regime_matches_its_prefix()
    {
        foreach (var rule in RuleCatalog.All)
        {
            if (rule.Id.StartsWith("FATCA-")) Assert.Equal(Regime.Fatca, rule.Regime);
            if (rule.Id.StartsWith("CRS-") || rule.Id.StartsWith("SC-") || rule.Id.StartsWith("DL-")) Assert.Equal(Regime.Crs, rule.Regime);
        }
    }

    [Fact]
    public void Interpretations_and_policies_are_labelled_as_such()
    {
        Assert.Equal(RuleBasis.Interpretation, RuleCatalog.Get(RuleIds.Domestic).Basis);
        Assert.Equal(RuleBasis.Interpretation, RuleCatalog.Get(RuleIds.NewAccountIndicia).Basis);
        Assert.Equal(RuleBasis.Interpretation, RuleCatalog.Get(RuleIds.DeadlinePreexistingYear).Basis);
        Assert.Equal(RuleBasis.Policy, RuleCatalog.Get(RuleIds.TinFormat).Basis);
        Assert.Equal(RuleBasis.Policy, RuleCatalog.Get(RuleIds.FatcaScopeAllAccounts).Basis);
        Assert.Equal(RuleBasis.Verified, RuleCatalog.Get(RuleIds.CrsStandingOrder).Basis);
    }

    [Fact]
    public void Unknown_rule_id_throws()
    {
        Assert.Throws<KeyNotFoundException>(() => RuleCatalog.Get("CRS-IND-NOPE"));
    }

    [Theory]
    [InlineData("RO", false)]
    [InlineData("ro", false)]
    [InlineData("DE", true)]
    [InlineData(" de ", true)]
    [InlineData("AT", true)]
    [InlineData("EL", true)] // Greece, EU code
    [InlineData("GR", true)]
    [InlineData("CH", true)] // ANAF list 1.a
    [InlineData("GB", true)]
    [InlineData("MD", true)]
    [InlineData("OM", true)] // ANAF list 1.b: still reported
    [InlineData("US", false)] // FATCA only
    [InlineData("AE", false)] // ANAF list 2: not reported
    [InlineData("KY", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Crs_reportable_jurisdictions(string? country, bool expected)
    {
        Assert.Equal(expected, Jurisdictions.IsCrsReportable(country));
    }

    [Fact]
    public void Every_other_eu_member_state_is_crs_reportable()
    {
        Assert.Equal(27, Jurisdictions.EuMemberStates.Count);
        Assert.All(Jurisdictions.EuMemberStates.Where(c => c != "RO"), c => Assert.True(Jurisdictions.IsCrsReportable(c), c));
    }

    [Fact]
    public void Tin_issuance_comes_from_the_oecd_sheets_we_read()
    {
        Assert.False(Jurisdictions.IssuesTinToAll("AT"));
        Assert.True(Jurisdictions.IssuesTinToAll("DE"));
        Assert.True(Jurisdictions.IssuesTinToAll("IT"));
        Assert.Null(Jurisdictions.IssuesTinToAll("FR")); // not read: unknown, never guessed
    }

    [Theory]
    [InlineData("2015-12-31", true, false)]
    [InlineData("2016-01-01", false, false)]
    [InlineData("2014-06-30", true, true)]
    [InlineData("2014-07-01", true, false)] // new for FATCA, preexisting for CRS
    [InlineData("2011-05-16", true, true)]
    [InlineData("2019-03-14", false, false)]
    public void Preexisting_status_is_computed_per_regime(string openedOn, bool crsPreexisting, bool fatcaPreexisting)
    {
        var date = DateOnly.Parse(openedOn);
        Assert.Equal(crsPreexisting, AccountStatus.IsCrsPreexisting(date));
        Assert.Equal(fatcaPreexisting, AccountStatus.IsFatcaPreexisting(date));
    }

    [Fact]
    public void Preexisting_status_needs_a_single_regime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AccountStatus.IsPreexisting(Regime.Both, new DateOnly(2010, 1, 1)));
    }

    [Fact]
    public void Usd_upper_bound_never_guesses_an_unknown_currency()
    {
        Assert.Equal(10_575m, AccountStatus.UsdUpperBound(42_300m, "RON"));
        Assert.Null(AccountStatus.UsdUpperBound(1_000m, "JPY"));
    }
}
