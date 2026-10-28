namespace HackathonStarter.Features.Clients;

/// <summary>
/// Everything the rule engine needs about a client, without EF: immutable and cheap to build in tests.
/// <see cref="ClientService.SnapshotAsync"/> builds it from the database; the golden cases build it by hand.
/// Every phone, address and standing order carries its derived Country, so rules never depend on the raw value
/// (which is masked before any model).
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
    SelfCertificationSnapshot? SelfCertification,
    IReadOnlyList<AcceptedExplanationSnapshot> AcceptedExplanations)
{
    public IEnumerable<AddressSnapshot> CurrentAddresses => Addresses.Where(a => a.IsCurrent);
    public IEnumerable<PhoneSnapshot> CurrentPhones => Phones.Where(p => p.IsCurrent);

    public static ClientSnapshot From(Client c) => new(
        c.Id, c.Code, c.FullName, c.BirthDate, c.BirthCountry, [.. c.Citizenships],
        [.. c.Addresses.Select(a => new AddressSnapshot(a.Kind, a.Line, a.City, a.Country, a.IsCurrent))],
        [.. c.Phones.Select(p => new PhoneSnapshot(p.Number, p.Country, p.IsCurrent))],
        [.. c.Accounts.Select(a => new AccountSnapshot(a.Iban, a.Type, a.OpenedOn, a.Balance, a.Currency,
            [.. a.StandingOrders.Select(o => new StandingOrderSnapshot(o.BeneficiaryAccount, o.BeneficiaryCountry, o.IsActive))]))],
        [.. c.PowersOfAttorney.Select(p => new PowerOfAttorneySnapshot(p.HolderName, p.HolderCountry, p.IsActive))],
        c.SelfCertifications.Where(s => s.Status == SelfCertStatus.Valid).OrderByDescending(s => s.SignedAt)
            .Select(SelfCertificationSnapshot.From).FirstOrDefault(),
        [.. c.AcceptedExplanations.Select(e => new AcceptedExplanationSnapshot(e.Jurisdiction, e.Category, e.CaseCode, e.AcceptedAt))]);
}

public sealed record AddressSnapshot(AddressKind Kind, string Line, string City, string Country, bool IsCurrent = true);

public sealed record PhoneSnapshot(string Number, string Country, bool IsCurrent = true);

/// <param name="OpenedOn">Preexisting is computed per regime: <see cref="Preexisting.For"/>.</param>
/// <param name="Balance">Needed for the low-value exception of FATCA-IND-CO on preexisting accounts (guide p. 31).</param>
public sealed record AccountSnapshot(string Iban, AccountType Type, DateOnly OpenedOn, decimal Balance, string Currency,
                                     IReadOnlyList<StandingOrderSnapshot> StandingOrders);

public sealed record StandingOrderSnapshot(string BeneficiaryAccount, string BeneficiaryCountry, bool IsActive = true);

public sealed record AcceptedExplanationSnapshot(string Jurisdiction, ExplanationCategory Category, string CaseCode, DateTime AcceptedAt);

public sealed record PowerOfAttorneySnapshot(string HolderName, string HolderCountry, bool IsActive = true);

public sealed record TaxResidenceSnapshot(string Country, string? Tin, NoTinReason NoTinReason = NoTinReason.None, string? NoTinExplanation = null);

public sealed record SelfCertificationSnapshot(DateTime SignedAt, IReadOnlyList<TaxResidenceSnapshot> Residences, bool DeclaresUsPerson)
{
    public static SelfCertificationSnapshot From(SelfCertification s) => new(
        s.SignedAt, [.. s.Residences.Select(r => new TaxResidenceSnapshot(r.Country, r.Tin, r.NoTinReason, r.NoTinExplanation))], s.DeclaresUsPerson);
}
