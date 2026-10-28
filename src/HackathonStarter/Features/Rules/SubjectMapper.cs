using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Rules;

/// <summary>The shared contract (PLAN 8.1) over the RuleSubject engine: the snapshot is mapped, then evaluated as usual.</summary>
public sealed partial class CrsRuleEngine
{
    public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind) =>
        Evaluate(SubjectMapper.From(before), SubjectMapper.From(after), new RuleChange(kind.ToString()));

    public RuleOutcome EvaluateCurrent(ClientSnapshot client) => EvaluateCurrent(SubjectMapper.From(client));

    public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification) =>
        EvaluateSelfCertification(SubjectMapper.From(client), SubjectMapper.From(selfCertification));
}

/// <summary>
/// ClientSnapshot to RuleSubject. Countries are the derived ISO codes from the snapshot, never parsed from raw values here.
/// What the snapshot does not carry stays at the safe default: no service-provider addresses or phones (the III.B pct. 13
/// exception is not applied), no tax residence on file outside the self-certification (lit. a) is not applied), no product
/// name (the explanation names the account type).
/// </summary>
public static class SubjectMapper
{
    public static RuleSubject From(ClientSnapshot s) => new()
    {
        Code = s.Code,
        FullName = s.FullName.Trim(),
        BirthDate = s.BirthDate == default ? null : s.BirthDate,
        BirthCountry = s.BirthCountry,
        Citizenships = [.. s.Citizenships],
        Addresses = [.. s.Addresses.Select(a => new SubjectAddress(a.Kind, a.Country, a.IsCurrent, $"{a.Line}, {a.City}".Trim(' ', ',')))],
        Phones = [.. s.Phones.Select(p => new SubjectPhone(p.Number, p.Country, p.IsCurrent))],
        Accounts = [.. s.Accounts.Select(a => new SubjectAccount(a.Iban, a.Type, a.OpenedOn, a.Balance, a.Currency)
        {
            // GOLDEN-CASES Q2: a US account has no IBAN, so the country is the explicit one.
            StandingOrders = [.. a.StandingOrders.Select(o => new SubjectStandingOrder(o.BeneficiaryAccount, o.BeneficiaryCountry, o.IsActive))],
        })],
        PowersOfAttorney = [.. s.PowersOfAttorney.Select(p => new SubjectPowerOfAttorney(p.HolderName, p.HolderCountry, p.IsActive))],
        // The snapshot carries only the latest valid self-certification (or none).
        SelfCertifications = s.SelfCertification is { } sc ? [From(sc)] : [],
        AcceptedExplanations = [.. s.AcceptedExplanations.Select(e => new SubjectExplanation(e.Jurisdiction, e.Category.ToString()))],
    };

    // The snapshot keeps no name, address or birth date from the form: SC-VALID leaves them to the operator.
    public static SubjectSelfCertification From(SelfCertificationSnapshot sc) =>
        new(sc.SignedAt, [.. sc.Residences.Select(r => new SubjectTaxResidence(r.Country, r.Tin, r.NoTinReason, r.NoTinExplanation))],
            sc.DeclaresUsPerson)
        {
            FormFieldsKnown = false,
        };
}
