using HackathonStarter.Services;

namespace HackathonStarter.Features.Synthetic;

/// <summary>
/// The 50 synthetic CRS clients behind <c>EvaluationService</c> and /metrics (PLAN 16.1, 16.3): fixed seed, in memory
/// only, never in the database. Mix: 30 with nothing to report, 7 CRS, 3 FATCA, 10 traps.
///
/// Every client is generated from a <see cref="ScenarioTemplate"/>, and the template carries the expected decision and
/// rules, written by hand after the golden cases. Nothing here runs the rule engine: the label is the scenario chosen at
/// generation, otherwise the metric would check itself.
///
/// The records are our own, shaped like PLAN section 7 with the same member names (plus a postal code, the ABA routing
/// number of GOLDEN-CASES.md Î2 and the document's key line), so they map onto the Phase 0 entities by name. Every
/// account is opened from 2016 on (new for both CRS and FATCA). Documents are referenced by file name only; no PDF is
/// generated here.
/// </summary>
public static class SyntheticClients
{
    public const int Seed = 2026;

    /// <summary>The golden cases' moment (GOLDEN-CASES.md, 1); every change here arrives in the three weeks before it.</summary>
    public static readonly DateTime ReferenceTime = new(2026, 10, 28, 7, 0, 0, DateTimeKind.Utc);

    private const string Domestic = "GEN-DOMESTIC";

    public static IReadOnlyList<ScenarioTemplate> Templates { get; } =
    [
        new("domestic-move", ScenarioGroup.Nothing, 6, ScenarioDecision.NoCase, [], [Domestic],
            "fără nimic: mutare în România", "nothing: move within Romania"),
        new("domestic-phone-changed", ScenarioGroup.Nothing, 5, ScenarioDecision.NoCase, [], [Domestic],
            "fără nimic: telefon RO înlocuit cu alt telefon RO", "nothing: Romanian phone replaced by another Romanian phone"),
        new("domestic-phone-added", ScenarioGroup.Nothing, 4, ScenarioDecision.NoCase, [], [Domestic],
            "fără nimic: al doilea telefon RO", "nothing: second Romanian phone"),
        new("domestic-mailing", ScenarioGroup.Nothing, 4, ScenarioDecision.NoCase, [], [Domestic],
            "fără nimic: adresă de corespondență în România", "nothing: mailing address in Romania"),
        new("domestic-order", ScenarioGroup.Nothing, 6, ScenarioDecision.NoCase, [], [Domestic],
            "fără nimic: ordin permanent către un cont din România", "nothing: standing order to a Romanian account"),
        new("domestic-poa", ScenarioGroup.Nothing, 5, ScenarioDecision.NoCase, [], [Domestic],
            "fără nimic: împuternicire pentru o persoană din România", "nothing: power of attorney for a person in Romania"),

        new("crs-move", ScenarioGroup.Crs, 2, ScenarioDecision.Clarification, ["CRS-IND-ADDR"], [],
            "CRS: mutare într-o jurisdicție raportabilă", "CRS: move to a reportable jurisdiction"),
        new("crs-move-phone", ScenarioGroup.Crs, 1, ScenarioDecision.Clarification, ["CRS-IND-ADDR", "CRS-IND-PHONE"], [],
            "CRS: mutare și telefon străin, fără telefon RO", "CRS: move and a foreign phone, no Romanian phone"),
        new("crs-phone", ScenarioGroup.Crs, 1, ScenarioDecision.Clarification, ["CRS-IND-PHONE"], [],
            "CRS: telefon străin în locul telefonului RO", "CRS: foreign phone instead of the Romanian one"),
        new("crs-custodial-order", ScenarioGroup.Crs, 1, ScenarioDecision.Clarification, ["CRS-IND-SO"], [],
            "CRS: ordin permanent din cont de custodie", "CRS: standing order from a custodial account"),
        new("crs-poa", ScenarioGroup.Crs, 1, ScenarioDecision.Clarification, ["CRS-IND-POA"], [],
            "CRS: împuternicire pentru o persoană cu adresă în străinătate", "CRS: power of attorney for a person with a foreign address"),
        new("crs-mailing", ScenarioGroup.Crs, 1, ScenarioDecision.Clarification, ["CRS-IND-MAIL"], [],
            "CRS: adresă de corespondență într-o jurisdicție raportabilă", "CRS: mailing address in a reportable jurisdiction"),

        new("fatca-phone", ScenarioGroup.Fatca, 1, ScenarioDecision.FatcaAlert, ["FATCA-IND-PHONE"], ["CRS-IND-PHONE"],
            "FATCA: telefon SUA lângă telefonul RO", "FATCA: US phone next to the Romanian one"),
        new("fatca-order", ScenarioGroup.Fatca, 1, ScenarioDecision.FatcaAlert, ["FATCA-IND-SO"], ["CRS-IND-SO"],
            "FATCA: ordin permanent din cont curent către SUA", "FATCA: standing order from a current account to the US"),
        new("fatca-citizenship", ScenarioGroup.Fatca, 1, ScenarioDecision.FatcaAlert, ["FATCA-IND-CIT"], [],
            "FATCA: cetățenie SUA (al doilea pașaport)", "FATCA: US citizenship (second passport)"),

        new("trap-depository-order", ScenarioGroup.Trap, 3, ScenarioDecision.NoCase, [], ["CRS-IND-SO"],
            "capcană: ordin din cont de depozit", "trap: standing order from a depository account"),
        new("trap-foreign-phone", ScenarioGroup.Trap, 3, ScenarioDecision.NoCase, [], ["CRS-IND-PHONE"],
            "capcană: telefon străin lângă telefonul RO", "trap: foreign phone next to the Romanian one"),
        // Nationality is not a signal (PLAN 8.2); which rule the engine lists as cleared here is R1's call.
        new("trap-citizenship", ScenarioGroup.Trap, 2, ScenarioDecision.NoCase, [], [],
            "capcană: cetățenie străină, alta decât SUA", "trap: foreign citizenship other than US"),
        new("trap-foreign-salary", ScenarioGroup.Trap, 2, ScenarioDecision.NoCase, [], [Domestic],
            "capcană: salariu de la o firmă străină", "trap: salary from a foreign company"),
    ];

