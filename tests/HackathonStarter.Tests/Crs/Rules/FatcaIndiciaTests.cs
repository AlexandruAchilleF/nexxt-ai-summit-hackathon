using HackathonStarter.Features.Rules;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using AddressKind = HackathonStarter.Features.Clients.AddressKind;
using static HackathonStarter.Tests.Crs.RuleEngine.Subjects;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>The US indicia (IGA Anexa I, Sec. II.B.1; guide p. 31) and the CRS exceptions FATCA must not inherit.</summary>
public class FatcaIndiciaTests
{
    private static readonly CrsRuleEngine Engine = new();
    private static readonly DateOnly FatcaPreexisting = new(2012, 1, 10);

    private static RuleOutcome Run(RuleSubject after) => Engine.Evaluate(RoResident(), after, new RuleChange("DocumentUploaded"));

    private static RuleHit HitOf(RuleOutcome o, string ruleId)
    {
        var hit = Assert.Single(o.Indicia, h => h.RuleId == ruleId);
        Assert.Equal(("US", Regime.Fatca), (hit.Jurisdiction, hit.Regime));
        return hit;
    }

    [Fact]
    public void Us_citizenship_is_an_indicium()
    {
        var o = Run(RoResident().WithCitizenships("RO", "US"));
        Assert.Equal(Decision.FatcaAlert, o.Decision);
        HitOf(o, RuleIds.FatcaCitizenship);
        Assert.DoesNotContain(o.Indicia, h => h.RuleId == RuleIds.FatcaBirthPlace);
    }

    // Golden case 7: with FATCA in play (another US indicium hit), the place of birth is listed as read and not applied.
    [Fact]
    public void Place_of_birth_outside_the_us_is_cleared_when_another_us_indicium_hit()
    {
        var o = Run(RoResident().WithCitizenships("RO", "US"));
        var pob = Assert.Single(o.Cleared, c => c.RuleId == RuleIds.FatcaBirthPlace);
        Assert.Equal(("US", Regime.Fatca, "BirthCountry", "31"), (pob.Jurisdiction, pob.Regime, pob.SourceField, pob.GuidePage));
        Assert.Contains("România (RO), nu SUA", pob.ExplanationRo);
        Assert.Contains("Romania (RO), not the US", pob.Explanation);
    }

    // The narrow condition: no US indicium, no entry, so a change that stays in Romania ends in GEN-DOMESTIC (cases 1, 8, 15).
    [Fact]
    public void Place_of_birth_is_not_listed_without_a_us_indicium()
    {
        var o = Run(RoResident().ReplacePhone("+40733902614", "RO"));
        Assert.DoesNotContain(o.Cleared, c => c.RuleId == RuleIds.FatcaBirthPlace);
        Assert.Equal(RuleIds.Domestic, Assert.Single(o.Cleared).RuleId);
    }

    [Fact]
    public void An_unknown_place_of_birth_is_not_listed_as_cleared()
    {
        var o = Run(RoResident().BornIn(null).WithCitizenships("RO", "US"));
        Assert.DoesNotContain(o.Cleared, c => c.RuleId == RuleIds.FatcaBirthPlace);
        Assert.DoesNotContain(o.Indicia, c => c.RuleId == RuleIds.FatcaBirthPlace);
    }

    [Fact]
    public void Us_tax_residence_on_file_is_an_indicium()
    {
        Assert.Equal("RecordedTaxResidences", HitOf(Run(RoResident().WithRecordedResidence("US")), RuleIds.FatcaCitizenship).SourceField);
    }

    [Fact]
    public void Us_place_of_birth_is_an_indicium()
    {
        HitOf(Run(RoResident().BornIn("US")), RuleIds.FatcaBirthPlace);
    }

    [Theory]
    [InlineData("RO")]
    [InlineData("CA")] // +1 is shared by the NANP: the engine reads the country, never the prefix
    [InlineData(null)]
    public void Other_places_of_birth_are_not_indicia(string? country)
    {
        Assert.Equal(Decision.NoCase, Run(RoResident().BornIn(country)).Decision);
    }

    [Theory]
    [InlineData(AddressKind.Residence)]
    [InlineData(AddressKind.Mailing)]
    public void Current_us_address_is_an_indicium(AddressKind kind)
    {
        HitOf(Run(RoResident().WithAddress(kind, "US")), RuleIds.FatcaAddress);
    }

    [Fact]
    public void Us_phone_is_an_indicium_even_next_to_a_romanian_phone()
    {
        var o = Run(RoResident().WithPhone("+13125550147", "US"));
        Assert.Contains("excepția CRS nu se aplică", HitOf(o, RuleIds.FatcaPhone).ExplanationRo);
    }

