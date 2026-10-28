using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Tin;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.Rules.Subjects;

namespace HackathonStarter.Tests.Crs.Rules;

/// <summary>
/// The shared contract: entities, then <see cref="ClientSnapshot.From"/> (what ClientService does), then
/// <see cref="ICrsRuleEngine"/>. It gives the same decision, hits and cleared rules as the RuleSubject path; texts differ
/// only where the snapshot carries less (the product name of an account).
/// </summary>
public class SnapshotPathTests
{
    private static readonly ICrsRuleEngine Engine = new CrsRuleEngine(new FakeTinFormat());

    private static IEnumerable<(string, string, Coverage, RuleBasis, string)> Shape(IEnumerable<RuleHit> hits) =>
        hits.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy, h.Basis, h.SourceField)).Order();

    [Theory]
    [MemberData(nameof(EngineCases.Ids), MemberType = typeof(EngineCases))]
    public void Every_golden_case_gives_the_same_outcome_through_the_snapshot(string id)
    {
        var c = EngineCases.Get(id);
        var direct = new CrsRuleEngine(new FakeTinFormat()).Evaluate(c.Before, c.After, c.Change);
        var shared = Engine.Evaluate(Snapshots.From(c.Before), Snapshots.From(c.After), Enum.Parse<ChangeKind>(c.Change.Kind));

        Assert.Equal(c.Expected, shared.Decision);
        Assert.Equal(Shape(direct.Indicia), Shape(shared.Indicia));
        Assert.Equal(Shape(direct.Cleared), Shape(shared.Cleared));
        Assert.Equal(direct.ReportableFor, shared.ReportableFor);
        Assert.Equal(c.Regime, shared.Regime);
        Assert.Equal(direct.Uncovered.Count(), shared.Uncovered.Count());
        var said = string.Join(" ", shared.Cleared.Select(x => x.ExplanationRo).Prepend(shared.ExplanationRo));
        foreach (var phrase in c.Says ?? []) Assert.Contains(phrase, said);
    }

    [Theory]
    [MemberData(nameof(EngineCases.Ids), MemberType = typeof(EngineCases))]
    public void Explanation_is_english_and_explanation_ro_is_romanian(string id)
    {
        var c = EngineCases.Get(id);
        var o = Engine.Evaluate(Snapshots.From(c.Before), Snapshots.From(c.After), Enum.Parse<ChangeKind>(c.Change.Kind));

        Assert.All(o.Indicia.Concat(o.Cleared).Select(h => (h.Explanation, h.ExplanationRo)).Append((o.Explanation, o.ExplanationRo)), t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Explanation));
            Assert.NotEqual(t.Explanation, t.ExplanationRo);
            Assert.DoesNotContain(" și ", t.Explanation);
        });
    }

    [Fact]
    public void Andrei_phone_texts_are_in_the_right_language()
    {
        var c = EngineCases.Get("13");
        var phone = Assert.Single(Engine.Evaluate(Snapshots.From(c.Before), Snapshots.From(c.After), ChangeKind.PhoneAdded).Cleared,
            x => x.RuleId == RuleIds.CrsPhone);

        Assert.Contains("telefon actual în România", phone.ExplanationRo);
        Assert.Contains("Romanian phone", phone.Explanation);
    }

    [Fact]
    public void Golden_2_through_the_snapshot_needs_a_corrected_tin()
    {
        var cristina = RoResident("CL-0002", "2900101405128", new DateOnly(2026, 10, 27), fullName: "Cristina Dobre");
        var form = new SelfCertificationSnapshot(new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
            [new TaxResidenceSnapshot("IT", "DBRCST90A41Z129")], false);

        var o = Engine.EvaluateSelfCertification(Snapshots.From(cristina), form);

        Assert.Equal(Decision.Clarification, o.Decision);
        var hit = Assert.Single(o.Indicia);
        Assert.Equal((RuleIds.TinFormat, "IT", Coverage.None, RuleBasis.Policy), (hit.RuleId, hit.Jurisdiction, hit.CoveredBy, hit.Basis));
        Assert.Contains("corectarea", o.ExplanationRo);
        Assert.Contains("correction", o.Explanation);
        Assert.Equal(Regime.Crs, o.Regime);
    }

    [Fact]
    public void Golden_3_through_the_snapshot_leaves_the_form_fields_to_the_operator()
    {
        var bogdan = RoResident("CL-0003", "1870522083340", new DateOnly(2026, 10, 26), fullName: "Bogdan Lazăr")
            .MovedTo("AT", "Annenstraße 24, 8020 Graz");
        var form = new SelfCertificationSnapshot(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            [new TaxResidenceSnapshot("AT", null, NoTinReason.B_UnableToObtain, "Nu am primit Steuernummer.")], false);

        var o = Engine.EvaluateSelfCertification(Snapshots.From(bogdan), form);

        Assert.Equal(Decision.Compliant, o.Decision);
        var hit = Assert.Single(o.Indicia);
        Assert.Equal((RuleIds.TinMissing, "AT", Coverage.SelfCertification, RuleBasis.Interpretation),
            (hit.RuleId, hit.Jurisdiction, hit.CoveredBy, hit.Basis));
        var valid = Assert.Single(o.Cleared, x => x.RuleId == RuleIds.SelfCertValid);
        Assert.Equal(RuleBasis.Policy, valid.Basis);
        Assert.Contains("operatorul", valid.ExplanationRo);
        Assert.Equal(["AT"], o.ReportableFor);
        Assert.Null(o.Regime);
        Assert.Empty(o.Uncovered);
    }

    [Fact]
    public void Maria_response_form_through_the_snapshot_is_format_valid_and_reportable_for_germany()
    {
        var f = EngineCases.MariaResponseForm;
        var form = new SelfCertificationSnapshot(f.SignedAt, [new TaxResidenceSnapshot("DE", "86095174223")], false);

        var o = Engine.EvaluateSelfCertification(Snapshots.From(EngineCases.MariaAfter), form);

        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("format valid", Assert.Single(o.Cleared, x => x.RuleId == RuleIds.TinFormat).Explanation);
        Assert.Equal(["DE"], o.ReportableFor);
    }

    [Fact]
    public void A_doubtful_self_certification_is_not_in_the_snapshot_and_covers_nothing()
    {
        var client = Snapshots.Entity(RoResident().MovedTo("AT"));
        client.SelfCertifications[0].Status = SelfCertStatus.Doubtful;
        client.SelfCertifications[0].Residences = [new TaxResidence { Country = "AT", Tin = "123456789" }];

        var after = ClientSnapshot.From(client);
        Assert.Null(after.SelfCertification);
        Assert.Equal(Decision.Clarification, Engine.Evaluate(Snapshots.From(RoResident()), after, ChangeKind.AddressChanged).Decision);
    }

    [Fact]
    public void Evaluate_current_rereads_the_whole_client_without_calling_indicia_old()
    {
        var maria = Engine.EvaluateCurrent(Snapshots.From(EngineCases.MariaAfter));
        Assert.Equal(Decision.Clarification, maria.Decision);
        Assert.Equal(Regime.Crs, maria.Regime);
        Assert.Equal(2, maria.Uncovered.Count());
        Assert.All(maria.Indicia, h => Assert.DoesNotContain("exista și înainte", h.ExplanationRo));

        // Golden 18: CaseService re-evaluates with the accepted explanation before closing.
        var c = EngineCases.Get("18-explained");
        var teodora = Engine.EvaluateCurrent(Snapshots.From(c.After));
        Assert.Equal(Decision.Compliant, teodora.Decision);
        Assert.Equal(Coverage.AcceptedExplanation, Assert.Single(teodora.Indicia).CoveredBy);
        Assert.Empty(teodora.ReportableFor);
        Assert.Null(teodora.Regime);
    }

    [Fact]
    public void Wired_like_program_cs_the_phase_0_tin_placeholder_means_not_checked_never_wrong_format()
    {
        var provider = new ServiceCollection().AddRules().AddTin().BuildServiceProvider();
        var engine = provider.GetRequiredService<ICrsRuleEngine>();
        Assert.Same(provider.GetRequiredService<CrsRuleEngine>(), engine);

        var form = new SelfCertificationSnapshot(ChangeAt, [new TaxResidenceSnapshot("IT", "DBRCST90A41Z129")], false);
        var o = engine.EvaluateSelfCertification(Snapshots.From(RoResident()), form);

        Assert.Equal(Decision.NoCase, o.Decision);
        Assert.Contains("format not checked", Assert.Single(o.Cleared, x => x.RuleId == RuleIds.TinFormat).Explanation);
    }
}

