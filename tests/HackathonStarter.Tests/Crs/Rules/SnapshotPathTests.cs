#if CRS_PHASE0
// Compiled with SubjectMapper once the Phase 0 contracts are merged (delete the #if / #endif lines).
using System.Text.Json;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.Rules.Subjects;

namespace HackathonStarter.Tests.Crs.Rules;

/// <summary>The shared contract (ClientSnapshot, ICrsRuleEngine) gives the same outcome as the RuleSubject path.</summary>
public class SnapshotPathTests
{
    private static readonly ICrsRuleEngine Engine = new CrsRuleEngine(new FakeTinFormat());

    [Theory]
    [MemberData(nameof(EngineCases.Ids), MemberType = typeof(EngineCases))]
    public void Every_golden_case_gives_the_same_outcome_through_the_snapshot(string id)
    {
        var c = EngineCases.Get(id);
        var direct = new CrsRuleEngine(new FakeTinFormat()).Evaluate(c.Before, c.After, c.Change);
        var shared = Engine.Evaluate(Snapshots.From(c.Before), Snapshots.From(c.After), Snapshots.From(c.Change));

        Assert.Equal(JsonSerializer.Serialize(direct), JsonSerializer.Serialize(shared));
    }

    [Fact]
    public void Golden_3_through_the_snapshot_leaves_the_form_fields_to_the_operator()
    {
        var bogdan = RoResident("CL-0003", "1870522083340", new DateOnly(2026, 10, 26)).MovedTo("AT", "Graz");
        var sc = new SelfCertification
        {
            SignedAt = ChangeAt,
            Channel = "ghișeu",
            Residences = [new TaxResidence("AT", null, NoTinReason.B_UnableToObtain, "Nu am primit Steuernummer.")],
            Status = SelfCertStatus.Valid,
        };

        var o = Engine.EvaluateSelfCertification(Snapshots.From(bogdan), sc);

        Assert.Equal(Decision.Compliant, o.Decision);
        Assert.Equal((RuleIds.TinMissing, "AT", Coverage.SelfCertification), (o.Indicia[0].RuleId, o.Indicia[0].Jurisdiction, o.Indicia[0].CoveredBy));
        var valid = Assert.Single(o.Cleared, x => x.RuleId == RuleIds.SelfCertValid);
        Assert.Equal(RuleBasis.Policy, valid.Basis);
        Assert.Contains("operatorul", valid.Explanation);
    }

    [Fact]
    public void A_doubtful_self_certification_covers_nothing()
    {
        var after = Snapshots.From(RoResident().MovedTo("AT"));
        after.SelfCertifications[0].Status = SelfCertStatus.Doubtful;
        after.SelfCertifications[0].Residences = [new TaxResidence("AT", "123456789", NoTinReason.None, null)];

        Assert.Equal(Decision.Clarification, Engine.Evaluate(Snapshots.From(RoResident()), after, Snapshots.From(new RuleChange("AddressChanged"))).Decision);
    }

    [Fact]
    public void Add_rules_registers_the_shared_contract_on_the_same_engine()
    {
        var provider = new ServiceCollection().AddRules().BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<CrsRuleEngine>(), provider.GetRequiredService<ICrsRuleEngine>());
    }
}

/// <summary>RuleSubject fixtures as shared snapshots (the inverse of SubjectMapper), until R5's GoldenCases.cs exists.</summary>
internal static class Snapshots
{
    public static ClientSnapshot From(RuleSubject s) => new()
    {
        Code = s.Code,
        FirstName = s.FullName,
        LastName = "",
        BirthDate = s.BirthDate ?? default,
        BirthCountry = s.BirthCountry,
        Citizenships = [.. s.Citizenships],
        Addresses = [.. s.Addresses.Select(a => new Address
        {
            Kind = Enum.Parse<AddressKind>(a.Kind.ToString()), Line = a.Text, City = "", Country = a.Country, IsCurrent = a.IsCurrent,
        })],
        Phones = [.. s.Phones.Select(p => new Phone { Number = p.Number, Country = p.Country, IsCurrent = p.IsCurrent })],
        Accounts = [.. s.Accounts.Select(a => new Account
        {
            Iban = a.Number, Type = Enum.Parse<AccountType>(a.Type.ToString()), Product = a.Product, OpenedOn = a.OpenedOn,
            Balance = a.Balance, Currency = a.Currency,
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
            Residences = [.. sc.Residences.Select(r => new TaxResidence(r.Country, r.Tin, r.NoTinReason switch
            {
                SubjectNoTinReason.CountryDoesNotIssue => NoTinReason.A_CountryDoesNotIssue,
                SubjectNoTinReason.UnableToObtain => NoTinReason.B_UnableToObtain,
                SubjectNoTinReason.NotRequired => NoTinReason.C_NotRequired,
                _ => NoTinReason.None,
            }, r.NoTinExplanation))],
        })],
        AcceptedExplanations = [.. s.AcceptedExplanations.Select(e => new AcceptedExplanation(
            e.Jurisdiction, Enum.Parse<ExplanationCategory>(e.Category), null, ChangeAt))],
    };

    public static ChangeEvent From(RuleChange c) => new()
    {
        Kind = Enum.Parse<ChangeKind>(c.Kind), Summary = c.Summary, SourceChannel = c.SourceChannel, ReceivedAt = ChangeAt,
    };
}
#endif