    public static IReadOnlyList<SyntheticClientCase> All { get; } = Generate();

    public static IReadOnlyList<SyntheticClientCase> Generate(int seed = Seed) => new Generator(seed).Run();

    private sealed record Street(string Name, string PostalCode, string City);

    private sealed record Built(string Country, SyntheticClient Before, SyntheticClient After, SyntheticChangeKind Kind, string Summary);

    private sealed class Generator(int seed)
    {
        private static readonly string[] Reportable = ["DE", "AT", "IT", "ES", "FR"];
        private static readonly string[] PhoneChannels = ["online", "ghișeu", "call center"];
        private static readonly string[] OfficeChannels = ["ghișeu", "online"];
        private static readonly string[] PoaScopes = ["operațiuni pe contul curent", "consultare sold și extrase", "operațiuni pe toate conturile"];

        private static readonly Street[] RoStreets =
        [
            new("Calea Victoriei", "010063", "București"), new("Str. Baba Novac", "031622", "București"), new("Bd. Iuliu Maniu", "061103", "București"),
            new("Str. Horea", "400174", "Cluj-Napoca"), new("Calea Dorobanților", "400117", "Cluj-Napoca"), new("Str. Observatorului", "400363", "Cluj-Napoca"),
            new("Str. Lăpușneanu", "700057", "Iași"), new("Bd. Independenței", "700102", "Iași"), new("Str. Sărărie", "700451", "Iași"),
            new("Str. Mărășești", "300086", "Timișoara"), new("Bd. Republicii", "300159", "Timișoara"), new("Calea Șagului", "300516", "Timișoara"),
            new("Bd. Tomis", "900178", "Constanța"), new("Str. Mircea cel Bătrân", "900658", "Constanța"),
            new("Str. Lungă", "500035", "Brașov"), new("Bd. Eroilor", "500030", "Brașov"), new("Str. Zizinului", "500407", "Brașov"),
            new("Calea Unirii", "200409", "Craiova"), new("Str. Brestei", "200581", "Craiova"),
            new("Calea Aradului", "410223", "Oradea"), new("Str. Nufărului", "410582", "Oradea"),
            new("Str. Mihai Viteazu", "550350", "Sibiu"), new("Calea Dumbrăvii", "550324", "Sibiu"),
        ];

