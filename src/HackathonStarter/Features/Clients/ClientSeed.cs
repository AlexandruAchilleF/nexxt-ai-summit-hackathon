using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;
using UglyToad.PdfPig;

namespace HackathonStarter.Features.Clients;

/// <summary>
/// Demo clients, all synthetic: the golden cases' clients exactly as on their cards (docs/GOLDEN-CASES.md), then ordinary
/// RO residents up to <see cref="DemoClientCount"/>. AppDbContext seeds whatever this returns into an empty table. Owner: R5.
/// The 50 clients of <see cref="SyntheticClients"/> stay in memory (metrics) and are not seeded.
/// </summary>
public static class ClientSeed
{
    /// <summary>The most clients the demo database holds.</summary>
    public const int DemoClientCount = 25;

    /// <summary>Fixed, so the filler clients are the same on every start.</summary>
    public const int FillerSeed = 2027;

    /// <summary>Filler codes start at CL-2001: clear of the golden CL-00nn and of the synthetic CL-1001..CL-1050.</summary>
    public const int FillerFirstNumber = 2001;

    // Everyone on a golden card (GOLDEN-CASES.md, 2), transcribed or not: filler names stay clear of them, so the
    // filler does not change when a card is transcribed and a search for "Maria" or "Pop" finds only the golden client.
    private static readonly string[] GoldenPeople =
    [
        "Maria Popescu", "Cristina Dobre", "Bogdan Lazăr", "Radu Ionescu", "Alexandra Vlad", "Marius Stoica", "Andrei Pop",
        "Sorin Matei", "Victor Neagu", "Doina Cazacu", "Petru Sârbu", "Lucian Toma", "Ioana Mureșan", "Irina Constantin",
        "Gheorghe Marin", "Teodora Iliescu",
    ];

    private sealed record Street(string Name, string PostalCode);

    // Real streets and postal codes, none of them a golden client's address.
    private static readonly Dictionary<string, Street[]> Streets = new()
    {
        ["București"] = [new("Calea Victoriei", "010063"), new("Str. Baba Novac", "031622"), new("Bd. Iuliu Maniu", "061103")],
        ["Cluj-Napoca"] = [new("Str. Horea", "400174"), new("Calea Dorobanților", "400117")],
        ["Iași"] = [new("Str. Lăpușneanu", "700057"), new("Bd. Independenței", "700102")],
        ["Timișoara"] = [new("Bd. Republicii", "300159"), new("Calea Șagului", "300516")],
        ["Constanța"] = [new("Bd. Tomis", "900178"), new("Str. Mircea cel Bătrân", "900658")],
        ["Brașov"] = [new("Str. Lungă", "500035"), new("Bd. Eroilor", "500030")],
        ["Craiova"] = [new("Calea Unirii", "200409"), new("Str. Brestei", "200581")],
        ["Oradea"] = [new("Calea Aradului", "410223"), new("Str. Nufărului", "410582")],
    };

    private static readonly DateOnly FirstOpening = new(2016, 1, 4), LastOpening = new(2025, 12, 15);

    /// <param name="now">The demo date (ISimClock) at startup: the documents' UploadedAt. No other date depends on it.</param>
    public static List<Client> Create(DateTime now)
    {
        var golden = Golden(now);
        return [.. golden, .. Filler(Math.Max(0, DemoClientCount - golden.Count))];
    }

    /// <summary>
    /// One client per code of the golden cases that have input (CL-0013 serves 8 and 13, CL-0015 serves 1 and 15), from
    /// <see cref="GoldenClients.Create"/>, so a case transcribed later is seeded with no change here. Each gets the documents
    /// its cards list (<see cref="GoldenCase.Documents"/>) that <see cref="SyntheticPdfs"/> generates; an answer's document
    /// (<see cref="GoldenResponse.Document"/>) is not on file yet: it arrives during the demo.
    /// </summary>
    private static List<Client> Golden(DateTime now)
    {
        var codes = GoldenCases.All.Where(c => c.HasInput).Select(c => c.ClientCode).Distinct().Order(StringComparer.Ordinal);
        return [.. codes.Select(code =>
        {
            var client = GoldenClients.Create(code);
            var files = GoldenCases.All.Where(c => c.ClientCode == code).SelectMany(c => c.Documents).DistinctBy(d => d.FileName)
                .Where(d => SyntheticPdfs.FileNames.Contains(d.FileName));
            client.Documents.AddRange(files.Select(d => Document(SyntheticPdfs.Get(d.FileName), d.Kind, now)));
            return client;
        })];
    }

