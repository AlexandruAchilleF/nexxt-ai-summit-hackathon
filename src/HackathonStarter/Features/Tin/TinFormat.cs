using System.Text.RegularExpressions;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Tin;

/// <summary>
/// Local TIN format rules per country (OECD TIN sheets; EU rules checked against TIN on Europa on 28.10.2026). Pure and
/// synchronous, so the rule engine can use it. A country without a rule and an empty TIN are NotChecked, never a wrong
/// format (that would be a false SC-TIN-FORMAT).
/// </summary>
public sealed partial class TinFormat : ITinFormat
{
    private static readonly Dictionary<string, (string Source, Func<string, bool> IsValid)> Rules = new()
    {
        ["AT"] = ("OECD TIN sheet AT · Steuernummer", Austria),
        ["DE"] = ("OECD TIN sheet DE · ISO 7064 MOD 11,10", Germany),
        ["ES"] = ("OECD TIN sheet ES · DNI/NIE", Spain),
        ["FR"] = ("OECD TIN sheet FR · numero fiscal", France),
        ["IT"] = ("OECD TIN sheet IT · codice fiscale", Italy),
        ["RO"] = ("OECD TIN sheet RO · CNP", PiiVault.IsValidCnp),
        ["US"] = ("IRS/SSA · SSN or ITIN (US is not covered by TIN on Europa)", UnitedStates),
    };

    // OECD TIN sheet AT: Austria does not issue a TIN to every individual (docs/GOLDEN-CASES.md, case 3). Others: unknown.
    private static readonly Dictionary<string, bool> IssuesToAll = new() { ["AT"] = false };

    public static IReadOnlyList<string> Countries { get; } = Rules.Keys.Order().ToList();

    /// <summary>Upper case, without spaces, dots, dashes and slashes: "860 951 742 23" → "86095174223".</summary>
    public static string Normalize(string? tin) =>
        string.Concat((tin ?? "").Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-' or '/'))).ToUpperInvariant();

    public TinCheck Check(string country, string tin)
    {
        var code = country.Trim().ToUpperInvariant();
        var normalized = Normalize(tin);
        if (normalized.Length == 0) return new(code, "", false, TinCheckSource.NotChecked, "No TIN given.");
        if (!Rules.TryGetValue(code, out var rule))
            return new(code, normalized, false, TinCheckSource.NotChecked, $"No local format rule for {code}.");
        return new(code, normalized, rule.IsValid(normalized), TinCheckSource.LocalRules, rule.Source);
    }

    public bool? IssuesTinToAllResidents(string country) =>
        IssuesToAll.TryGetValue(country.Trim().ToUpperInvariant(), out var all) ? all : null;

    /// <summary>
    /// 11 digits, not starting with 0. In the first 10, exactly one digit appears 2 or 3 times (3 times: not all
    /// adjacent), the others at most once. Check digit: ISO 7064 MOD 11,10.
    /// </summary>
    private static bool Germany(string t)
    {
        if (!AllDigits(t, 11) || t[0] == '0') return false;
        var repeated = t[..10].GroupBy(c => c).Where(g => g.Count() > 1).ToList();
        if (repeated.Count != 1 || repeated[0].Count() > 3) return false;
        if (repeated[0].Count() == 3 && t[..10].Contains(new string(repeated[0].Key, 3))) return false;

        var p = 10;
        foreach (var d in t[..10].Select(c => c - '0'))
        {
            var s = (d + p) % 10;
            if (s == 0) s = 10;
            p = s * 2 % 11;
        }
        return (11 - p) % 10 == t[10] - '0';
    }

    // Odd positions (1st, 3rd…): value of the digit or letter (digits count as A-J).
    private static readonly int[] ItalyOdd = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];

    /// <summary>Codice fiscale: 16 characters, the last is a check letter (odd/even tables, sum mod 26).</summary>
    private static bool Italy(string t)
    {
        if (!ItalyPattern().IsMatch(t)) return false;
        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var k = char.IsDigit(t[i]) ? t[i] - '0' : t[i] - 'A';
            sum += i % 2 == 0 ? ItalyOdd[k] : k;
        }
        return t[15] == (char)('A' + sum % 26);
    }

    /// <summary>9 digits; the 2nd, 4th, 6th and 8th are doubled (digit sum); check = (10 - sum mod 10) mod 10.</summary>
    private static bool Austria(string t)
    {
        if (!AllDigits(t, 9)) return false;
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            var d = t[i] - '0';
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return (10 - sum % 10) % 10 == t[8] - '0';
    }

    /// <summary>DNI (8 digits + letter) or NIE (X/Y/Z = 0/1/2, 7 digits, letter); letter = table[number mod 23].</summary>
    private static bool Spain(string t)
    {
        if (!SpainPattern().IsMatch(t)) return false;
        var digits = t[0] is 'X' or 'Y' or 'Z' ? "XYZ".IndexOf(t[0]) + t[1..8] : t[..8];
        return t[8] == "TRWAGMYFPDXBNJZSQVHLCKE"[int.Parse(digits) % 23];
    }

    /// <summary>13 digits, the first 0-3; the last 3 are the first 10 mod 511.</summary>
    private static bool France(string t) =>
        FrancePattern().IsMatch(t) && long.Parse(t[..10]) % 511 == int.Parse(t[10..]);

    /// <summary>SSN (area not 000, 666 or 9xx; group not 00; serial not 0000) or ITIN (9xx, digits 4-5 in the IRS ranges).</summary>
    private static bool UnitedStates(string t)
    {
        if (!AllDigits(t, 9)) return false;
        int area = int.Parse(t[..3]), group = int.Parse(t[3..5]), serial = int.Parse(t[5..]);
        if (t[0] == '9') return group is (>= 50 and <= 65) or (>= 70 and <= 88) or (>= 90 and <= 92) or (>= 94 and <= 99);
        return area is not (0 or 666) && group != 0 && serial != 0;
    }

    private static bool AllDigits(string t, int length) => t.Length == length && t.All(char.IsAsciiDigit);

    [GeneratedRegex("^[A-Z]{6}[0-9LMNPQRSTUV]{2}[ABCDEHLMPRST][0-9LMNPQRSTUV]{2}[A-Z][0-9LMNPQRSTUV]{3}[A-Z]$")]
    private static partial Regex ItalyPattern();

    [GeneratedRegex(@"^(\d{8}|[XYZ]\d{7})[A-Z]$")]
    private static partial Regex SpainPattern();

    [GeneratedRegex(@"^[0-3]\d{12}$")]
    private static partial Regex FrancePattern();
}