        private static readonly Dictionary<string, Street[]> ForeignStreets = new()
        {
            ["DE"] = [new("Schillerstraße", "10625", "Berlin"), new("Rotebühlstraße", "70178", "Stuttgart"), new("Sendlinger Straße", "80331", "München"), new("Hohenzollernring", "50672", "Köln")],
            ["AT"] = [new("Landstraßer Hauptstraße", "1030", "Wien"), new("Herrengasse", "8010", "Graz"), new("Landstraße", "4020", "Linz")],
            ["IT"] = [new("Corso Buenos Aires", "20124", "Milano"), new("Via Nazionale", "00184", "Roma"), new("Via Garibaldi", "10122", "Torino")],
            ["ES"] = [new("Calle de Toledo", "28005", "Madrid"), new("Carrer de Mallorca", "08036", "Barcelona"), new("Calle Colón", "46004", "Valencia")],
            ["FR"] = [new("Rue de Rivoli", "75004", "Paris"), new("Rue de la République", "69002", "Lyon"), new("Boulevard Longchamp", "13001", "Marseille")],
        };

        private static readonly Dictionary<string, string> Landlords = new()
        {
            ["DE"] = "Hausverwaltung Brandt GmbH", ["AT"] = "Hausverwaltung Gruber GmbH", ["IT"] = "Immobiliare Conti S.r.l.",
            ["ES"] = "Inmobiliaria Navarro S.L.", ["FR"] = "Agence Immobilière Morel SARL",
        };

        private static readonly Dictionary<string, string> Employers = new()
        {
            ["DE"] = "Kessler Datentechnik GmbH, Hamburg", ["AT"] = "Alpenwerk Engineering GmbH, Linz", ["IT"] = "Ferraresi Sistemi S.r.l., Bologna",
            ["ES"] = "Ibérica Consultores S.L., Madrid", ["FR"] = "Lefèvre Conseil SAS, Lyon",
        };

        private static readonly Dictionary<string, string> Nationalities = new()
        {
            ["DE"] = "Staatsangehörigkeit / Nationality: DEUTSCH", ["AT"] = "Staatsangehörigkeit / Nationality: ÖSTERREICH",
            ["IT"] = "Cittadinanza / Nationality: ITALIANA", ["ES"] = "Nacionalidad / Nationality: ESPAÑOLA",
            ["FR"] = "Nationalité / Nationality: FRANÇAISE", ["US"] = "Nationality: UNITED STATES OF AMERICA",
        };

        private readonly SyntheticData _data = new(seed);
        private readonly Random _random = new(seed);
        private int _nextReportable;
        private DateTime _at;

        public IReadOnlyList<SyntheticClientCase> Run()
        {
            var scenarios = Templates.SelectMany(t => Enumerable.Repeat(t, t.Count)).ToArray();
            _random.Shuffle(scenarios);
            var cases = new List<SyntheticClientCase>(scenarios.Length);
            for (var i = 0; i < scenarios.Length; i++) cases.Add(Create(scenarios[i], $"CL-{1001 + i}"));
            return cases;
        }