    /// <summary>
    /// Ordinary RO residents with nothing to report (the "standard RO resident" of GOLDEN-CASES.md, 1): residence and mobile
    /// in RO, citizenship [RO], a current account opened from 2016 on (new for CRS and FATCA), sometimes a savings account,
    /// a Valid self-certification [RO, TIN = CNP] signed at the counter. The first <paramref name="count"/> of one fixed
    /// sequence, so a longer list starts with the shorter one.
    /// </summary>
    public static List<Client> Filler(int count)
    {
        var data = new SyntheticData(FillerSeed);
        var random = new Random(FillerSeed + 1);
        var reserved = GoldenPeople.Concat(GoldenClients.Codes.Select(code => GoldenClients.Create(code).FullName))
            .SelectMany(n => n.Split(' ')).Select(TextMatch.Normalize).ToHashSet();
        var taken = new HashSet<string>();
        var clients = new List<Client>(count);

        while (clients.Count < count)
        {
            var person = data.Person();
            if (Clashes(person.FirstName, reserved) || Clashes(person.LastName, reserved) || !taken.Add(person.FullName)) continue;

            var adult = person.BirthDate.AddYears(18);
            var opened = Day(random, adult > FirstOpening ? adult : FirstOpening, LastOpening);
            var signedAt = opened.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc).AddMinutes(random.Next(0, 480));
            var street = Streets[person.City][random.Next(Streets[person.City].Length)];
            var number = FillerFirstNumber + clients.Count;
            var client = new Client
            {
                Id = number,
                Code = $"CL-{number}",
                FirstName = person.FirstName,
                LastName = person.LastName,
                BirthDate = person.BirthDate,
                BirthCountry = "RO",
                Cnp = person.Cnp,
                Citizenships = ["RO"],
                Addresses =
                [
                    new Address
                    {
                        Kind = AddressKind.Residence, Line = $"{street.Name} {random.Next(1, 120)}, ap. {random.Next(1, 80)}",
                        City = $"{street.PostalCode} {person.City}", Country = "RO", ValidFrom = opened,
                    },
                ],
                Phones = [new Phone { Number = data.PhoneE164("RO"), Country = "RO" }],
                Accounts =
                [
                    new Account { Iban = data.Iban("RZBR"), Type = AccountType.Depository, Product = "Cont curent", OpenedOn = opened, Balance = Money(random, 300, 45_000), Currency = "RON" },
                ],
                SelfCertifications =
                [
                    new SelfCertification
                    {
                        SignedAt = signedAt, ReceivedAt = signedAt, Channel = "ghișeu",
                        Residences = [new TaxResidence { Country = "RO", Tin = person.Cnp }],
                    },
                ],
            };
            if (random.Next(3) == 0 && opened < LastOpening)
                client.Accounts.Add(new Account
                {
                    Iban = data.Iban("RZBR"), Type = AccountType.Depository, Product = "Cont de economii",
                    OpenedOn = Day(random, opened.AddDays(1), LastOpening), Balance = Money(random, 1_000, 90_000), Currency = "RON",
                });
            clients.Add(client);
        }
        return clients;
    }

    private static ClientDocument Document(SyntheticPdf pdf, DocumentKind kind, DateTime now)
    {
        using var parsed = PdfDocument.Open(pdf.Bytes);
        return new ClientDocument { Kind = kind, FileName = pdf.FileName, Content = pdf.Bytes, Pages = parsed.NumberOfPages, UploadedAt = now };
    }

    /// <summary>Equal to a golden name, or one inside the other ("Ion" in "Ionescu", "Stan" in "Constantin").</summary>
    private static bool Clashes(string name, HashSet<string> reserved)
    {
        var n = TextMatch.Normalize(name);
        return reserved.Any(r => r.Contains(n, StringComparison.Ordinal) || n.Contains(r, StringComparison.Ordinal));
    }

    private static DateOnly Day(Random random, DateOnly from, DateOnly to) => from.AddDays(random.Next(0, to.DayNumber - from.DayNumber + 1));

    private static decimal Money(Random random, int min, int max) => random.Next(min * 100, max * 100 + 1) / 100m;
}
