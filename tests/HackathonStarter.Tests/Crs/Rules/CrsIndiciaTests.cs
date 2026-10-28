using HackathonStarter.Features.Rules;
using static HackathonStarter.Tests.Crs.Rules.Subjects;

namespace HackathonStarter.Tests.Crs.Rules;

/// <summary>One hit test and one cleared test per CRS indicium (CPF Anexa 1, Sec. III.B pct. 12-13; guide p. 29).</summary>
public class CrsIndiciaTests
{
    private static readonly CrsRuleEngine Engine = new();
    private static readonly DateOnly Preexisting = new(2013, 5, 1);

    private static RuleOutcome Run(RuleSubject after, string kind = "AddressChanged") =>
        Engine.Evaluate(RoResident(), after, new RuleChange(kind));

    private static RuleHit HitOf(RuleOutcome o, string ruleId) => Assert.Single(o.Indicia, h => h.RuleId == ruleId);

    private static RuleHit ClearedOf(RuleOutcome o, string ruleId) => Assert.Single(o.Cleared, h => h.RuleId == ruleId);

    // lit. a)
    [Fact]
    public void Recorded_residence_in_a_reportable_jurisdiction_is_an_indicium()
    {
        var hit = HitOf(Run(RoResident().WithRecordedResidence("AT")), RuleIds.CrsResidence);
        Assert.Equal("AT", hit.Jurisdiction);
    }

    [Fact]
    public void Recorded_residence_in_a_non_reportable_jurisdiction_is_cleared()
    {
        var o = Run(RoResident().WithRecordedResidence("AE"));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu fac obiectul raportării", ClearedOf(o, RuleIds.CrsResidence).Explanation);
    }

    // lit. b)
    [Fact]
    public void Current_residence_in_a_reportable_jurisdiction_is_an_indicium()
    {
        var o = Run(RoResident().MovedTo("AT"));
        Assert.Equal(Decision.Clarification, o.Decision);
        Assert.Equal("Addresses/Residence", HitOf(o, RuleIds.CrsAddress).SourceField);
    }

    [Fact]
    public void Mailing_address_abroad_is_its_own_rule()
    {
        var o = Run(RoResident().WithAddress(SubjectAddressKind.Mailing, "FR"));
        Assert.Equal(("FR", "Addresses/Mailing"), (HitOf(o, RuleIds.CrsMailing).Jurisdiction, HitOf(o, RuleIds.CrsMailing).SourceField));
        Assert.DoesNotContain(o.Indicia, h => h.RuleId == RuleIds.CrsAddress);
    }

    [Fact]
    public void A_foreign_address_that_is_no_longer_current_is_not_an_indicium()
    {
        Assert.Equal(Decision.NoCase, Run(RoResident().WithAddress(SubjectAddressKind.Residence, "DE", current: false)).Decision);
    }

    [Fact]
    public void Address_in_a_non_reportable_jurisdiction_is_cleared_with_the_reason()
    {
        var o = Run(RoResident().MovedTo("AE"));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu fac obiectul raportării", ClearedOf(o, RuleIds.CrsAddress).Explanation);
    }

    [Fact]
    public void A_service_providers_address_is_ignored_when_there_are_others()
    {
        var o = Run(RoResident().WithAddress(SubjectAddressKind.Mailing, "DE", serviceProvider: true));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("prestator de servicii", ClearedOf(o, RuleIds.CrsMailing).Explanation);
    }

    [Fact]
    public void A_service_providers_address_counts_when_it_is_the_only_one()
    {
        var only = RoResident() with { Addresses = [new SubjectAddress(SubjectAddressKind.Mailing, "DE", IsServiceProvider: true)] };
        Assert.Equal(Decision.Clarification, Run(only).Decision);
    }

    // lit. c)
    [Fact]
    public void Foreign_phone_without_a_romanian_phone_is_an_indicium()
    {
        var o = Run(RoResident().ReplacePhone("+33612345678", "FR"), "PhoneChanged");
        Assert.Equal("FR", HitOf(o, RuleIds.CrsPhone).Jurisdiction);
    }

    [Fact]
    public void Foreign_phone_next_to_a_current_romanian_phone_is_cleared()
    {
        var o = Run(RoResident().WithPhone("+33612345678", "FR"), "PhoneAdded");
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Equal("FR", ClearedOf(o, RuleIds.CrsPhone).Jurisdiction);
    }