        private SyntheticClientCase Create(ScenarioTemplate scenario, string code)
        {
            _at = ReferenceTime.AddDays(-_random.Next(1, 22)).AddMinutes(_random.Next(0, 600));
            var person = _data.Person();
            var adult = person.BirthDate.AddYears(18);
            var opened = DayBetween(adult > new DateOnly(2016, 1, 4) ? adult : new DateOnly(2016, 1, 4), new DateOnly(2025, 12, 15));
            var signedAt = opened.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc).AddMinutes(_random.Next(0, 540));
            var before = new SyntheticClient(code, person.FirstName, person.LastName, person.BirthDate, "RO", person.Cnp, ["RO"],
                [RoAddress(Pick(RoStreets), SyntheticAddressKind.Residence, opened)],
                [Phone("RO")],
                [new(_data.Iban("RZBR"), SyntheticAccountType.Depository, "Cont curent", opened, Money(200, 60_000), "RON", [])],
                [],
                [new(signedAt, Pick(OfficeChannels), [new("RO", person.Cnp)], DeclaresUsPerson: false)],
                []);

            var built = scenario.Id switch
            {
                "domestic-move" => Move(before, "RO", newPhone: false),
                "domestic-phone-changed" => ChangePhone(before, "RO", replace: true),
                "domestic-phone-added" => ChangePhone(before, "RO", replace: false),
                "domestic-mailing" => AddMailing(before, "RO"),
                "domestic-order" => AddStandingOrder(before, "RO", custodial: false),
                "domestic-poa" => GrantPowerOfAttorney(before, "RO"),
                "crs-move" => Move(before, NextReportable(), newPhone: false),
                "crs-move-phone" => Move(before, NextReportable(), newPhone: true),
                "crs-phone" => ChangePhone(before, NextReportable(), replace: true),
                "crs-custodial-order" => AddStandingOrder(before, NextReportable(), custodial: true),
                "crs-poa" => GrantPowerOfAttorney(before, NextReportable()),
                "crs-mailing" => AddMailing(before, NextReportable()),
                "fatca-phone" => ChangePhone(before, "US", replace: false),
                "fatca-order" => AddStandingOrder(before, "US", custodial: false),
                "fatca-citizenship" => UploadPassport(before, "US"),
                "trap-depository-order" => AddStandingOrder(before, NextReportable(), custodial: false),
                "trap-foreign-phone" => ChangePhone(before, NextReportable(), replace: false),
                "trap-citizenship" => UploadPassport(before, NextReportable()),
                "trap-foreign-salary" => UploadPayslip(before, NextReportable()),
                _ => throw new InvalidOperationException($"No generator for scenario '{scenario.Id}'."),
            };
            var channel = built.Kind switch
            {
                SyntheticChangeKind.PowerOfAttorneyGranted => "ghișeu",
                SyntheticChangeKind.PhoneAdded or SyntheticChangeKind.PhoneChanged => Pick(PhoneChannels),
                _ => Pick(OfficeChannels),
            };
            return new(scenario, built.Country, built.Before, built.After, new SyntheticChange(built.Kind, built.Summary, channel, _at));
        }

        private Built Move(SyntheticClient before, string country, bool newPhone)
        {
            var home = before.Addresses.Single();
            var moved = country == "RO"
                ? RoAddress(Pick(RoStreets.Where(s => s.City != home.City).ToArray()), SyntheticAddressKind.Residence, Since())
                : ForeignAddress(country, SyntheticAddressKind.Residence, Since());
            var after = before with { Addresses = [home with { IsCurrent = false }, moved] };
            var summary = $"Adresă nouă de reședință: {moved.OneLine}";
            if (newPhone)
            {
                var (old, phone) = (before.Phones.Single(), Phone(country));
                after = after with { Phones = [old with { IsCurrent = false }, phone] };
                summary += $"; telefon nou {phone.Number}, care înlocuiește {old.Number}";
            }
            return new(country, before, after, SyntheticChangeKind.AddressChanged, summary);
        }

