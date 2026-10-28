using HackathonStarter.Services;

namespace HackathonStarter.Features.Synthetic;

/// <summary>
/// Foreign identifiers for CRS/FATCA scenarios: TINs, E.164 mobile numbers, IBANs and ABA routing numbers, each in
/// the published format and with its check digit or character where one exists. "Format valid" only: nothing says
/// a tax office or a bank ever issued the value.
///
/// Structure: OECD TIN sheets. Check digits: TIN on Europa accepts exactly the digits computed here (probed
/// 2026-10-28) for DE, AT, ES, FR and IT. The OECD sheets give no check algorithm for DE, AT, ES and FR, and the
/// IT structure is only an image: for those parts, the source is TIN on Europa, not the OECD (NEVERIFICAT there).
/// US TINs come only from 987-65-4320…4329 (reserved for examples), US phones only from 555-0100…0199.
/// </summary>
public sealed partial class SyntheticData
{
    public static readonly string[] TinCountries = ["RO", "DE", "IT", "ES", "FR", "AT", "US"];
    public static readonly string[] PhoneCountries = ["RO", "DE", "AT", "IT", "ES", "FR", "US"];
    public static readonly string[] IbanCountries = ["DE", "AT", "IT", "ES", "FR"];

    private const string ItalianMonths = "ABCDEHLMPRST";
    private const string SpanishLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    // Odd positions (1st, 3rd, …) of a codice fiscale or an Italian CIN; digits 0–9 count as A–J.
    private static readonly int[] ItalianOddValues = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];

    private static readonly string[] DeBankCodes = ["10010010", "12030000", "37040044", "50010517", "70010080"];
    private static readonly string[] AtBankCodes = ["12000", "14000", "19043", "20111"];
    private static readonly string[] ItBankCodes = ["02008", "03069", "05034", "05428"];
    private static readonly string[] EsBankCodes = ["0049", "0081", "0182", "2085", "2100"];
    private static readonly string[] FrBankCodes = ["20041", "30002", "30003", "30004", "30006"];
    private static readonly string[] AtMobilePrefixes = ["650", "660", "664", "676", "699"];
    private static readonly string[] UsAreaCodes = ["202", "212", "305", "312", "415", "617"];

    /// <summary>TIN in its compact form (no spaces, hyphens or slashes). RO: the CNP.</summary>
    public string Tin(string country) => country.ToUpperInvariant() switch
    {
        "RO" => Cnp(),
        "DE" => DeTin(),
        "IT" => ItTin(),
        "ES" => EsTin(),
        "FR" => FrTin(),
        "AT" => AtTin(),
        "US" => UsTin(),
        _ => throw new ArgumentException($"No TIN generator for '{country}'.", nameof(country)),
    };

    /// <summary>
    /// A TIN that is wrong on purpose (case 2: codice fiscale without its control character). US TINs have no
    /// check digit, so both flaws drop the last digit.
    /// </summary>
    public string WrongTin(string country, TinFlaw flaw = TinFlaw.MissingCheck)
    {
        country = country.ToUpperInvariant();
        var tin = Tin(country);
        if (flaw == TinFlaw.MissingCheck || country == "US") return tin[..^1];
        var alphabet = char.IsDigit(tin[^1]) ? "0123456789" : country == "ES" ? SpanishLetters : "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var others = alphabet.Where(c => c != tin[^1]).ToArray();
        return tin[..^1] + others[_random.Next(others.Length)];
    }

    /// <summary>
    /// German IdNr: 11 digits, the first not 0. In the first 10, one digit appears twice and the others at most once
    /// (the office also issues one digit three times, never three in a row); the last is ISO 7064 MOD 11,10.
    /// </summary>
    public string DeTin()
    {
        var digits = Enumerable.Range(0, 10).ToList();
        Shuffle(digits);
        digits[9] = digits[_random.Next(9)];
        Shuffle(digits);
        if (digits[0] == 0)
        {
            var i = digits.FindIndex(d => d != 0);
            (digits[0], digits[i]) = (digits[i], digits[0]);
        }
        var body = string.Concat(digits);
        return body + Mod11_10CheckDigit(body);
    }

    public static int Mod11_10CheckDigit(string digits)
    {
        var product = 10;
        foreach (var c in digits)
        {
            var sum = (c - '0' + product) % 10;
            product = (sum == 0 ? 10 : sum) * 2 % 11;
        }
        return (11 - product) % 10;
    }

    /// <summary>Codice fiscale of a random person born in Romania.</summary>
    public string ItTin()
    {
        var male = _random.Next(2) == 0;
        var birth = new DateOnly(1950, 1, 1).AddDays(_random.Next(0, 365 * 55));
        return CodiceFiscale(Pick(LastNames), Pick(male ? MaleNames : FemaleNames), birth, male);
    }

    /// <summary>
    /// Italian codice fiscale, 16 characters: surname (3) + name (3) + year (2) + month letter + day (+40 for women)
    /// + place of birth ("Z129" = Romania) + control character. Same person, same code.
    /// </summary>
    public static string CodiceFiscale(string lastName, string firstName, DateOnly birth, bool male, string birthPlace = "Z129")
    {
        var body = NameCode(lastName, isFirstName: false) + NameCode(firstName, isFirstName: true)
            + $"{birth:yy}{ItalianMonths[birth.Month - 1]}{birth.Day + (male ? 0 : 40):00}{birthPlace.ToUpperInvariant()}";
        return body + ItalianControlCharacter(body);
    }

    /// <summary>Control character of a codice fiscale; the same algorithm gives the CIN of an Italian IBAN.</summary>
    public static char ItalianControlCharacter(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var c = char.ToUpperInvariant(body[i]);
            var value = char.IsDigit(c) ? c - '0' : c - 'A';
            sum += i % 2 == 0 ? ItalianOddValues[value] : value;
        }
        return (char)('A' + sum % 26);
    }

    private static string NameCode(string name, bool isFirstName)
    {
        var letters = TextMatch.Normalize(name).ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z').ToArray();
        var consonants = letters.Where(c => !"AEIOU".Contains(c)).ToArray();
        if (isFirstName && consonants.Length >= 4) return $"{consonants[0]}{consonants[2]}{consonants[3]}";
        return string.Concat(consonants.Concat(letters.Where(c => "AEIOU".Contains(c))).Concat("XXX"))[..3];
    }

    /// <summary>
    /// Spanish TIN: NIE for foreigners (X/Y/Z + 7 digits + letter), DNI otherwise (8 digits + letter). The letter is
    /// the number mod 23, with X/Y/Z read as 0/1/2.
    /// </summary>
    public string EsTin(bool foreigner = true)
    {
        var body = foreigner ? $"{"XYZ"[_random.Next(3)]}{_random.Next(0, 10_000_000):0000000}" : _random.Next(10_000_000, 100_000_000).ToString();
        return body + SpanishControlLetter(body);
    }

    public static char SpanishControlLetter(string body)
    {
        var prefix = "XYZ".IndexOf(char.ToUpperInvariant(body[0]));
        return SpanishLetters[int.Parse(prefix >= 0 ? prefix + body[1..] : body) % 23];
    }

    /// <summary>French numéro fiscal (SPI): 13 digits, the first 0–3; the last 3 are the first 10 mod 511.</summary>
    public string FrTin()
    {
        var first10 = $"{_random.Next(0, 4)}{_random.Next(0, 1_000_000_000):000000000}";
        return first10 + FrenchTinKey(first10).ToString("000");
    }

    public static int FrenchTinKey(string first10) => (int)(long.Parse(first10) % 511);

    /// <summary>
    /// Austrian Steuernummer, 9 digits (99-999/9999: tax office + number + check digit). The check digit is the Luhn
    /// digit of the first 8: not in the OECD sheet (NEVERIFICAT there), the only one TIN on Europa accepts.
    /// </summary>
    public string AtTin()
    {
        var body = $"{_random.Next(1, 100):00}{_random.Next(0, 1_000_000):000000}";
        return body + LuhnCheckDigit(body);
    }

    /// <summary>US TIN in ITIN format (9xx-xx-xxxx), only 987-65-4320…4329: reserved for examples, issued to no one.</summary>
    public string UsTin() => $"98765432{_random.Next(10)}";

    /// <summary>
    /// Mobile number in E.164, e.g. "+4915144201187". US: an existing area code + 555-01xx (reserved for fiction).
    /// </summary>
    public string PhoneE164(string country) => country.ToUpperInvariant() switch
    {
        "RO" => $"+407{_random.Next(20, 90)}{Digits(6)}",
        "DE" => $"+4915{"1279"[_random.Next(4)]}{Digits(8)}",
        "AT" => $"+43{Pick(AtMobilePrefixes)}{Digits(7)}",
        "IT" => $"+393{_random.Next(20, 94)}{Digits(7)}",
        "ES" => $"+346{Digits(8)}",
        "FR" => $"+33{_random.Next(6, 8)}{Digits(8)}",
        "US" => $"+1{Pick(UsAreaCodes)}55501{_random.Next(0, 100):00}",
        _ => throw new ArgumentException($"No phone generator for '{country}'.", nameof(country)),
    };

    /// <summary>
    /// IBAN with mod-97 check digits (<see cref="IbanWithCheckDigits"/>) and the national check where one exists:
    /// CIN (IT), "DC" control digits (ES), RIB key (FR). DE and AT: bank code + account number.
    /// </summary>
    public string ForeignIban(string country)
    {
        country = country.ToUpperInvariant();
        var bban = country switch
        {
            "DE" => Pick(DeBankCodes) + Digits(10),
            "AT" => Pick(AtBankCodes) + Digits(11),
            "IT" => ItalianBban(Pick(ItBankCodes), Digits(5), Digits(12)),
            "ES" => SpanishBban(Pick(EsBankCodes), Digits(4), Digits(10)),
            "FR" => FrenchBban(Pick(FrBankCodes), Digits(5), Digits(11)),
            _ => throw new ArgumentException($"No IBAN generator for '{country}' (RO: Iban()).", nameof(country)),
        };
        return IbanWithCheckDigits(country, bban);
    }

    public static string ItalianBban(string abi, string cab, string account) =>
        $"{ItalianControlCharacter(abi + cab + account)}{abi}{cab}{account}";

    public static string SpanishBban(string bank, string branch, string account) =>
        $"{bank}{branch}{SpanishControlDigit("00" + bank + branch)}{SpanishControlDigit(account)}{account}";

    /// <summary>One of the two Spanish "DC" digits, over 10 digits with weights 1, 2, 4, 8, 5, 10, 9, 7, 3, 6.</summary>
    public static int SpanishControlDigit(string tenDigits)
    {
        int[] weights = [1, 2, 4, 8, 5, 10, 9, 7, 3, 6];
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (tenDigits[i] - '0') * weights[i];
        var digit = 11 - sum % 11;
        return digit == 11 ? 0 : digit == 10 ? 1 : digit;
    }

    public static string FrenchBban(string bank, string branch, string account) =>
        $"{bank}{branch}{account}{RibKey(bank, branch, account):00}";

    /// <summary>French RIB key: 97 − (89 × bank + 15 × branch + 3 × account) mod 97 (numeric account numbers).</summary>
    public static int RibKey(string bank, string branch, string account) =>
        (int)(97 - (89 * long.Parse(bank) + 15 * long.Parse(branch) + 3 * long.Parse(account)) % 97);

    /// <summary>US ABA routing number: Federal Reserve district 01–12, 6 digits, check digit (weights 3, 7, 1).</summary>
    public string AbaRoutingNumber()
    {
        var body = $"{_random.Next(1, 13):00}{Digits(6)}";
        return body + AbaCheckDigit(body);
    }

    public static int AbaCheckDigit(string first8)
    {
        var sum = 0;
        for (var i = 0; i < 8; i++) sum += (first8[i] - '0') * (i % 3 == 0 ? 3 : i % 3 == 1 ? 7 : 1);
        return (10 - sum % 10) % 10;
    }

    private string Digits(int count) => string.Concat(Enumerable.Range(0, count).Select(_ => (char)('0' + _random.Next(10))));

    private void Shuffle<T>(List<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}

public enum TinFlaw { MissingCheck, WrongCheck }
