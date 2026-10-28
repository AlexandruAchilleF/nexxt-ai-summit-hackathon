#if CRS_PHASE0
// Compiled once the Phase 0 contracts are merged: delete the #if / #endif lines and fix the names the compiler
// reports (README, "Integrarea cu Faza 0"). Written against PLAN 7.1 / 7.2 / 8.1.
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Rules;

/// <summary>
/// The shared contract (PLAN 8.1) over the RuleSubject engine: the snapshot is mapped, then evaluated as usual.
/// </summary>
public sealed partial class CrsRuleEngine : ICrsRuleEngine
{
    public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeEvent change) =>
        Evaluate(SubjectMapper.From(before), SubjectMapper.From(after), SubjectMapper.From(change));

    public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertification selfCert) =>
        EvaluateSelfCertification(SubjectMapper.From(client), SubjectMapper.From(selfCert));
}

/// <summary>
/// ClientSnapshot to RuleSubject. Enums are matched by name, so only property names couple the two models. Countries
/// are the derived ISO codes from the snapshot (PLAN 7.1), never parsed from raw values here.
/// </summary>
public static class SubjectMapper
{
    public static RuleSubject From(ClientSnapshot s) => new()
    {
        Code = s.Code,
        FullName = $"{s.FirstName} {s.LastName}".Trim(),
        BirthDate = s.BirthDate,
        BirthCountry = s.BirthCountry,
        Citizenships = [.. s.Citizenships],
        Addresses = [.. s.Addresses.Select(a => new SubjectAddress(
            Enum.Parse<SubjectAddressKind>(a.Kind.ToString()), a.Country, a.IsCurrent, $"{a.Line}, {a.City}".Trim(' ', ',')))],
        Phones = [.. s.Phones.Select(p => new SubjectPhone(p.Number, p.Country, p.IsCurrent))],
        Accounts = [.. s.Accounts.Select(From)],
        PowersOfAttorney = [.. s.PowersOfAttorney.Select(p => new SubjectPowerOfAttorney(p.HolderName, p.HolderCountry, p.IsActive))],
        SelfCertifications = [.. s.SelfCertifications.Select(From)],
        AcceptedExplanations = [.. s.AcceptedExplanations.Select(e => new SubjectExplanation(e.Jurisdiction, e.Category.ToString()))],
    };

    // PLAN 7.1 keeps no name, address or birth date from the form: SC-VALID leaves them to the operator. When the
    // model gets them, map them here and drop FormFieldsKnown = false.
    public static SubjectSelfCertification From(SelfCertification sc) =>
        new(sc.SignedAt, [.. sc.Residences.Select(From)], sc.DeclaresUsPerson, sc.Status.ToString() == "Valid")
        {
            FormFieldsKnown = false,
        };

    public static RuleChange From(ChangeEvent c) => new(c.Kind.ToString(), c.Summary, c.SourceChannel);

    private static SubjectAccount From(Account a) =>
        new(a.Iban, Enum.Parse<SubjectAccountType>(a.Type.ToString()), a.OpenedOn, a.Balance, a.Currency, a.Product)
        {
            // GOLDEN-CASES Q2: a US account has no IBAN, so the country is set explicitly.
            StandingOrders = [.. a.StandingOrders.Select(o => new SubjectStandingOrder(o.BeneficiaryAccount, o.BeneficiaryCountry, o.IsActive))],
        };

    private static SubjectTaxResidence From(TaxResidence r) =>
        new(r.Country, r.Tin, NoTinReason(r.NoTinReason.ToString()), r.NoTinExplanation);

    // "A_CountryDoesNotIssue" (PLAN 7.1) or "CountryDoesNotIssue".
    private static SubjectNoTinReason NoTinReason(string name) => Enum.GetValues<SubjectNoTinReason>()
        .FirstOrDefault(v => v != SubjectNoTinReason.None && name.EndsWith(v.ToString(), StringComparison.Ordinal));
}
#endif