    [Fact]
    public void Us_phone_that_is_not_current_is_not_an_indicium()
    {
        Assert.Equal(Decision.NoCase, Run(RoResident().WithPhone("+13125550147", "US", current: false)).Decision);
    }

    [Fact]
    public void Us_standing_order_from_a_deposit_account_is_an_indicium()
    {
        var after = RoResident().WithAccounts(Account(AccountType.Depository, FatcaPreexisting).WithOrder("073909523/4417209358", "US"));
        var hit = HitOf(Run(after), RuleIds.FatcaStandingOrder);
        Assert.Equal(RuleBasis.Verified, hit.Basis);
        Assert.Contains("nu există la FATCA", hit.ExplanationRo);
    }

    [Fact]
    public void Inactive_us_standing_order_is_not_an_indicium()
    {
        var after = RoResident().WithAccounts(
            Account(AccountType.Depository, FatcaPreexisting).WithOrder("073909523/4417209358", "US", active: false));
        Assert.DoesNotContain(Run(after).Indicia, h => h.RuleId == RuleIds.FatcaStandingOrder);
    }

    [Fact]
    public void Power_of_attorney_to_a_person_with_a_us_address_is_an_indicium()
    {
        HitOf(Run(RoResident().WithPowerOfAttorney("Persoană Sintetică", "US")), RuleIds.FatcaPowerOfAttorney);
        Assert.Equal(Decision.NoCase, Run(RoResident().WithPowerOfAttorney("Persoană Sintetică", "US", active: false)).Decision);
    }

    [Fact]
    public void Only_address_in_care_of_on_a_new_account_is_an_indicium_in_any_country()
    {
        var o = Run(RoResident().WithOnlyAddress(AddressKind.InCareOf, "IT"));
        Assert.Equal(RuleBasis.Interpretation, HitOf(o, RuleIds.FatcaInCareOf).Basis);
        Assert.Equal(Decision.FatcaAlert, o.Decision);
    }

    [Theory]
    [InlineData(AddressKind.InCareOf, "IT")]
    [InlineData(AddressKind.HoldMail, "IT")]
    [InlineData(AddressKind.HoldMail, "US")]
    public void Lower_value_preexisting_account_clears_non_us_in_care_of_and_hold_mail(AddressKind kind, string country)
    {
        var after = RoResident().WithAccounts(Account(AccountType.Depository, FatcaPreexisting, balance: 42_300m))
            .WithOnlyAddress(kind, country);
        var o = Run(after);
        Assert.DoesNotContain(o.Indicia, h => h.RuleId == RuleIds.FatcaInCareOf);
        Assert.Contains("valoare mică", Assert.Single(o.Cleared, c => c.RuleId == RuleIds.FatcaInCareOf).ExplanationRo);
    }

    [Fact]
    public void Lower_value_preexisting_account_with_a_us_in_care_of_address_is_still_an_indicium()
    {
        var after = RoResident().WithAccounts(Account(AccountType.Depository, FatcaPreexisting, balance: 42_300m))
            .WithOnlyAddress(AddressKind.InCareOf, "US");
        // The old Romanian address is still on file, not current: "sole address" read as current only (Q6), our reading.
        var hit = HitOf(Run(after), RuleIds.FatcaInCareOf);
        Assert.Equal(RuleBasis.Interpretation, hit.Basis);
        Assert.Contains("interpretare de design", hit.ExplanationRo);
        Assert.DoesNotContain("Cont nou FATCA", hit.ExplanationRo);

        var nothingOld = after with { Addresses = [.. after.Addresses.Where(a => a.IsCurrent)] };
        Assert.Equal(RuleBasis.Verified, HitOf(Run(nothingOld), RuleIds.FatcaInCareOf).Basis);
    }

    [Theory]
    [InlineData(5_000_000, "RON")] // over USD 1,000,000 at the upper-bound rate
    [InlineData(1_000, "JPY")] // unknown currency: never assumed lower value
    public void High_value_or_unknown_currency_keeps_the_in_care_of_indicium(int balance, string currency)
    {
        var after = RoResident().WithAccounts(Account(AccountType.Depository, FatcaPreexisting, balance: balance, currency: currency))
            .WithOnlyAddress(AddressKind.InCareOf, "IT");
        HitOf(Run(after), RuleIds.FatcaInCareOf);
    }

    [Fact]
    public void A_self_certification_declaring_us_status_covers_fatca_indicia_and_reports_to_the_us()
    {
        var sc = SelfCert(ChangeAt, new SubjectTaxResidence("RO", "1900101400004"), new SubjectTaxResidence("US", "123-45-6789"))
            with { DeclaresUsPerson = true };
        var o = Run(RoResident().WithCitizenships("RO", "US").WithSelfCert(sc));
        Assert.Equal(Decision.Compliant, o.Decision);
        Assert.Equal(Coverage.SelfCertification, HitOf(o, RuleIds.FatcaCitizenship).CoveredBy);
        Assert.Equal(["US"], o.ReportableFor);
    }