/// <summary>RuleSubject fixtures as the shared entities, then as snapshots through ClientSnapshot.From.</summary>
internal static class Snapshots
{
    public static ClientSnapshot From(RuleSubject s) => ClientSnapshot.From(Entity(s));

    public static Client Entity(RuleSubject s) => new()
    {
        Code = s.Code,
        FirstName = s.FullName,
        BirthDate = s.BirthDate ?? default,
        BirthCountry = s.BirthCountry,
        Citizenships = [.. s.Citizenships],
        Addresses = [.. s.Addresses.Select(a => new Address { Kind = a.Kind, Line = a.Text, Country = a.Country, IsCurrent = a.IsCurrent })],
        Phones = [.. s.Phones.Select(p => new Phone { Number = p.Number, Country = p.Country, IsCurrent = p.IsCurrent })],
        Accounts = [.. s.Accounts.Select(a => new Account
        {
            Iban = a.Number, Type = a.Type, Product = a.Product, OpenedOn = a.OpenedOn, Balance = a.Balance, Currency = a.Currency,
            StandingOrders = [.. a.StandingOrders.Select(o => new StandingOrder
            {
                BeneficiaryAccount = o.BeneficiaryAccount, BeneficiaryCountry = o.BeneficiaryCountry, IsActive = o.IsActive,
            })],
        })],
        PowersOfAttorney = [.. s.PowersOfAttorney.Select(p => new PowerOfAttorney
        {
            HolderName = p.HolderName, HolderCountry = p.HolderCountry, IsActive = p.IsActive,
        })],
        SelfCertifications = [.. s.SelfCertifications.Select(sc => new SelfCertification
        {
            SignedAt = sc.SignedAt, DeclaresUsPerson = sc.DeclaresUsPerson,
            Status = sc.IsValid ? SelfCertStatus.Valid : SelfCertStatus.Doubtful,
            Residences = [.. sc.Residences.Select(r => new TaxResidence
            {
                Country = r.Country, Tin = r.Tin, NoTinReason = r.NoTinReason, NoTinExplanation = r.NoTinExplanation,
            })],
        })],
        AcceptedExplanations = [.. s.AcceptedExplanations.Select(e => new AcceptedExplanation
        {
            Jurisdiction = e.Jurisdiction, Category = Enum.Parse<ExplanationCategory>(e.Category), CaseCode = "CRS-TEST-0001", AcceptedAt = ChangeAt,
        })],
    };
}
