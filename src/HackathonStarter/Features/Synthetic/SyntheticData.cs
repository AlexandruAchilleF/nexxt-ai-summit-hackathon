using System.Numerics;

namespace HackathonStarter.Features.Synthetic;

/// <summary>
/// Realistic, VALID test data for Romania: CNPs, IBANs, card numbers (Luhn), CUIs and phone numbers pass the
/// same checksums the app uses, so masking, checks and validations behave exactly as with real data.
/// Use it for seed data, demos, attack scenarios and tests, never real customers' data.
///
/// Deterministic: the same seed always produces the same people and companies (repeatable tests and videos).
///
/// Caveat: a valid CNP, IBAN or phone number can coincide with a real person's. Emails use example.com
/// (reserved, never delivered) and cards use the 400000 test prefix. Don't present generated values as
/// belonging to anyone real.
/// </summary>
public sealed partial class SyntheticData(int seed = 42)
{
    private readonly Random _random = new(seed);

    private static readonly string[] MaleNames = ["Andrei", "Mihai", "Alexandru", "Ion", "Cristian", "Bogdan", "Radu", "Vlad", "Stefan", "Gabriel", "Adrian", "Florin"];
    private static readonly string[] FemaleNames = ["Maria", "Elena", "Ioana", "Andreea", "Ana", "Cristina", "Alexandra", "Diana", "Raluca", "Simona", "Laura", "Bianca"];
    private static readonly string[] LastNames = ["Popescu", "Ionescu", "Popa", "Dumitru", "Stan", "Stoica", "Gheorghe", "Matei", "Ciobanu", "Rusu", "Munteanu", "Constantin", "Marin", "Dinu", "Tudor", "Florea"];
    private static readonly string[] CompanyWords = ["Nord", "Delta", "Carpat", "Danubius", "Orizont", "Atlas", "Vector", "Sigma", "Lumina", "Arcadia", "Meridian", "Teleor"];
    private static readonly string[] CompanyFields = ["Logistic", "Construct", "Agro", "Soft", "Trading", "Consult", "Medical", "Energy", "Retail", "Design"];
    private static readonly string[] Cities = ["București", "Cluj-Napoca", "Iași", "Timișoara", "Constanța", "Brașov", "Craiova", "Oradea"];

    /// <summary>Bank codes as they appear in Romanian IBANs (realistic looking demos).</summary>
    public static readonly string[] BankCodes = ["RZBR", "BTRL", "RNCB", "BRDE", "INGB", "BACX", "CECE", "UGBI"];

    /// <summary>Romanian county codes used in CNPs (01–46, plus 51–52 for Călărași and Giurgiu).</summary>
    private static readonly int[] Counties = [.. Enumerable.Range(1, 46), 51, 52];

    public SyntheticPerson Person()
    {
        var male = _random.Next(2) == 0;
        var first = Pick(male ? MaleNames : FemaleNames);
        var last = Pick(LastNames);
        var birth = new DateOnly(1950, 1, 1).AddDays(_random.Next(0, 365 * 55)); // born 1950–2004
        return new SyntheticPerson(first, last, birth, male,
            Cnp(birth, male), Iban(), Card(), Phone(), Email(first, last), Pick(Cities));
    }

    public List<SyntheticPerson> People(int count) => [.. Enumerable.Range(0, count).Select(_ => Person())];

    public SyntheticCompany Company()
    {
        var name = $"{Pick(CompanyWords)} {Pick(CompanyFields)} SRL";
        var founded = 1995 + _random.Next(0, 30);
        var county = _random.Next(1, 41);
        return new SyntheticCompany(name, Cui(), $"J{county:00}/{_random.Next(100, 99999)}/{founded}", founded, Iban(), Pick(Cities));
    }

    public List<SyntheticCompany> Companies(int count) => [.. Enumerable.Range(0, count).Select(_ => Company())];

