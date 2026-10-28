namespace HackathonStarter.Features.Clients;

/// <summary>
/// Everything the rule engine needs about a client, without EF: immutable and cheap to build in tests.
/// <see cref="ClientService.SnapshotAsync"/> builds it from the database; the golden cases build it by hand.
/// </summary>
public sealed record ClientSnapshot(
    int ClientId,
    string Code,
    string FullName,
    DateOnly BirthDate,
    string? BirthCountry,
    IReadOnlyList<string> Citizenships,
    IReadOnlyList<AddressSnapshot> Addresses,
    IReadOnlyList<PhoneSnapshot> Phones,
    IReadOnlyList<AccountSnapshot> Accounts,
    IReadOnlyList<PowerOfAttorneySnapshot> PowersOfAttorney,
    SelfCertificationSnapshot? SelfCertification)
{
    public IEnumerable<AddressSnapshot> CurrentAddresses => Addresses.Where(a => a.IsCurrent);
    public IEnumerable<PhoneSnapshot> CurrentPhones => Phones.Where(p => p.IsCurrent);

    public static ClientSnapshot From(Client c) => new(
        c.Id, c.Code, c.FullName, c.BirthDate, c.BirthCountry, [.. c.Citizenships],
        [.. c.Addresses.Select(a => new AddressSnapshot(a.Kind, a.Line, a.City, a.Country, a.IsCurrent))],
        [.. c.Phones.Select(p => new PhoneSnapshot(p.Number, p.Country, p.IsCurrent))],
        [.. c.Accounts.Select(a => new AccountSnapshot(a.Iban, a.Type, a.IsPreexisting,
            [.. a.StandingOrders.Select(o => new StandingOrderSnapshot(o.BeneficiaryIban, o.BeneficiaryCountry, o.IsActive))]))],
        [.. c.PowersOfAttorney.Select(p => new PowerOfAttorneySnapshot(p.HolderName, p.HolderCountry, p.IsActive))],
        c.SelfCertifications.Where(s => s.Status == SelfCertStatus.Valid).OrderByDescending(s => s.SignedAt)
            .Select(SelfCertificationSnapshot.From).FirstOrDefault());
}

public sealed record AddressSnapshot(AddressKind Kind, string Line, string City, string Country, bool IsCurrent = true);

public sealed record PhoneSnapshot(string Number, string Country, bool IsCurrent = true);

public sealed record AccountSnapshot(string Iban, AccountType Type, bool IsPreexisting, IReadOnlyList<StandingOrderSnapshot> StandingOrders);

public sealed record StandingOrderSnapshot(string BeneficiaryIban, string BeneficiaryCountry, bool IsActive = true);

public sealed record PowerOfAttorneySnapshot(string HolderName, string HolderCountry, bool IsActive = true);

public sealed record TaxResidenceSnapshot(string Country, string? Tin, NoTinReason NoTinReason = NoTinReason.None, string? NoTinExplanation = null);

public sealed record SelfCertificationSnapshot(DateTime SignedAt, IReadOnlyList<TaxResidenceSnapshot> Residences, bool DeclaresUsPerson)
{
    public static SelfCertificationSnapshot From(SelfCertification s) => new(
        s.SignedAt, [.. s.Residences.Select(r => new TaxResidenceSnapshot(r.Country, r.Tin, r.NoTinReason, r.NoTinExplanation))], s.DeclaresUsPerson);
}