    [Fact]
    public void A_romanian_phone_that_is_no_longer_current_does_not_clear_the_foreign_one()
    {
        var after = RoResident() with
        {
            Phones = [new SubjectPhone("+40721000000", "RO", IsCurrent: false), new SubjectPhone("+33612345678", "FR")],
        };
        Assert.Equal(Decision.Clarification, Run(after, "PhoneChanged").Decision);
    }

    [Fact]
    public void Not_counting_an_old_romanian_phone_is_labelled_interpretation()
    {
        var preexisting = RoResident(openedOn: Preexisting);
        var replaced = HitOf(Run(preexisting.ReplacePhone("+33612345678", "FR"), "PhoneChanged"), RuleIds.CrsPhone);
        Assert.Equal(RuleBasis.Interpretation, replaced.Basis);
        Assert.Contains("nu mai e actual", replaced.Explanation);

        var neverRomanian = HitOf(Run(preexisting.WithoutPhones().WithPhone("+33612345678", "FR"), "PhoneAdded"), RuleIds.CrsPhone);
        Assert.Equal(RuleBasis.Verified, neverRomanian.Basis);
    }

    [Fact]
    public void A_service_providers_phone_is_ignored_when_there_are_other_numbers()
    {
        var o = Run(RoResident().WithPhone("+4930123456", "DE", serviceProvider: true), "PhoneAdded");
        Assert.Contains("prestator de servicii", ClearedOf(o, RuleIds.CrsPhone).Explanation);
    }

    // lit. d)
    [Theory]
    [InlineData(SubjectAccountType.Custodial)]
    [InlineData(SubjectAccountType.Insurance)]
    [InlineData(SubjectAccountType.Other)]
    public void Standing_order_from_a_non_deposit_account_is_an_indicium(SubjectAccountType type)
    {
        var after = RoResident().WithAccounts(Account(type, Preexisting).WithOrder("DE02700100800918273645", "DE"));
        Assert.Equal(RuleBasis.Verified, HitOf(Run(after, "StandingOrderCreated"), RuleIds.CrsStandingOrder).Basis);
    }

    [Fact]
    public void Standing_order_from_a_deposit_account_is_cleared()
    {
        var after = RoResident().WithAccounts(Account(SubjectAccountType.Depository, Preexisting).WithOrder("DE02700100800918273645", "DE"));
        var o = Run(after, "StandingOrderCreated");
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("exceptat la CRS", ClearedOf(o, RuleIds.CrsStandingOrder).Explanation);
    }

    [Fact]
    public void An_inactive_standing_order_is_cleared()
    {
        var after = RoResident().WithAccounts(
            Account(SubjectAccountType.Custodial, Preexisting).WithOrder("DE02700100800918273645", "DE", active: false));
        var o = Run(after, "StandingOrderCreated");
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu mai e activ", ClearedOf(o, RuleIds.CrsStandingOrder).Explanation);
    }

    [Fact]
    public void One_custodial_and_one_deposit_order_to_the_same_country_give_only_the_hit()
    {
        var after = RoResident().WithAccounts(
            Account(SubjectAccountType.Depository, Preexisting).WithOrder("DE02700100800918273645", "DE"),
            Account(SubjectAccountType.Custodial, Preexisting, "RO88RZBR0000069004471852").WithOrder("DE02700100800918273645", "DE"));
        var o = Run(after, "StandingOrderCreated");
        Assert.Single(o.Indicia);
        Assert.DoesNotContain(o.Cleared, h => h.RuleId == RuleIds.CrsStandingOrder);
    }

    // lit. e)
    [Fact]
    public void Effective_power_of_attorney_abroad_is_an_indicium()
    {
        var o = Run(RoResident().WithPowerOfAttorney("Persoană Sintetică", "AT"), "PowerOfAttorneyGranted");
        Assert.Equal("PowersOfAttorney", HitOf(o, RuleIds.CrsPowerOfAttorney).SourceField);
    }

    [Fact]
    public void Expired_power_of_attorney_is_cleared()
    {
        var o = Run(RoResident().WithPowerOfAttorney("Persoană Sintetică", "AT", active: false), "PowerOfAttorneyGranted");
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu mai e valabilă", ClearedOf(o, RuleIds.CrsPowerOfAttorney).Explanation);
    }