    [Fact]
    public void An_accepted_explanation_never_covers_a_fatca_indicium()
    {
        var o = Run(RoResident().BornIn("US").WithExplanation("US", "Other"));
        Assert.Equal(Decision.FatcaAlert, o.Decision);
        Assert.Equal(Coverage.None, HitOf(o, RuleIds.FatcaBirthPlace).CoveredBy);
    }

    [Fact]
    public void Account_opened_between_the_cutoffs_is_new_for_fatca_and_preexisting_for_crs()
    {
        var account = Account(AccountType.Custodial, new DateOnly(2015, 3, 1))
            .WithOrder("073909523/4417209358", "US").WithOrder("DE02700100800918273645", "DE");
        var o = Run(RoResident().WithAccounts(account));
        Assert.Equal(RuleBasis.Interpretation, HitOf(o, RuleIds.FatcaStandingOrder).Basis);
        Assert.Equal(RuleBasis.Verified, Assert.Single(o.Indicia, h => h.RuleId == RuleIds.CrsStandingOrder).Basis);
    }

    // A new "not a US person" self-certification, against IGA Annex I as published by the US Treasury: III.B.2 for new
    // accounts, II.B.3-4 for preexisting ones, VI.A for a form the file contradicts.
    private static readonly SubjectSelfCertification NotUsPerson = SelfCert(ChangeAt, new SubjectTaxResidence("RO", "1900101400004"));

    public static TheoryData<string, string> NeedsDocumentsWhenPreexisting => new()
    {
        { "address", RuleIds.FatcaAddress }, { "us-only-phone", RuleIds.FatcaPhone }, { "order", RuleIds.FatcaStandingOrder },
    };

    private static RuleSubject WithUsLink(RuleSubject s, string link, DateOnly openedOn) => link switch
    {
        "address" => s.WithAccounts(Account(AccountType.Depository, openedOn)).MovedTo("US"),
        "us-only-phone" => s.WithAccounts(Account(AccountType.Depository, openedOn)).ReplacePhone("+12025550143", "US"),
        "order" => s.WithAccounts(Account(AccountType.Depository, openedOn).WithOrder("073909523/4417209358", "US")),
        "phone-next-to-ro" => s.WithAccounts(Account(AccountType.Depository, openedOn)).WithPhone("+13125550147", "US"),
        "power-of-attorney" => s.WithAccounts(Account(AccountType.Depository, openedOn)).WithPowerOfAttorney("Daniel Ionescu", "US"),
        _ => throw new ArgumentOutOfRangeException(nameof(link)),
    };

    [Theory]
    [MemberData(nameof(NeedsDocumentsWhenPreexisting))]
    public void The_form_from_account_opening_never_covers_a_fatca_indicium(string link, string ruleId)
    {
        var client = WithUsLink(RoResident(), link, new DateOnly(2019, 3, 14));

        Assert.Equal(Decision.FatcaAlert, Engine.EvaluateCurrent(client).Decision);
        var o = Engine.EvaluateCurrent(client, ChangeAt); // its only form was signed in 2019, before the change
        Assert.Equal(Decision.FatcaAlert, o.Decision);
        Assert.Equal(Coverage.None, HitOf(o, ruleId).CoveredBy);
    }

    [Theory]
    [MemberData(nameof(NeedsDocumentsWhenPreexisting))]
    public void On_a_new_account_a_new_not_us_person_form_covers_the_indicium_as_our_reading(string link, string ruleId)
    {
        var client = WithUsLink(RoResident(), link, new DateOnly(2019, 3, 14)).WithSelfCert(NotUsPerson);

        var o = Engine.EvaluateCurrent(client, ChangeAt);

        Assert.Equal(Decision.Compliant, o.Decision);
        var hit = HitOf(o, ruleId);
        Assert.Equal((Coverage.SelfCertification, RuleBasis.Interpretation), (hit.CoveredBy, hit.Basis));
        Assert.Contains("III.B.2", hit.ExplanationRo);
        Assert.DoesNotContain("US", o.ReportableFor);
        Assert.Equal(RuleBasis.Interpretation, Assert.Single(o.Cleared, c => c.RuleId == RuleIds.FatcaChange).Basis);
        // Without the date the engine cannot tell the form is new, so it keeps the alert.
        Assert.Equal(Decision.FatcaAlert, Engine.EvaluateCurrent(client).Decision);
    }