        private Built ChangePhone(SyntheticClient before, string country, bool replace)
        {
            var (old, phone) = (before.Phones.Single(), Phone(country));
            return replace
                ? new(country, before, before with { Phones = [old with { IsCurrent = false }, phone] },
                    SyntheticChangeKind.PhoneChanged, $"Telefon nou {phone.Number}, care înlocuiește {old.Number}")
                : new(country, before, before with { Phones = [old, phone] },
                    SyntheticChangeKind.PhoneAdded, $"Telefon nou {phone.Number}; {old.Number} rămâne la dosar");
        }

        private Built AddMailing(SyntheticClient before, string country)
        {
            var mailing = country == "RO"
                ? RoAddress(Pick(RoStreets), SyntheticAddressKind.Mailing, Since())
                : ForeignAddress(country, SyntheticAddressKind.Mailing, Since());
            return new(country, before, before with { Addresses = [.. before.Addresses, mailing] },
                SyntheticChangeKind.AddressAdded, $"Adresă de corespondență: {mailing.OneLine}");
        }

        private Built AddStandingOrder(SyntheticClient before, string country, bool custodial)
        {
            var current = before.Accounts.Single();
            if (country == "RO")
            {
                var payee = _random.Next(2) == 0 ? _data.Person().FullName : $"Asociația de Proprietari bl. {(char)('A' + _random.Next(8))}{_random.Next(1, 20)}";
                var local = new SyntheticStandingOrder(payee, _data.Iban(), null, "RO", Money(100, 2_500), "RON", "lunar", true);
                return new(country, before, WithOrder(before, current, local), SyntheticChangeKind.StandingOrderCreated,
                    $"Ordin permanent lunar din {current.Product} către {payee}, {local.BeneficiaryAccount}");
            }

            // Golden cases 5a-5c: foreign orders go out of a second, EUR account (custodial only where the scenario says so).
            var source = custodial
                ? new SyntheticAccount(_data.Iban("RZBR"), SyntheticAccountType.Custodial, "Cont de instrumente financiare", Later(current.OpenedOn), Money(2_000, 150_000), "EUR", [])
                : new SyntheticAccount(_data.Iban("RZBR"), SyntheticAccountType.Depository, "Cont curent EUR", Later(current.OpenedOn), Money(300, 25_000), "EUR", []);
            var start = before with { Accounts = [current, source] };
            var relative = $"{_data.Person().FirstName} {before.LastName}";
            if (country == "US")
            {
                var us = new SyntheticStandingOrder(relative, _random.NextInt64(1_000_000_000, 10_000_000_000).ToString(),
                    _data.AbaRoutingNumber(), "US", Money(100, 800), "EUR", "lunar", true);
                return new(country, start, WithOrder(start, source, us), SyntheticChangeKind.StandingOrderCreated,
                    $"Ordin permanent lunar din {source.Product} către {relative}, cont {us.BeneficiaryAccount}, ABA {us.RoutingNumber} (SUA)");
            }
            var payeeAbroad = _random.Next(2) == 0 ? relative : Landlords[country];
            var order = new SyntheticStandingOrder(payeeAbroad, _data.ForeignIban(country), null, country, Money(150, 1_500), "EUR", "lunar", true);
            return new(country, start, WithOrder(start, source, order), SyntheticChangeKind.StandingOrderCreated,
                $"Ordin permanent lunar din {source.Product} către {payeeAbroad}, {order.BeneficiaryAccount}");
        }

        private Built GrantPowerOfAttorney(SyntheticClient before, string country)
        {
            var holder = _data.Person();
            var name = _random.Next(2) == 0 ? $"{holder.FirstName} {before.LastName}" : holder.FullName;
            var address = country == "RO"
                ? RoAddress(Pick(RoStreets), SyntheticAddressKind.Residence, Since())
                : ForeignAddress(country, SyntheticAddressKind.Residence, Since());
            var poa = new SyntheticPowerOfAttorney(name, address.OneLine, country, Pick(PoaScopes), true, DateOnly.FromDateTime(_at));
            return new(country, before, before with { PowersOfAttorney = [poa] }, SyntheticChangeKind.PowerOfAttorneyGranted,
                $"Împuternicire pentru {name}, {address.OneLine}: {poa.Scope}");
        }

