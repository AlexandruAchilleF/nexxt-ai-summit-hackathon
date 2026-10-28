using HackathonStarter.Features.Rules;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using AddressKind = HackathonStarter.Features.Clients.AddressKind;
using static HackathonStarter.Tests.Crs.Rules.Subjects;

namespace HackathonStarter.Tests.Crs.Rules;

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
        Assert.Equal(RuleBasis.Verified, HitOf(Run(after), RuleIds.FatcaInCareOf).Basis);
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
}
