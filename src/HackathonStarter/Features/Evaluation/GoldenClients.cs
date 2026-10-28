using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Evaluation;

/// <summary>
/// The golden cases' clients as they are BEFORE the change (docs/GOLDEN-CASES.md, section 4), built once per client
/// code: the golden cases take their snapshots from here (<see cref="ClientSnapshot.From"/>), the seed can insert the
/// same entities. Every call returns new entities (EF tracks and mutates them). Values come from the cards; the few
/// a card does not give (balances, Radu's phone, Ioana's product) are marked "added". Owner: R5.
///
/// "Standard RO resident" (GOLDEN-CASES.md, 1): current residence and mobile in RO, citizenship [RO], born in RO,
/// a Valid self-certification [RO, TIN = CNP] signed at the counter when the first account was opened. A card's date
/// without a time is taken at midnight UTC. <see cref="Client.Id"/> is the number in the code, so snapshots are stable.
/// </summary>
public static class GoldenClients
{
    private static readonly Dictionary<string, Func<Client>> Factories = new()
    {
        ["CL-0001"] = Maria,
        ["CL-0005"] = Radu,
        ["CL-0013"] = Andrei,
        ["CL-0015"] = Ioana,
    };

    public static IReadOnlyCollection<string> Codes => Factories.Keys;

    public static Client Create(string code) =>
        Factories.TryGetValue(code, out var create) ? create() : throw new KeyNotFoundException($"No golden client {code}");

    public static DateTime Midnight(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    // Case 4. Pages 29 b, c; 34; 35-36.
    private static Client Maria() => StandardRoResident(1, "Maria", "Popescu", new(1991, 6, 8), "2910608400319",
        "Bd. Unirii 15, bl. 7, ap. 22", "030823 București", "+40721384516",
        new Account { Iban = "RO19RZBR0000060021458391", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2019, 3, 14), Balance = 18_450.00m, Currency = "RON" });

    // Cases 5a, 5b, 5c: the same standing order, three outcomes.
    private static Client Radu() => StandardRoResident(5, "Radu", "Ionescu", new(1982, 2, 14), "1820214400458",
        "Calea Moșilor 112, ap. 9", "020871 București", "+40726193458", // phone added: the card gives none
        // p. 25 lit. b: balance at 31.12.2025, value added by R5, NEVERIFICAT (not on the card); over 50.000 $ with a margin, no exchange rate in code.
        new Account { Iban = "RO46RZBR0000060019823746", Type = AccountType.Depository, Product = "Cont curent EUR", OpenedOn = new(2017, 5, 2), Balance = 82_500.00m, Currency = "EUR" },
        // Balance and currency added: EUR, so the 450,00 EUR order of 5b reads the same as in 5a.
        new Account { Iban = "RO88RZBR0000069004471852", Type = AccountType.Custodial, Product = "Cont de instrumente financiare", OpenedOn = new(2020, 9, 10), Balance = 31_750.00m, Currency = "EUR" });

    // Cases 13 and 8 (T4b): the trap persona.
    private static Client Andrei() => StandardRoResident(13, "Andrei", "Pop", new(1995, 9, 23), "1950923350125",
        "Str. Mărășești 6, ap. 3", "300086 Timișoara", "+40745210963",
        new Account { Iban = "RO20RZBR0000060033317205", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2021, 4, 6), Balance = 23_870.00m, Currency = "RON" }); // balance added

    // Cases 15 and 1 (T4b). Preexisting on both regimes (p. 20).
    // NEVERIFICAT: the standard self-certification is then dated 2014-02-17, before CRS (2016); see the HANDOFF question.
    private static Client Ioana() => StandardRoResident(15, "Ioana", "Mureșan", new(1988, 4, 11), "2880411120231",
        "Str. Memorandumului 21, ap. 4", "400114 Cluj-Napoca", "+40744518207",
        new Account { Iban = "RO43RZBR0000060007719364", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2014, 2, 17), Balance = 12_640.00m, Currency = "RON" }); // product, balance, currency added

    /// <param name="city">With the postal code, as the card writes it ("030823 București"): the entity has no postal code field.</param>
    private static Client StandardRoResident(int id, string firstName, string lastName, DateOnly birthDate, string cnp,
                                             string line, string city, string phone, params Account[] accounts)
    {
        var firstOpened = accounts.Min(a => a.OpenedOn);
        var signedAt = Midnight(firstOpened.Year, firstOpened.Month, firstOpened.Day);
        return new Client
        {
            Id = id,
            Code = $"CL-{id:0000}",
            FirstName = firstName,
            LastName = lastName,
            BirthDate = birthDate,
            BirthCountry = "RO",
            Cnp = cnp,
            Citizenships = ["RO"],
            // ValidFrom added by R5, not on the card.
            Addresses = [new Address { Kind = AddressKind.Residence, Line = line, City = city, Country = "RO", ValidFrom = firstOpened }],
            Phones = [new Phone { Number = phone, Country = "RO" }],
            Accounts = [.. accounts],
            SelfCertifications =
            [
                new SelfCertification
                {
                    // ReceivedAt added by R5, not on the card.
                    SignedAt = signedAt, ReceivedAt = signedAt, Channel = "ghișeu",
                    Residences = [new TaxResidence { Country = "RO", Tin = cnp }],
                },
            ],
        };
    }
}