        private Built UploadPassport(SyntheticClient before, string country)
        {
            var file = $"pasaport-{country.ToLowerInvariant()}-{Slug(before.FirstName)}-{before.Code[3..]}.pdf";
            var passport = new SyntheticDocument(SyntheticDocumentKind.Passport, file, Nationalities[country]);
            return new(country, before, before with { Citizenships = ["RO", country], Documents = [passport] },
                SyntheticChangeKind.DocumentUploaded, $"Document încărcat: {file} (al doilea pașaport, {country})");
        }

        // The signal (foreign employer) is in the document only; the bank data does not change.
        private Built UploadPayslip(SyntheticClient before, string country)
        {
            var file = $"fluturas-{Slug(before.FirstName)}-{before.Code[3..]}-2026-09.pdf";
            var payslip = new SyntheticDocument(SyntheticDocumentKind.Payslip, file, $"Angajator: {Employers[country]}");
            return new(country, before, before with { Documents = [payslip] },
                SyntheticChangeKind.DocumentUploaded, $"Document încărcat: {file} (fluturaș de salariu)");
        }

        private static SyntheticClient WithOrder(SyntheticClient client, SyntheticAccount account, SyntheticStandingOrder order) =>
            client with
            {
                Accounts = [.. client.Accounts.Select(a => a.Iban == account.Iban ? a with { StandingOrders = [.. a.StandingOrders, order] } : a)],
            };

        private SyntheticAddress RoAddress(Street street, SyntheticAddressKind kind, DateOnly from) =>
            new(kind, $"{street.Name} {_random.Next(1, 120)}, ap. {_random.Next(1, 80)}", street.PostalCode, street.City, "RO", true, from);

        private SyntheticAddress ForeignAddress(string country, SyntheticAddressKind kind, DateOnly from)
        {
            var street = Pick(ForeignStreets[country]);
            var number = _random.Next(1, 90);
            return new(kind, country == "FR" ? $"{number} {street.Name}" : $"{street.Name} {number}", street.PostalCode, street.City, country, true, from);
        }

        private SyntheticPhone Phone(string country) => new(_data.PhoneE164(country), country, true);

        private string NextReportable() => Reportable[_nextReportable++ % Reportable.Length];

        private DateOnly Since() => DateOnly.FromDateTime(_at).AddDays(-_random.Next(1, 20));

        private DateOnly Later(DateOnly opened) => DayBetween(opened.AddDays(30), DateOnly.FromDateTime(_at).AddDays(-30));

        private DateOnly DayBetween(DateOnly from, DateOnly to) => from.AddDays(_random.Next(0, to.DayNumber - from.DayNumber + 1));

        private decimal Money(int min, int max) => _random.Next(min * 100, max * 100 + 1) / 100m;

        private T Pick<T>(T[] items) => items[_random.Next(items.Length)];

        private static string Slug(string name) => TextMatch.Normalize(name).Replace(' ', '-');
    }
}

public enum ScenarioGroup { Nothing, Crs, Fatca, Trap }

/// <summary>Member names match <c>Decision</c> (PLAN 7.2).</summary>
public enum ScenarioDecision { NoCase, Clarification, FatcaAlert }

/// <summary>A scenario and what it should produce, written by hand (never computed by the engine).</summary>
/// <param name="Hits">Indicia expected uncovered, compared exactly on (RuleId, Jurisdiction) (GOLDEN-CASES.md, 1).</param>
/// <param name="Cleared">Trap rules expected among the cleared ones, compared as a subset.</param>
public sealed record ScenarioTemplate(
    string Id, ScenarioGroup Group, int Count, ScenarioDecision Expected,
    IReadOnlyList<string> Hits, IReadOnlyList<string> Cleared, string Label, string LabelEn);