    [Theory]
    [MemberData(nameof(NeedsDocumentsWhenPreexisting))]
    public void On_a_preexisting_account_the_form_alone_is_not_enough_for_address_us_only_phone_or_order(string link, string ruleId)
    {
        var client = WithUsLink(RoResident(), link, FatcaPreexisting).WithSelfCert(NotUsPerson);

        var o = Engine.EvaluateCurrent(client, ChangeAt);

        Assert.Equal(Decision.FatcaAlert, o.Decision);
        var hit = HitOf(o, ruleId);
        Assert.Equal(Coverage.None, hit.CoveredBy);
        Assert.Contains("II.B.4 lit. b-c", hit.ExplanationRo);
        Assert.Contains("II.B.4(b)-(c)", hit.Explanation);
        Assert.DoesNotContain(o.Cleared, c => c.RuleId == RuleIds.FatcaChange);
        Assert.Contains("Cont preexistent", o.ExplanationRo);
    }

    [Theory]
    [InlineData("phone-next-to-ro", RuleIds.FatcaPhone, true)]
    [InlineData("phone-next-to-ro", RuleIds.FatcaPhone, false)]
    [InlineData("power-of-attorney", RuleIds.FatcaPowerOfAttorney, true)]
    [InlineData("power-of-attorney", RuleIds.FatcaPowerOfAttorney, false)]
    public void Lit_d_indicia_are_covered_by_the_new_form_alone_on_any_account(string link, string ruleId, bool preexisting)
    {
        var opened = preexisting ? FatcaPreexisting : new DateOnly(2019, 3, 14);
        var client = WithUsLink(RoResident(), link, opened).WithSelfCert(NotUsPerson);

        var o = Engine.EvaluateCurrent(client, ChangeAt);

        Assert.Equal(Decision.Compliant, o.Decision);
        var hit = HitOf(o, ruleId);
        Assert.Equal(Coverage.SelfCertification, hit.CoveredBy);
        Assert.Equal(preexisting ? RuleBasis.Verified : RuleBasis.Interpretation, hit.Basis); // the indicium's own basis
        Assert.Contains("II.B.4 lit. d", hit.ExplanationRo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Us_citizenship_or_birthplace_on_file_is_never_covered_by_a_not_us_person_form(bool preexisting)
    {
        var account = Account(AccountType.Depository, preexisting ? FatcaPreexisting : new DateOnly(2019, 3, 14));

        var citizen = Engine.EvaluateCurrent(RoResident().WithAccounts(account).WithCitizenships("RO", "US").WithSelfCert(NotUsPerson), ChangeAt);
        Assert.Equal(Decision.FatcaAlert, citizen.Decision);
        Assert.Contains("VI.A", HitOf(citizen, RuleIds.FatcaCitizenship).ExplanationRo);

        var born = Engine.EvaluateCurrent(RoResident().WithAccounts(account).BornIn("US").WithSelfCert(NotUsPerson), ChangeAt);
        Assert.Equal(Decision.FatcaAlert, born.Decision);
        Assert.Contains("II.B.4 lit. a", HitOf(born, RuleIds.FatcaBirthPlace).ExplanationRo);
    }

    [Fact]
    public void A_form_signed_the_day_the_change_became_known_is_new_and_one_from_the_day_before_is_not()
    {
        var client = WithUsLink(RoResident(), "power-of-attorney", new DateOnly(2019, 3, 14));
        RuleOutcome SignedOn(DateTime at) =>
            Engine.EvaluateCurrent(client.WithSelfCert(SelfCert(at, new SubjectTaxResidence("RO", "1900101400004"))), ChangeAt);

        Assert.Equal(Decision.Compliant, SignedOn(ChangeAt.Date).Decision); // a date on a form has no time
        Assert.Equal(Decision.FatcaAlert, SignedOn(ChangeAt.Date.AddDays(-1)).Decision);
    }

    [Fact]
    public void Golden_14_closes_on_a_new_not_us_person_form_through_the_shared_contract()
    {
        var c = EngineCases.Get("14");
        ICrsRuleEngine engine = new CrsRuleEngine();
        var answered = Snapshots.From(c.After.WithSelfCert(NotUsPerson));

        Assert.Equal(Decision.FatcaAlert, engine.Evaluate(Snapshots.From(c.Before), Snapshots.From(c.After), Features.Changes.ChangeKind.PhoneAdded).Decision);
        Assert.Equal(Decision.FatcaAlert, engine.EvaluateCurrent(answered).Decision);
        var o = engine.EvaluateCurrent(answered, ChangeAt);
        Assert.Equal(Decision.Compliant, o.Decision);
        Assert.DoesNotContain("US", o.ReportableFor);
    }
}