    /// <summary>
    /// CNP: S YYMMDD JJ NNN C. S = 1/2 (born 1900–1999, male/female), 5/6 (2000–2099); JJ = county;
    /// C = checksum with weights 279146358279 (the one <c>PiiVault.IsValidCnp</c> verifies).
    /// </summary>
    public string Cnp(DateOnly? birth = null, bool? male = null, int? county = null)
    {
        var date = birth ?? new DateOnly(1950, 1, 1).AddDays(_random.Next(0, 365 * 55));
        var isMale = male ?? _random.Next(2) == 0;
        var sex = date.Year >= 2000 ? (isMale ? 5 : 6) : (isMale ? 1 : 2);
        var body = $"{sex}{date:yyMMdd}{county ?? Pick(Counties):00}{_random.Next(1, 1000):000}";
        return body + CnpChecksum(body);
    }

    public static int CnpChecksum(string first12)
    {
        const string weights = "279146358279";
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (first12[i] - '0') * (weights[i] - '0');
        return sum % 11 == 10 ? 1 : sum % 11;
    }

    /// <summary>Romanian IBAN (24 characters, mod-97 check digits), e.g. RO66RZBR0000060012345678.</summary>
    public string Iban(string? bankCode = null)
    {
        var bank = (bankCode ?? Pick(BankCodes)).ToUpperInvariant();
        var account = string.Concat(Enumerable.Range(0, 16).Select(_ => (char)('0' + _random.Next(10))));
        return IbanWithCheckDigits("RO", bank + account);
    }

    public static string IbanWithCheckDigits(string country, string bban)
    {
        var rearranged = bban + country + "00";
        var digits = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));
        var check = 98 - (int)(BigInteger.Parse(digits) % 97);
        return $"{country}{check:00}{bban}";
    }

    /// <summary>16-digit card number on the 400000 test prefix, valid Luhn checksum.</summary>
    public string Card()
    {
        var body = "400000" + string.Concat(Enumerable.Range(0, 9).Select(_ => (char)('0' + _random.Next(10))));
        return body + LuhnCheckDigit(body);
    }

    public static int LuhnCheckDigit(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var d = body[body.Length - 1 - i] - '0';
            if (i % 2 == 0) { d *= 2; if (d > 9) d -= 9; } // doubled from the right, check digit not yet appended
            sum += d;
        }
        return (10 - sum % 10) % 10;
    }

    /// <summary>CUI (company fiscal code) with its control digit (key 753217532).</summary>
    public string Cui()
    {
        var body = _random.Next(10_000_000, 49_999_999).ToString();
        return body + CuiControlDigit(body);
    }

    public static int CuiControlDigit(string body)
    {
        const string key = "753217532";
        var padded = body.PadLeft(9, '0');
        var sum = 0;
        for (var i = 0; i < 9; i++) sum += (padded[i] - '0') * (key[i] - '0');
        var control = sum * 10 % 11;
        return control == 10 ? 0 : control;
    }

    /// <summary>Romanian mobile number: "07xx xxx xxx".</summary>
    public string Phone() => $"07{_random.Next(20, 90)} {_random.Next(100, 1000)} {_random.Next(100, 1000)}";

    /// <summary>"maria.popescu17@example.com" (example.com is reserved: mail is never delivered).</summary>
    public string Email(string first, string last) =>
        $"{Ascii(first)}.{Ascii(last)}{_random.Next(1, 100)}@example.com";

    private T Pick<T>(T[] items) => items[_random.Next(items.Length)];

    private static string Ascii(string s) => Services.TextMatch.Normalize(s).Replace(' ', '-');
}

public record SyntheticPerson(
    string FirstName, string LastName, DateOnly BirthDate, bool Male,
    string Cnp, string Iban, string Card, string Phone, string Email, string City)
{
    public string FullName => $"{FirstName} {LastName}";
}

public record SyntheticCompany(string Name, string Cui, string RegistrationNumber, int Founded, string Iban, string City);
