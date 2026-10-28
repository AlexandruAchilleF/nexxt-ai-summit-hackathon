using HackathonStarter.Features.Rules;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using HackathonStarter.Features.Tin;
using static HackathonStarter.Tests.Crs.Rules.Subjects;

namespace HackathonStarter.Tests.Crs.Rules;

/// <summary>Golden cases, engine layer (docs/GOLDEN-CASES.md): hits compared exactly, Cleared as a superset.</summary>
public class GoldenEngineTests
{
    private static readonly CrsRuleEngine Engine = new(new FakeTinFormat());

    [Theory]
    [MemberData(nameof(EngineCases.Ids), MemberType = typeof(EngineCases))]
    public void Golden_case(string id)
    {
        var c = EngineCases.Get(id);
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        Assert.Equal(c.Expected, outcome.Decision);
        Assert.Equal(
            c.Hits.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)).Order(),
            outcome.Indicia.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)).Order());
        foreach (var expected in c.Hits.Where(h => h.SourceField is not null))
            Assert.Equal(expected.SourceField, outcome.Indicia.Single(h => h.RuleId == expected.RuleId && h.Jurisdiction == expected.Jurisdiction).SourceField);
        foreach (var (ruleId, jurisdiction) in c.ClearedAtLeast)
            Assert.Contains(outcome.Cleared, x => x.RuleId == ruleId && x.Jurisdiction == jurisdiction);
        if (c.ReportableFor is not null) Assert.Equal(c.ReportableFor, outcome.ReportableFor);
        Assert.Equal(c.Regime, outcome.Regime);
        var said = string.Join(" ", outcome.Cleared.Select(x => x.ExplanationRo).Prepend(outcome.ExplanationRo));
        foreach (var phrase in c.Says ?? []) Assert.Contains(phrase, said);

        Assert.Equal(CrsRuleEngine.Version, outcome.EngineVersion);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ExplanationRo));
        Assert.False(string.IsNullOrWhiteSpace(outcome.Explanation));
        Assert.All(outcome.Indicia.Concat(outcome.Cleared), h =>
        {
            Assert.True(RuleCatalog.Contains(h.RuleId), h.RuleId);
            Assert.Equal(RuleCatalog.Get(h.RuleId).GuidePage, h.GuidePage);
            Assert.False(string.IsNullOrWhiteSpace(h.ExplanationRo), h.RuleId);
            Assert.False(string.IsNullOrWhiteSpace(h.Explanation), h.RuleId);
        });
    }

    [Fact]
    public void Maria_opens_a_crs_case_with_both_indicia_on_the_new_account()
    {
        var outcome = Engine.Evaluate(EngineCases.MariaBefore, EngineCases.MariaAfter, new RuleChange("AddressChanged"));

        Assert.Equal(Regime.Crs, outcome.Regime);
        Assert.Equal(2, outcome.Uncovered.Count());
        Assert.Contains(outcome.Indicia, h => h.RuleId == RuleIds.CrsAddress && h.SourceField == "Addresses/Residence");
        Assert.Contains(outcome.Indicia, h => h.RuleId == RuleIds.CrsPhone && h.SourceField == "Phones");
        // New account: the indicia are our "reason to know" reading, and the explanation says so.
        Assert.All(outcome.Indicia, h => Assert.Equal(RuleBasis.Interpretation, h.Basis));
        Assert.All(outcome.Indicia, h => Assert.Contains("GEN-NEW-ACCOUNT", h.ExplanationRo));
        Assert.Contains("Cont nou", outcome.ExplanationRo);
        Assert.Empty(outcome.ReportableFor);
    }

    [Fact]
    public void Maria_after_response_is_reportable_for_germany()
    {
        var outcome = Engine.Evaluate(EngineCases.MariaAfter, EngineCases.MariaAfterResponse, new RuleChange("SelfCertSubmitted"));

        Assert.Equal(Decision.Compliant, outcome.Decision);
        Assert.Null(outcome.Regime);
        Assert.Contains("SC-CONSISTENT", outcome.ExplanationRo);
        Assert.All(outcome.Indicia, h => Assert.Contains("exista și înainte", h.ExplanationRo));
    }

    [Fact]
    public void Maria_response_form_has_every_validity_element_and_a_format_valid_german_tin()
    {
        var outcome = Engine.EvaluateSelfCertification(EngineCases.MariaAfter, EngineCases.MariaResponseForm);

        Assert.Equal(Decision.NoCase, outcome.Decision);
        Assert.Empty(outcome.Indicia);
        Assert.Equal(RuleBasis.Verified, Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.SelfCertValid).Basis);
        Assert.Contains("format valid", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.TinFormat).ExplanationRo);
        Assert.Equal(["DE"], outcome.ReportableFor);
    }

    [Fact]
    public void Standing_order_from_a_deposit_account_is_cleared_with_the_reason()
    {
        var outcome = Engine.Evaluate(EngineCases.Get("5a").Before, EngineCases.Get("5a").After, new RuleChange("StandingOrderCreated"));
        var cleared = Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.CrsStandingOrder);

        Assert.Contains("cont de depozit", cleared.ExplanationRo);
        Assert.Contains("depository account", cleared.Explanation);
        Assert.Equal("StandingOrders", cleared.SourceField);
    }

    [Fact]
    public void Us_standing_order_from_a_deposit_account_is_fatca_but_not_crs()
    {
        var c = EngineCases.Get("5c");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        Assert.Equal(Regime.Fatca, outcome.Regime);
        Assert.Contains("excepția CRS", Assert.Single(outcome.Indicia).ExplanationRo);
        Assert.Contains("SUA nu e jurisdicție CRS", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.CrsStandingOrder).ExplanationRo);
    }

    [Fact]
    public void Andrei_german_phone_is_cleared_because_of_the_romanian_phone()
    {
        var c = EngineCases.Get("13");
        var cleared = Assert.Single(Engine.Evaluate(c.Before, c.After, c.Change).Cleared, x => x.RuleId == RuleIds.CrsPhone);
        Assert.Contains("telefon actual în România", cleared.ExplanationRo);
    }

    [Fact]
    public void Cluj_to_iasi_explains_the_new_address_is_in_romania()
    {
        var c = EngineCases.Get("15");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        var domestic = Assert.Single(outcome.Cleared);
        Assert.Equal(RuleIds.Domestic, domestic.RuleId);
        Assert.Contains("Noua adresă e în România", domestic.ExplanationRo);
        Assert.Equal(RuleBasis.Interpretation, domestic.Basis);
    }

    [Fact]
    public void Preexisting_account_indicia_are_verified_law_not_interpretation()
    {
        var c = EngineCases.Get("17b");
        var hit = Assert.Single(Engine.Evaluate(c.Before, c.After, c.Change).Indicia);
        Assert.Equal(RuleBasis.Verified, hit.Basis);
        Assert.DoesNotContain("GEN-NEW-ACCOUNT", hit.ExplanationRo);
    }

    [Fact]
    public void Standing_order_from_a_preexisting_custodial_account_is_verified_law()
    {
        var custodial = Account(AccountType.Custodial, new DateOnly(2014, 3, 1), "RO88RZBR0000069004471852");
        var before = RoResident().WithAccounts(custodial);
        var after = RoResident().WithAccounts(custodial.WithOrder("DE02700100800918273645", "DE"));

        var hit = Assert.Single(Engine.Evaluate(before, after, new RuleChange("StandingOrderCreated")).Indicia);
        Assert.Equal((RuleIds.CrsStandingOrder, RuleBasis.Verified), (hit.RuleId, hit.Basis));
    }

    [Fact]
    public void Petru_in_care_of_explains_the_undocumented_account_route_and_the_lower_value_trap()
    {
        var c = EngineCases.Get("12");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        var hit = Assert.Single(outcome.Indicia);
        Assert.Contains("cont nedocumentat", hit.ExplanationRo);
        Assert.Equal(RuleBasis.Interpretation, hit.Basis); // the old Cluj address is on file, but not current (Q6)
        Assert.Contains("valoare mică", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.FatcaInCareOf).ExplanationRo);
    }

    [Fact]
    public void Student_explanation_covers_the_indicium_without_making_germany_reportable()
    {
        var c = EngineCases.Get("18-explained");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        Assert.Empty(outcome.ReportableFor);
        Assert.Contains("Student", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.Explanation).ExplanationRo);
    }
}

/// <summary>R3's ITinFormat stand-in for the golden cases: Italian codice fiscale length (16), German IdNr (11 digits).</summary>
internal sealed class FakeTinFormat : ITinFormat
{
    public TinCheck Check(string country, string tin) => country switch
    {
        "IT" => new(country, tin, tin.Length == 16, TinCheckSource.LocalRules, ""),
        "DE" => new(country, tin, tin.Length == 11 && tin.All(char.IsDigit), TinCheckSource.LocalRules, ""),
        _ => new(country, tin, false, TinCheckSource.NotChecked, ""),
    };

    public bool? IssuesTinToAllResidents(string country) => null;
}
