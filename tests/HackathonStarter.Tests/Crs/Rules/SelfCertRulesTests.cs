using HackathonStarter.Features.Rules;
using AddressKind = HackathonStarter.Features.Clients.AddressKind;
using NoTinReason = HackathonStarter.Features.Clients.NoTinReason;
using static HackathonStarter.Tests.Crs.RuleEngine.Subjects;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>EvaluateSelfCertification: validity, TIN per jurisdiction, reasonableness (guide p. 33-37). Golden cases 2 and 3.</summary>
public class SelfCertRulesTests
{
    private static readonly CrsRuleEngine Engine = new(new FakeTinFormat());

    private static SubjectSelfCertification Form(params SubjectTaxResidence[] residences) => SelfCert(ChangeAt, residences);

    private static RuleOutcome Run(SubjectSelfCertification sc, RuleSubject? client = null) =>
        Engine.EvaluateSelfCertification(client ?? RoResident(), sc);

    [Fact]
    public void Golden_2_italian_tin_in_the_wrong_format_needs_clarification()
    {
        var cristina = RoResident("CL-0002", "2900101405128", new DateOnly(2026, 10, 27), fullName: "Cristina Dobre",
            iban: "RO59RZBR0000060041172630");
        var sc = Form(new SubjectTaxResidence("IT", "DBRCST90A41Z129")) with
        {
            SignedAt = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc), FullName = "Cristina Dobre",
            ResidenceAddress = "Via Padova 18, 20127 Milano", ResidenceAddressCountry = "IT",
            BirthDate = new DateOnly(1990, 1, 1),
        };

        var o = Run(sc, cristina);

