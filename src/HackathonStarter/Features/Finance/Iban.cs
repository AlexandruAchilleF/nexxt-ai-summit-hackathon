using System.Numerics;
using System.Text.RegularExpressions;

namespace HackathonStarter.Features.Finance;

/// <summary>IBAN validation (ISO 13616, mod-97), with country-specific lengths for common European countries.</summary>
public static partial class Iban
{
    private static readonly Dictionary<string, int> Lengths = new()
    {
        ["RO"] = 24, ["DE"] = 22, ["FR"] = 27, ["IT"] = 27, ["ES"] = 24, ["NL"] = 18, ["BE"] = 16,
        ["AT"] = 20, ["PL"] = 28, ["HU"] = 28, ["BG"] = 22, ["MD"] = 24, ["GB"] = 22, ["IE"] = 22,
    };

    /// <summary>Removes spaces and upper-cases: " ro49 aaaa..." → "RO49AAAA...".</summary>
    public static string Normalize(string iban) => WhitespaceRegex().Replace(iban, "").ToUpperInvariant();

    public static IbanCheck Validate(string input)
    {
        var iban = Normalize(input);
        if (!FormatRegex().IsMatch(iban))
            return new IbanCheck(false, iban, "Invalid format: 2 letters, 2 digits, then letters/digits.");

        var country = iban[..2];
        if (Lengths.TryGetValue(country, out var expected) && iban.Length != expected)
            return new IbanCheck(false, iban, $"{country} IBANs have {expected} characters, this one has {iban.Length}.");

        // Move the first 4 chars to the end, letters → numbers (A=10 … Z=35), remainder mod 97 must be 1.
        var rearranged = iban[4..] + iban[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));
        var valid = BigInteger.Parse(digits) % 97 == 1;

        return new IbanCheck(valid, iban, valid ? null : "Checksum is wrong (typo?).",
            Country: country, BankCode: country == "RO" ? iban.Substring(4, 4) : null);
    }

    /// <summary>"RO49AAAA1B31007593840000" → "RO49 AAAA 1B31 0075 9384 0000".</summary>
    public static string Format(string iban) =>
        string.Join(' ', Normalize(iban).Chunk(4).Select(c => new string(c)));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{10,30}$")]
    private static partial Regex FormatRegex();
}

public record IbanCheck(bool IsValid, string Normalized, string? Error, string? Country = null, string? BankCode = null);
