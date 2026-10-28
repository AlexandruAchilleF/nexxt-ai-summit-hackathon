using HackathonStarter.Features.Rules;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using AddressKind = HackathonStarter.Features.Clients.AddressKind;
using static HackathonStarter.Tests.Crs.RuleEngine.Subjects;

namespace HackathonStarter.Tests.Crs.RuleEngine;

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
        Assert.Contains("nu fac obiectul raportării", ClearedOf(o, RuleIds.CrsResidence).ExplanationRo);
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
        var o = Run(RoResident().WithAddress(AddressKind.Mailing, "FR"));
        Assert.Equal(("FR", "Addresses/Mailing"), (HitOf(o, RuleIds.CrsMailing).Jurisdiction, HitOf(o, RuleIds.CrsMailing).SourceField));
        Assert.DoesNotContain(o.Indicia, h => h.RuleId == RuleIds.CrsAddress);
    }

    [Fact]
    public void A_foreign_address_that_is_no_longer_current_is_not_an_indicium()
    {
        Assert.Equal(Decision.NoCase, Run(RoResident().WithAddress(AddressKind.Residence, "DE", current: false)).Decision);
    }

    [Fact]
    public void Address_in_a_non_reportable_jurisdiction_is_cleared_with_the_reason()
    {
        var o = Run(RoResident().MovedTo("AE"));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu fac obiectul raportării", ClearedOf(o, RuleIds.CrsAddress).ExplanationRo);
    }

    [Fact]
    public void A_service_providers_address_is_ignored_when_there_are_others()
    {
        var o = Run(RoResident().WithAddress(AddressKind.Mailing, "DE", serviceProvider: true));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("prestator de servicii", ClearedOf(o, RuleIds.CrsMailing).ExplanationRo);
    }

    [Fact]
    public void A_service_providers_address_counts_when_it_is_the_only_one()
    {
        var only = RoResident() with { Addresses = [new SubjectAddress(AddressKind.Mailing, "DE", IsServiceProvider: true)] };
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
        Assert.Contains("nu mai e actual", replaced.ExplanationRo);

        var neverRomanian = HitOf(Run(preexisting.WithoutPhones().WithPhone("+33612345678", "FR"), "PhoneAdded"), RuleIds.CrsPhone);
        Assert.Equal(RuleBasis.Verified, neverRomanian.Basis);
    }

    [Fact]
    public void A_service_providers_phone_is_ignored_when_there_are_other_numbers()
    {
        var o = Run(RoResident().WithPhone("+4930123456", "DE", serviceProvider: true), "PhoneAdded");
        Assert.Contains("prestator de servicii", ClearedOf(o, RuleIds.CrsPhone).ExplanationRo);
    }

    // lit. d)
    [Theory]
    [InlineData(AccountType.Custodial)]
    [InlineData(AccountType.Insurance)]
    [InlineData(AccountType.Other)]
    public void Standing_order_from_a_non_deposit_account_is_an_indicium(AccountType type)
    {
        var after = RoResident().WithAccounts(Account(type, Preexisting).WithOrder("DE02700100800918273645", "DE"));
        Assert.Equal(RuleBasis.Verified, HitOf(Run(after, "StandingOrderCreated"), RuleIds.CrsStandingOrder).Basis);
    }

    [Fact]
    public void Standing_order_from_a_deposit_account_is_cleared()
    {
        var after = RoResident().WithAccounts(Account(AccountType.Depository, Preexisting).WithOrder("DE02700100800918273645", "DE"));
        var o = Run(after, "StandingOrderCreated");
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("exceptat la CRS", ClearedOf(o, RuleIds.CrsStandingOrder).ExplanationRo);
    }

    [Fact]
    public void An_inactive_standing_order_is_cleared()
    {
        var after = RoResident().WithAccounts(
            Account(AccountType.Custodial, Preexisting).WithOrder("DE02700100800918273645", "DE", active: false));
        var o = Run(after, "StandingOrderCreated");
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu mai e activ", ClearedOf(o, RuleIds.CrsStandingOrder).ExplanationRo);
    }

    [Fact]
    public void One_custodial_and_one_deposit_order_to_the_same_country_give_only_the_hit()
    {
        var after = RoResident().WithAccounts(
            Account(AccountType.Depository, Preexisting).WithOrder("DE02700100800918273645", "DE"),
            Account(AccountType.Custodial, Preexisting, "RO88RZBR0000069004471852").WithOrder("DE02700100800918273645", "DE"));
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
        Assert.Contains("nu mai e valabilă", ClearedOf(o, RuleIds.CrsPowerOfAttorney).ExplanationRo);
    }

    // lit. f)
    [Theory]
    [InlineData(AddressKind.InCareOf)]
    [InlineData(AddressKind.HoldMail)]
    public void In_care_of_or_hold_mail_as_the_only_address_is_an_indicium(AddressKind kind)
    {
        var only = RoResident().WithAccounts(Account(AccountType.Depository, Preexisting)) with
        {
            Addresses = [new SubjectAddress(kind, "IT")],
        };
        var hit = HitOf(Run(only), RuleIds.CrsInCareOf);
        Assert.Equal(RuleBasis.Verified, hit.Basis); // preexisting account, truly no other address
        Assert.Contains("cont nedocumentat", hit.ExplanationRo);
    }

    // lit. f) does not apply next to another address (p. 29), but the c/o address is still a current mailing address in a
    // reportable jurisdiction (lit. b; GOLDEN-CASES Î10), so a case opens on the usual route, not the c/o one (p. 31).
    [Fact]
    public void In_care_of_next_to_another_current_address_is_not_lit_f_but_is_a_mailing_indicium()
    {
        var o = Run(RoResident().WithAddress(AddressKind.InCareOf, "IT"));
        Assert.Equal(Decision.Clarification, o.Decision);
        Assert.Contains("altă adresă actuală", ClearedOf(o, RuleIds.CrsInCareOf).ExplanationRo);
        var mail = HitOf(o, RuleIds.CrsMailing);
        Assert.Equal(("IT", "Addresses/InCareOf"), (mail.Jurisdiction, mail.SourceField));
        Assert.DoesNotContain("aceeași adresă ca la CRS-IND-CO", mail.ExplanationRo);
        Assert.DoesNotContain("cont nedocumentat", o.ExplanationRo);
    }

    // Î10: c/o is not a residence (p. 48) but is a mailing address (p. 29 lit. b): our reading, so Interpretation.
    [Fact]
    public void In_care_of_is_a_mailing_indicium_but_never_a_residence_indicium()
    {
        var o = Run(RoResident().WithOnlyAddress(AddressKind.InCareOf, "IT"));
        Assert.DoesNotContain(o.Indicia, h => h.RuleId == RuleIds.CrsAddress);
        var mail = HitOf(o, RuleIds.CrsMailing);
        Assert.Equal(("IT", "Addresses/InCareOf", RuleBasis.Interpretation), (mail.Jurisdiction, mail.SourceField, mail.Basis));
        Assert.Contains("Î10", mail.ExplanationRo);
        Assert.Contains("aceeași adresă ca la CRS-IND-CO", mail.ExplanationRo);
        Assert.Contains("same address as CRS-IND-CO", mail.Explanation);
    }

    [Fact]
    public void The_c_o_mailing_hit_names_the_c_o_route_only_on_a_preexisting_account()
    {
        var preexisting = HitOf(Run(RoResident().WithAccounts(Account(AccountType.Depository, Preexisting))
            .WithOnlyAddress(AddressKind.InCareOf, "IT")), RuleIds.CrsMailing);
        Assert.Contains("Fără alte indicii", preexisting.ExplanationRo);
        Assert.Contains("pct. 17", preexisting.ExplanationRo);

        var newAccount = HitOf(Run(RoResident().WithOnlyAddress(AddressKind.InCareOf, "IT")), RuleIds.CrsMailing);
        Assert.DoesNotContain("pct. 17", newAccount.ExplanationRo);
        Assert.Contains("GEN-NEW-ACCOUNT", newAccount.ExplanationRo);
    }

    [Fact]
    public void In_care_of_outside_the_reportable_jurisdictions_is_cleared_as_mailing_too()
    {
        var o = Run(RoResident().WithAddress(AddressKind.InCareOf, "AE"));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu fac obiectul raportării", ClearedOf(o, RuleIds.CrsMailing).ExplanationRo);
    }

    // Î10 decides c/o only; a hold-mail instruction stays a lit. f) matter.
    [Fact]
    public void Hold_mail_is_not_a_mailing_indicium()
    {
        var o = Run(RoResident().WithOnlyAddress(AddressKind.HoldMail, "IT"));
        HitOf(o, RuleIds.CrsInCareOf);
        Assert.DoesNotContain(o.Indicia, h => h.RuleId == RuleIds.CrsMailing);
    }

    // p. 31: the c/o route (paper search, else undocumented) holds while the c/o address is the only indicium; its own
    // CRS-IND-MAIL hit is the same address, but any other indicium switches to the usual preexisting route (pct. 19).
    [Fact]
    public void In_care_of_route_holds_with_its_own_mailing_hit_and_not_with_another_indicium()
    {
        var preexisting = RoResident().WithAccounts(Account(AccountType.Depository, Preexisting));
        var alone = Run(preexisting.WithOnlyAddress(AddressKind.InCareOf, "IT"));
        Assert.Equal(2, alone.Uncovered.Count());
        Assert.Contains("cont nedocumentat", alone.ExplanationRo);
        Assert.Contains("undocumented account", alone.Explanation);

        var withPhone = Run(preexisting.WithOnlyAddress(AddressKind.InCareOf, "IT").ReplacePhone("+4915144201187", "DE"));
        Assert.Equal(3, withPhone.Uncovered.Count());
        Assert.DoesNotContain("cont nedocumentat", withPhone.ExplanationRo);
        Assert.Contains("Sec. III.B pct. 19", withPhone.ExplanationRo);
    }

    [Fact]
    public void A_move_to_the_united_kingdom_written_uk_is_an_indicium_for_gb()
    {
        var o = Run(RoResident().MovedTo("UK", "221B Baker Street, London"));
        Assert.Equal(Decision.Clarification, o.Decision);
        Assert.Equal("GB", HitOf(o, RuleIds.CrsAddress).Jurisdiction);
    }

    // Romania and the US
    [Fact]
    public void Romanian_data_never_produces_an_indicium()
    {
        var after = RoResident().MovedTo("RO").WithAddress(AddressKind.Mailing, "RO").WithPhone("+40733000000", "RO")
            .WithPowerOfAttorney("Persoană Sintetică", "RO")
            .WithAccounts(Account(AccountType.Custodial, Preexisting).WithOrder("RO49AAAA1B31007593840000", "RO"));
        var o = Run(after);
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Equal(RuleIds.Domestic, Assert.Single(o.Cleared).RuleId);
    }

    [Fact]
    public void Without_a_guide_exception_a_us_phone_or_order_is_cleared_only_as_not_a_crs_jurisdiction()
    {
        // No Romanian phone, a custodial account: the only reason is that the US is not CRS, which the repo's sources do
        // not contain (the ANAF lists): Interpretation. With the exception, the reason is the guide's (Î12).
        var phone = ClearedOf(Run(RoResident().ReplacePhone("+13125550147", "US"), "PhoneChanged"), RuleIds.CrsPhone);
        Assert.Equal(RuleBasis.Interpretation, phone.Basis);
        Assert.DoesNotContain("pct. 12 lit. c", phone.ExplanationRo);

        var custodial = Account(AccountType.Custodial, Preexisting).WithOrder("073909523/4417209358", "US");
        var order = ClearedOf(Run(RoResident().WithAccounts(custodial), "StandingOrderCreated"), RuleIds.CrsStandingOrder);
        Assert.Equal(RuleBasis.Interpretation, order.Basis);
        Assert.DoesNotContain("exceptat", order.ExplanationRo);
    }

    [Fact]
    public void A_non_reportable_phone_next_to_a_romanian_phone_is_cleared_on_the_romanian_phone_first()
    {
        var cleared = ClearedOf(Run(RoResident().WithPhone("+971501234567", "AE"), "PhoneAdded"), RuleIds.CrsPhone);
        Assert.Equal(RuleBasis.Verified, cleared.Basis);
        Assert.StartsWith("Telefon din Emiratele Arabe Unite (AE), dar există și un telefon actual în România", cleared.ExplanationRo);
        Assert.Contains("în plus, AE e pe lista ANAF", cleared.ExplanationRo);
    }

    [Fact]
    public void The_us_is_never_a_crs_indicium()
    {
        var after = RoResident().MovedTo("US").ReplacePhone("+13125550147", "US").WithPowerOfAttorney("Persoană Sintetică", "US");
        var o = Run(after);
        Assert.DoesNotContain(o.Indicia, h => h.Regime == Regime.Crs);
        Assert.All(o.Cleared.Where(c => c.Regime == Regime.Crs), c => Assert.Contains("SUA", c.ExplanationRo));
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
        Assert.Contains("GEN-NEW-ACCOUNT", hit.ExplanationRo);
    }

    [Fact]
    public void Crs_and_fatca_uncovered_together_give_a_fatca_alert_with_both_regimes()
    {
        var o = Run(RoResident().MovedTo("DE").WithoutPhones().WithPhone("+13125550147", "US"));
        Assert.Equal(Decision.FatcaAlert, o.Decision);
        Assert.Equal(Regime.Both, o.Regime);
        Assert.Contains("Clarificare CRS", o.ExplanationRo);
    }
}
