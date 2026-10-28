using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Evaluation;

/// <summary>
/// The golden cases' clients as they are BEFORE the change (docs/GOLDEN-CASES.md, section 4), built once per client
/// code: the golden cases take their snapshots from here (<see cref="ClientSnapshot.From"/>), the seed can insert the
/// same entities. Every call returns new entities (EF tracks and mutates them). Values come from the cards; those a card
/// does not give (balances, products, a phone, a street in RO) are marked "added". Owner: R5.
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
        ["CL-0002"] = Cristina,
        ["CL-0003"] = Bogdan,
        ["CL-0005"] = Radu,
        ["CL-0006"] = Alexandra,
        ["CL-0007"] = Marius,
        ["CL-0009"] = Sorin,
        ["CL-0010"] = Victor,
        ["CL-0011"] = Doina,
        ["CL-0012"] = Petru,
        ["CL-0013"] = Andrei,
        ["CL-0014"] = Lucian,
        ["CL-0015"] = Ioana,
        ["CL-0016"] = Irina,
        ["CL-0017"] = Gheorghe,
        ["CL-0018"] = Teodora,
    };

    public static IReadOnlyCollection<string> Codes => Factories.Keys;

    public static Client Create(string code) =>
        Factories.TryGetValue(code, out var create) ? create() : throw new KeyNotFoundException($"No golden client {code}");

    public static DateTime Midnight(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    // Case 4. Pages 29 b, c; 34; 35-36.
    private static Client Maria() => StandardRoResident(1, "Maria", "Popescu", new(1991, 6, 8), "2910608400319",
        "Bd. Unirii 15, bl. 7, ap. 22", "030823 București", "+40721384516",
        new Account { Iban = "RO19RZBR0000060021458391", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2019, 3, 14), Balance = 18_450.00m, Currency = "RON" });

    // Case 2: not a standard RO resident (the card, Before). The bank has only the Milan address, from the online opening:
    // no RO address or phone, no self-certification on file; the one she submits is the change. Citizenship [RO] added.
    private static Client Cristina()
    {
        var client = StandardRoResident(2, "Cristina", "Dobre", new(1990, 1, 1), "2900101405128", "Via Padova 18", "20127 Milano", "",
            new Account { Iban = "RO59RZBR0000060041172630", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2026, 10, 27), Balance = 1_500.00m, Currency = "RON" }); // product, balance, currency added
        client.Addresses[0].Country = "IT";
        client.Phones.Clear();
        client.SelfCertifications.Clear();
        return client;
    }

    // Case 3. He has moved to Graz: only the new self-certification says so.
    // ÎNTREBARE (the card): what Before has is not decided; for now a standard RO resident in Brașov.
    private static Client Bogdan() => StandardRoResident(3, "Bogdan", "Lazăr", new(1987, 5, 22), "1870522083340",
        "Str. Republicii 27, ap. 2", "500030 Brașov", "+40756230418", // address and phone added
        new Account { Iban = "RO57RZBR0000060052208841", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2026, 10, 26), Balance = 2_300.00m, Currency = "RON" }); // product, balance, currency added

    // Cases 5a, 5b, 5c: the same standing order, three outcomes.
    private static Client Radu() => StandardRoResident(5, "Radu", "Ionescu", new(1982, 2, 14), "1820214400458",
        "Calea Moșilor 112, ap. 9", "020871 București", "+40726193458", // phone added: the card gives none
        // Over 50.000 USD, also at 31.12.2025 (the card, Î9, p. 25 lit. b): with a margin, so no exchange rate in code.
        new Account { Iban = "RO46RZBR0000060019823746", Type = AccountType.Depository, Product = "Cont curent EUR", OpenedOn = new(2017, 5, 2), Balance = 82_500.00m, Currency = "EUR" },
        // Balance and currency added: EUR, so the 450,00 EUR order of 5b reads the same as in 5a.
        new Account { Iban = "RO88RZBR0000069004471852", Type = AccountType.Custodial, Product = "Cont de instrumente financiare", OpenedOn = new(2020, 9, 10), Balance = 31_750.00m, Currency = "EUR" });

    // Case 6. The place of birth is unknown until the passport arrives.
    private static Client Alexandra()
    {
        var client = StandardRoResident(6, "Alexandra", "Vlad", new(1993, 7, 19), "2930719400974",
            "Str. Avram Iancu 14, ap. 5", "400083 Cluj-Napoca", "+40729845163", // address and phone added
            // Over 50.000 USD, also at 31.12.2025 (the card, Î9, p. 25 lit. b), as Radu's.
            new Account { Iban = "RO34RZBR0000060063391027", Type = AccountType.Depository, Product = "Cont curent EUR", OpenedOn = new(2016, 8, 4), Balance = 78_400.00m, Currency = "EUR" });
        client.BirthCountry = null;
        return client;
    }

    // Case 7. Born in Brașov.
    private static Client Marius() => StandardRoResident(7, "Marius", "Stoica", new(1979, 3, 5), "1790305082613",
        "Bd. Eroilor 9, ap. 11", "500007 Brașov", "+40768120354", // address and phone added
        // Over 50.000 USD, also at 31.12.2025 (the card, Î9, p. 25 lit. b), as Radu's.
        new Account { Iban = "RO76RZBR0000060074415582", Type = AccountType.Depository, Product = "Cont curent EUR", OpenedOn = new(2018, 11, 12), Balance = 76_900.00m, Currency = "EUR" });

    // Case 9. The card's self-certification (dual residence, online) replaces the standard one.
    private static Client Sorin()
    {
        var client = StandardRoResident(9, "Sorin", "Matei", new(1984, 9, 30), "1840930081451",
            "Str. Lungă 40", "500035 Brașov", "+40726447031",
            new Account { Iban = "RO87RZBR0000060085527316", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2019, 1, 21), Balance = 21_480.00m, Currency = "RON" }); // product, balance, currency added
        var signedAt = Midnight(2025, 2, 3);
        client.SelfCertifications =
        [
            new SelfCertification
            {
                SignedAt = signedAt, ReceivedAt = signedAt, Channel = "online",
                Residences = [new TaxResidence { Country = "RO", Tin = client.Cnp }, new TaxResidence { Country = "DE", Tin = "47302619546" }],
            },
        ];
        return client;
    }

    // Case 10. The balance is the one on his statement (extras-victor-2026-09.pdf, 30.09.2026).
    private static Client Victor() => StandardRoResident(10, "Victor", "Neagu", new(1976, 8, 12), "1760812403886",
        "Str. Polonă 33, ap. 8", "010494 București", "+40733418920", // address and phone added
        new Account { Iban = "RO84RZBR0000060096630475", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2017, 3, 20), Balance = 15_491.20m, Currency = "RON" }); // product, OpenedOn added

    // Case 11. Born in Iași.
    private static Client Doina() => StandardRoResident(11, "Doina", "Cazacu", new(1958, 1, 17), "2580117222041",
        "Str. Lăpușneanu 20, ap. 3", "700057 Iași", "+40745902617", // address and phone added
        new Account { Iban = "RO56RZBR0000060107741068", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2018, 6, 5), Balance = 27_310.00m, Currency = "RON" }); // product, balance, currency added

    // Case 12. Preexisting on both regimes, low value (p. 31); no phone on file.
    // NEVERIFICAT: the standard self-certification is then dated 2011-05-16, before CRS (2016), as Ioana's.
    private static Client Petru()
    {
        var client = StandardRoResident(12, "Petru", "Sârbu", new(1961, 4, 3), "1610403121731",
            "Str. Horea 5", "400174 Cluj-Napoca", "",
            new Account { Iban = "RO20RZBR0000060118852290", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2011, 5, 16), Balance = 42_300.00m, Currency = "RON" }); // product added
        client.Phones.Clear();
        return client;
    }

    // Cases 13 and 8 (T4b): the trap persona.
    private static Client Andrei() => StandardRoResident(13, "Andrei", "Pop", new(1995, 9, 23), "1950923350125",
        "Str. Mărășești 6, ap. 3", "300086 Timișoara", "+40745210963",
        new Account { Iban = "RO20RZBR0000060033317205", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2021, 4, 6), Balance = 23_870.00m, Currency = "RON" }); // balance added

    // Case 14. Born in Timișoara.
    private static Client Lucian() => StandardRoResident(14, "Lucian", "Toma", new(1991, 2, 26), "1910226354122",
        "Bd. Revoluției din 1989 7, ap. 15", "300054 Timișoara", "+40752318640", // address added
        // Over 50.000 USD, also at 31.12.2025 (the card, Î9, p. 25 lit. b), as Radu's.
        new Account { Iban = "RO27RZBR0000060129963514", Type = AccountType.Depository, Product = "Cont curent EUR", OpenedOn = new(2020, 2, 11), Balance = 81_250.00m, Currency = "EUR" });

    // Cases 15 and 1 (T4b). Preexisting on both regimes (p. 20).
    // NEVERIFICAT: the standard self-certification is then dated 2014-02-17, before CRS (2016); see the HANDOFF question.
    private static Client Ioana() => StandardRoResident(15, "Ioana", "Mureșan", new(1988, 4, 11), "2880411120231",
        "Str. Memorandumului 21, ap. 4", "400114 Cluj-Napoca", "+40744518207",
        new Account { Iban = "RO43RZBR0000060007719364", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2014, 2, 17), Balance = 12_640.00m, Currency = "RON" }); // product, balance, currency added

    // Cases 16 and 17a: the same change; 17a only ticks later. The card places the old address in București.
    private static Client Irina() => StandardRoResident(16, "Irina", "Constantin", new(1986, 6, 14), "2860614402290",
        "Str. Icoanei 17, ap. 4", "020452 București", "+40723655190", // street added
        new Account { Iban = "RO06RZBR0000060131074639", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2020, 6, 15), Balance = 19_860.00m, Currency = "RON" }); // product, balance, currency added

    // Case 17b. Preexisting on both regimes. NEVERIFICAT: the standard self-certification is then dated 2012-09-03, before CRS.
    // Low value, under 1.000.000 USD (the card, N2): high-value preexisting accounts are outside the MVP.
    private static Client Gheorghe() => StandardRoResident(17, "Gheorghe", "Marin", new(1967, 9, 21), "1670921403155",
        "Șos. Panduri 35, ap. 19", "050657 București", "+40722180944", // address added
        new Account { Iban = "RO03RZBR0000060142185752", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2012, 9, 3), Balance = 33_540.00m, Currency = "RON" }); // product added

    // Case 18. The card places the old address in București.
    private static Client Teodora() => StandardRoResident(18, "Teodora", "Iliescu", new(2004, 5, 2), "6040502401181",
        "Str. Olari 9, ap. 2", "024057 București", "+40731904226", // street added
        new Account { Iban = "RO38RZBR0000060153296860", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = new(2022, 9, 1), Balance = 3_270.00m, Currency = "RON" }); // product, balance, currency added

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