    // lit. f)
    [Theory]
    [InlineData(SubjectAddressKind.InCareOf)]
    [InlineData(SubjectAddressKind.HoldMail)]
    public void In_care_of_or_hold_mail_as_the_only_address_is_an_indicium(SubjectAddressKind kind)
    {
        var only = RoResident().WithAccounts(Account(SubjectAccountType.Depository, Preexisting)) with
        {
            Addresses = [new SubjectAddress(kind, "IT")],
        };
        var hit = HitOf(Run(only), RuleIds.CrsInCareOf);
        Assert.Equal(RuleBasis.Verified, hit.Basis); // preexisting account, truly no other address
        Assert.Contains("cont nedocumentat", hit.Explanation);
    }

    [Fact]
    public void In_care_of_next_to_another_current_address_is_cleared()
    {
        var o = Run(RoResident().WithAddress(SubjectAddressKind.InCareOf, "IT"));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("altă adresă actuală", ClearedOf(o, RuleIds.CrsInCareOf).Explanation);
    }

    [Fact]
    public void In_care_of_is_not_a_residence_or_mailing_indicium()
    {
        var o = Run(RoResident().WithOnlyAddress(SubjectAddressKind.InCareOf, "IT"));
        Assert.DoesNotContain(o.Indicia, h => h.RuleId is RuleIds.CrsAddress or RuleIds.CrsMailing);
    }

    // Romania and the US
    [Fact]
    public void Romanian_data_never_produces_an_indicium()
    {
        var after = RoResident().MovedTo("RO").WithAddress(SubjectAddressKind.Mailing, "RO").WithPhone("+40733000000", "RO")
            .WithPowerOfAttorney("Persoană Sintetică", "RO")
            .WithAccounts(Account(SubjectAccountType.Custodial, Preexisting).WithOrder("RO49AAAA1B31007593840000", "RO"));
        var o = Run(after);
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Equal(RuleIds.Domestic, Assert.Single(o.Cleared).RuleId);
    }

    [Fact]
    public void The_us_is_never_a_crs_indicium()
    {
        var after = RoResident().MovedTo("US").ReplacePhone("+13125550147", "US").WithPowerOfAttorney("Persoană Sintetică", "US");
        var o = Run(after);
        Assert.DoesNotContain(o.Indicia, h => h.Regime == Regime.Crs);
        Assert.All(o.Cleared.Where(c => c.Regime == Regime.Crs), c => Assert.Contains("SUA", c.Explanation));
    }

    // Coverage and decision
    [Fact]
    public void A_self_certification_declaring_the_jurisdiction_covers_the_indicium_and_makes_it_reportable()
    {
        var after = RoResident().MovedTo("AT").WithSelfCert(SelfCert(ChangeAt, new SubjectTaxResidence("AT", "123456789")));
        var o = Run(after);
        Assert.Equal(Decision.Compliant, o.Decision);
        Assert.Equal(Coverage.SelfCertification, HitOf(o, RuleIds.CrsAddress).CoveredBy);
        Assert.Equal(["AT"], o.ReportableFor);
    }

    [Fact]
    public void A_self_certification_that_is_not_valid_covers_nothing()
    {
        var doubtful = SelfCert(ChangeAt, new SubjectTaxResidence("AT", "123456789")) with { IsValid = false };
        Assert.Equal(Decision.Clarification, Run(RoResident().MovedTo("AT").WithSelfCert(doubtful)).Decision);
    }

    [Fact]
    public void An_explanation_for_another_jurisdiction_covers_nothing()
    {
        Assert.Equal(Decision.Clarification, Run(RoResident().MovedTo("AT").WithExplanation("DE", "Student")).Decision);
    }

    [Fact]
    public void Indicia_on_a_new_account_are_labelled_interpretation()
    {
        var hit = HitOf(Run(RoResident(openedOn: new DateOnly(2020, 1, 1)).MovedTo("AT")), RuleIds.CrsAddress);
        Assert.Equal(RuleBasis.Interpretation, hit.Basis);
        Assert.Contains("GEN-NEW-ACCOUNT", hit.Explanation);
    }

    [Fact]
    public void Crs_and_fatca_uncovered_together_give_a_fatca_alert_with_both_regimes()
    {
        var o = Run(RoResident().MovedTo("DE").WithoutPhones().WithPhone("+13125550147", "US"));
        Assert.Equal(Decision.FatcaAlert, o.Decision);
        Assert.Equal(Regime.Both, o.CaseRegime);
        Assert.Contains("Clarificare CRS", o.Explanation);
    }
}