        Assert.Equal(Decision.Clarification, o.Decision);
        var hit = Assert.Single(o.Indicia);
        Assert.Equal((RuleIds.TinFormat, "IT", Coverage.None, RuleBasis.Policy), (hit.RuleId, hit.Jurisdiction, hit.CoveredBy, hit.Basis));
        Assert.Contains("nu de titular", hit.ExplanationRo);
        Assert.Contains("corectarea", o.ExplanationRo);
        Assert.DoesNotContain("explicație rezonabilă", o.ExplanationRo);
        Assert.Empty(o.ReportableFor);
    }

    [Fact]
    public void The_corrected_italian_tin_is_format_valid_never_tin_valid()
    {
        var o = Run(Form(new SubjectTaxResidence("IT", "DBRCST90A41Z129T")));
        Assert.Equal(Decision.NoCase, o.Decision);
        var cleared = Assert.Single(o.Cleared, c => c.RuleId == RuleIds.TinFormat);
        Assert.Contains("format valid", cleared.ExplanationRo);
        Assert.DoesNotContain("TIN valid", cleared.ExplanationRo + cleared.Explanation);
        Assert.Equal(["IT"], o.ReportableFor);
    }

    [Fact]
    public void Golden_3_austria_without_tin_and_with_a_reason_is_compliant()
    {
        var bogdan = RoResident("CL-0003", "1870522083340", new DateOnly(2026, 10, 26), fullName: "Bogdan Lazăr",
            birthDate: new DateOnly(1987, 5, 22), iban: "RO57RZBR0000060052208841").MovedTo("AT", "Annenstraße 24, 8020 Graz");
        var sc = Form(new SubjectTaxResidence("AT", null, NoTinReason.B_UnableToObtain,
            "Nu sunt încă înregistrat la administrația fiscală austriacă și nu am primit Steuernummer.")) with
        {
            SignedAt = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc), FullName = "Bogdan Lazăr", ResidenceAddress = "Annenstraße 24, 8020 Graz", ResidenceAddressCountry = "AT",
            BirthDate = new DateOnly(1987, 5, 22),
        };

        var o = Run(sc, bogdan);

        Assert.Equal(Decision.Compliant, o.Decision);
        var hit = Assert.Single(o.Indicia);
        Assert.Equal((RuleIds.TinMissing, "AT", Coverage.SelfCertification), (hit.RuleId, hit.Jurisdiction, hit.CoveredBy));
        Assert.Contains("nu emite TIN tuturor", hit.ExplanationRo);
        Assert.Contains("motiv B", hit.ExplanationRo);
        Assert.Equal(RuleBasis.Interpretation, hit.Basis); // Q8: A or B is open
        Assert.StartsWith("Autocertificare acceptată", o.ExplanationRo);
        Assert.Equal(["AT"], o.ReportableFor);
    }

    [Theory]
    [InlineData(NoTinReason.A_CountryDoesNotIssue)]
    [InlineData(NoTinReason.B_UnableToObtain)]
    public void Missing_tin_for_a_jurisdiction_that_issues_tins_to_everyone_is_reason_to_know(NoTinReason reason)
    {
        var o = Run(Form(new SubjectTaxResidence("DE", null, reason, "Motiv sintetic.")));
        Assert.Equal(Decision.Clarification, o.Decision);
        Assert.Contains("VII.A pct. 3", Assert.Single(o.Indicia).ExplanationRo);
    }

    [Fact]
    public void Missing_tin_without_a_reason_needs_clarification()
    {
        Assert.Equal(RuleIds.TinMissing, Assert.Single(Run(Form(new SubjectTaxResidence("AT", null))).Indicia).RuleId);
    }

    [Fact]
    public void Reason_b_without_the_explanation_needs_clarification()
    {
        Assert.Equal(Decision.Clarification, Run(Form(new SubjectTaxResidence("AT", null, NoTinReason.B_UnableToObtain))).Decision);
    }

    [Fact]
    public void Reason_c_is_our_reading_and_never_beats_a_jurisdiction_that_issues_tins_to_everyone()
    {
        // Guide p. 53: reason C is the client's statement about the other country's law; the engine cannot check it.
        var austria = Assert.Single(Run(Form(new SubjectTaxResidence("AT", null, NoTinReason.C_NotRequired))).Indicia);
        Assert.Equal((Coverage.SelfCertification, RuleBasis.Interpretation), (austria.CoveredBy, austria.Basis));

        // Guide p. 37: no TIN although the OECD sheet says everyone gets one, whatever the reason.
        var germany = Run(Form(new SubjectTaxResidence("DE", null, NoTinReason.C_NotRequired)));
        Assert.Equal(Decision.Clarification, germany.Decision);
        Assert.Contains("p. 37", Assert.Single(germany.Uncovered).ExplanationRo);
    }

    [Theory]
    [InlineData(NoTinReason.None)]
    [InlineData(NoTinReason.A_CountryDoesNotIssue)]
    [InlineData(NoTinReason.C_NotRequired)]
    public void A_us_residence_without_the_us_tin_is_never_accepted(NoTinReason reason)
    {
        var o = Run(Form(new SubjectTaxResidence("RO", "1900101400004"), new SubjectTaxResidence("US", null, reason, "Nu am.")));
        var hit = Assert.Single(o.Uncovered, h => h.RuleId == RuleIds.TinMissing);
        Assert.Equal("US", hit.Jurisdiction);
        Assert.Contains("p. 38", hit.ExplanationRo);
    }

    [Fact]
    public void A_jurisdiction_whose_oecd_sheet_we_did_not_read_goes_to_the_operator()
    {
        var hit = Assert.Single(Run(Form(new SubjectTaxResidence("FR", null, NoTinReason.A_CountryDoesNotIssue))).Indicia);
        Assert.Equal((Coverage.None, RuleBasis.Policy), (hit.CoveredBy, hit.Basis));
    }

    [Fact]
    public void Without_r3s_validator_the_format_is_reported_as_not_checked()
    {
        var o = new CrsRuleEngine().EvaluateSelfCertification(RoResident(), Form(new SubjectTaxResidence("IT", "DBRCST90A41Z129")));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("nu a fost verificat", Assert.Single(o.Cleared, c => c.RuleId == RuleIds.TinFormat).ExplanationRo);
    }

    [Fact]
    public void Romanian_residence_needs_no_tin_check()
    {
        var o = Run(Form(new SubjectTaxResidence("RO", null)));
        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Empty(o.ReportableFor);
    }

    [Fact]
    public void Missing_validity_elements_are_listed()
    {
        var sc = Form(new SubjectTaxResidence("RO", "1900101400004")) with { SignedOrConfirmed = false, BirthDate = null };
        var hit = Assert.Single(Run(sc).Indicia);
        Assert.Equal(RuleIds.SelfCertValid, hit.RuleId);
        Assert.Contains("semnătura", hit.ExplanationRo);
        Assert.Contains("data nașterii", hit.ExplanationRo);
    }

    [Fact]
    public void Form_fields_the_data_model_does_not_carry_are_left_to_the_operator_not_reported_missing()
    {
        var sc = new SubjectSelfCertification(ChangeAt, [new SubjectTaxResidence("RO", "1900101400004")]) { FormFieldsKnown = false };
        var o = Run(sc);
        Assert.Equal(Decision.NoCase, o.Decision);
        var valid = Assert.Single(o.Cleared, c => c.RuleId == RuleIds.SelfCertValid);
        Assert.Equal(RuleBasis.Policy, valid.Basis);
        Assert.Contains("operatorul", valid.ExplanationRo);

        Assert.Equal(RuleIds.SelfCertValid, Assert.Single(Run(sc with { Residences = [] }).Indicia).RuleId);
    }

    [Fact]
    public void A_complete_self_certification_says_so_in_cleared()
    {
        Assert.Contains(Run(Form(new SubjectTaxResidence("RO", "1900101400004"))).Cleared, c => c.RuleId == RuleIds.SelfCertValid);
    }

    [Fact]
    public void Residence_address_outside_the_declared_jurisdictions_fails_reasonableness()
    {
        var sc = Form(new SubjectTaxResidence("RO", "1900101400004")) with { ResidenceAddressCountry = "DE" };
        var hit = Assert.Single(Run(sc).Indicia);
        Assert.Equal((RuleIds.SelfCertConsistent, "DE"), (hit.RuleId, hit.Jurisdiction));
    }

    [Fact]
    public void Reportable_address_on_file_that_is_not_declared_fails_reasonableness()
    {
        var client = RoResident().WithAddress(AddressKind.Mailing, "AT");
        var hit = Assert.Single(Run(Form(new SubjectTaxResidence("RO", "1900101400004")), client).Indicia);
        Assert.Equal((RuleIds.SelfCertConsistent, "AT"), (hit.RuleId, hit.Jurisdiction));
    }

    // A c/o address on file is a mailing address too (GOLDEN-CASES Î10; guide p. 29 lit. b, 48).
    [Fact]
    public void Reportable_in_care_of_address_on_file_that_is_not_declared_fails_reasonableness()
    {
        var client = RoResident().WithAddress(AddressKind.InCareOf, "IT");
        var hit = Assert.Single(Run(Form(new SubjectTaxResidence("RO", "1900101400004")), client).Indicia);
        Assert.Equal((RuleIds.SelfCertConsistent, "IT"), (hit.RuleId, hit.Jurisdiction));
    }

    [Fact]
    public void Us_citizen_on_file_without_us_status_on_the_form_is_a_fatca_alert()
    {
        var o = Run(Form(new SubjectTaxResidence("RO", "1900101400004")), RoResident().WithCitizenships("RO", "US"));
        Assert.Equal(Decision.FatcaAlert, o.Decision);
        Assert.Equal(RuleIds.FatcaChange, Assert.Single(o.Indicia).RuleId);
    }
}