/// <summary>One generated client: its scenario, the bank data before and after the change, and the change.</summary>
/// <param name="Jurisdiction">The country the scenario plays in: a reportable one for CRS and traps, US for FATCA, RO otherwise.</param>
public sealed record SyntheticClientCase(
    ScenarioTemplate Scenario, string Jurisdiction, SyntheticClient Before, SyntheticClient After, SyntheticChange Change)
{
    public string Code => After.Code;
    public string Label => Scenario.Label;
    public ScenarioDecision Expected => Scenario.Expected;
    public IReadOnlyList<ExpectedRule> ExpectedHits => [.. Scenario.Hits.Select(Rule)];
    public IReadOnlyList<ExpectedRule> ExpectedCleared => [.. Scenario.Cleared.Select(Rule)];

    // GEN-* rules have no jurisdiction; every other rule here is about the scenario's country.
    private ExpectedRule Rule(string id) => new(id, id.StartsWith("GEN-", StringComparison.Ordinal) ? null : Jurisdiction);
}

public sealed record ExpectedRule(string RuleId, string? Jurisdiction);

public sealed record SyntheticClient(
    string Code, string FirstName, string LastName, DateOnly BirthDate, string BirthCountry, string Cnp,
    IReadOnlyList<string> Citizenships, IReadOnlyList<SyntheticAddress> Addresses, IReadOnlyList<SyntheticPhone> Phones,
    IReadOnlyList<SyntheticAccount> Accounts, IReadOnlyList<SyntheticPowerOfAttorney> PowersOfAttorney,
    IReadOnlyList<SyntheticSelfCertification> SelfCertifications, IReadOnlyList<SyntheticDocument> Documents)
{
    public string FullName => $"{FirstName} {LastName}";
}

public enum SyntheticAddressKind { Residence, Mailing }

public sealed record SyntheticAddress(
    SyntheticAddressKind Kind, string Line, string PostalCode, string City, string Country, bool IsCurrent, DateOnly ValidFrom)
{
    public string OneLine => $"{Line}, {PostalCode} {City}, {Country}";
}

/// <param name="Number">E.164.</param>
public sealed record SyntheticPhone(string Number, string Country, bool IsCurrent);

public enum SyntheticAccountType { Depository, Custodial }

public sealed record SyntheticAccount(
    string Iban, SyntheticAccountType Type, string Product, DateOnly OpenedOn, decimal Balance, string Currency,
    IReadOnlyList<SyntheticStandingOrder> StandingOrders);

/// <param name="BeneficiaryAccount">IBAN, or the account number in the US, which has no IBAN (GOLDEN-CASES.md, Î2).</param>
/// <param name="RoutingNumber">ABA routing number, US only.</param>
/// <param name="BeneficiaryCountry">Set explicitly, not derived from the IBAN.</param>
public sealed record SyntheticStandingOrder(
    string BeneficiaryName, string BeneficiaryAccount, string? RoutingNumber, string BeneficiaryCountry,
    decimal Amount, string Currency, string Frequency, bool IsActive);

public sealed record SyntheticPowerOfAttorney(
    string HolderName, string HolderAddress, string HolderCountry, string Scope, bool IsActive, DateOnly ValidFrom);

/// <summary>Valid, signed at the opening of the first account: Romania, TIN = CNP (the "standard RO resident").</summary>
public sealed record SyntheticSelfCertification(
    DateTime SignedAt, string Channel, IReadOnlyList<SyntheticTaxResidence> Residences, bool DeclaresUsPerson);

public sealed record SyntheticTaxResidence(string Country, string Tin);

public enum SyntheticDocumentKind { Passport, Payslip }

/// <param name="KeyLine">The line that matters, as the document shows it: ground truth for extraction and signals.</param>
public sealed record SyntheticDocument(SyntheticDocumentKind Kind, string FileName, string KeyLine);

public enum SyntheticChangeKind
{
    AddressChanged, AddressAdded, PhoneAdded, PhoneChanged, StandingOrderCreated, PowerOfAttorneyGranted, DocumentUploaded,
}

public sealed record SyntheticChange(SyntheticChangeKind Kind, string Summary, string Channel, DateTime ReceivedAt);
