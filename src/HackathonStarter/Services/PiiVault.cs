using System.Text;
using System.Text.RegularExpressions;
using HackathonStarter.Features.Finance;

namespace HackathonStarter.Services;

/// <summary>
/// Masks personal data before text is sent to a model, and restores it in the model's answer.
/// "Plata din RO49AAAA1B31007593840000 pentru ion@x.ro" → "Plata din [IBAN_1] pentru [EMAIL_1]".
/// The same value always gets the same placeholder within one vault (one agent run), so the model can
/// still reason about it ("send to [IBAN_1]"). Detectors are validated (checksums), to avoid masking
/// random numbers: CNP (Romanian personal numeric code), IBAN, payment card (Luhn), email, phone (Romanian, or
/// international with a "+" / "00" prefix).
///
/// <see cref="ForStorage"/> makes a vault for data at rest (run history, recordings, exports): its placeholders
/// look like [IBAN-1] and are never restored, so nothing stored can be turned back into personal data, and they
/// can't be confused with the [IBAN_1] placeholders of a live run.
/// </summary>
public partial class PiiVault
{
    private readonly Dictionary<string, string> _byValue = new();
    private readonly Dictionary<string, string> _byToken = new();
    private readonly Dictionary<string, int> _counters = new();
    private readonly char _separator;

    public PiiVault() : this('_') { }

    private PiiVault(char separator) => _separator = separator;

    /// <summary>One-way masking for anything that is saved: placeholders like [CNP-1], never unmasked.</summary>
    public static PiiVault ForStorage() => new('-');

    public int Count => _byValue.Count;
    public IReadOnlyCollection<string> Types => _counters.Keys;

    /// <summary>What was masked so far: type ("IBAN"), original value and its placeholder.</summary>
    public IReadOnlyList<PiiItem> Items =>
        _byToken.Select(t => new PiiItem(t.Key[1..t.Key.IndexOf(_separator)], t.Value, t.Key)).ToList();

    /// <summary>The personal data items found in a text (validated: checksums for CNP, IBAN, card).</summary>
    public static IReadOnlyList<PiiItem> Detect(string? text)
    {
        var vault = new PiiVault();
        vault.Mask(text);
        return vault.Items;
    }

    /// <summary>Masks with a fresh storage vault: for a single value that is saved on its own.</summary>
    public static string? Redact(string? text) => text is null ? null : ForStorage().Mask(text);

    public string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        text = EmailRegex().Replace(text, m => Token("EMAIL", m.Value));
        text = IbanRegex().Replace(text, MaskIban);
        // CNP before card: about 1 in 10 CNPs also passes the Luhn check and would be labelled a card.
        text = CnpRegex().Replace(text, m => IsValidCnp(m.Value) ? Token("CNP", m.Value) : m.Value);
        text = CardRegex().Replace(text, m => IsLuhnValid(m.Value) ? Token("CARD", m.Value) : m.Value);
        text = PhoneRegex().Replace(text, m => Token("PHONE", m.Value));
        text = InternationalPhoneRegex().Replace(text, MaskInternationalPhone);
        // Digits after a "+" that the phone pattern left (an amount to it, "+1900515400013.50") are checked as CNP and card.
        text = CnpAfterPlusRegex().Replace(text, m => IsValidCnp(m.Value) ? Token("CNP", m.Value) : m.Value);
        text = CardAfterPlusRegex().Replace(text, m => IsLuhnValid(m.Value) ? Token("CARD", m.Value) : m.Value);
        return text;
    }

    public string Unmask(string? text)
    {
        if (string.IsNullOrEmpty(text) || _byToken.Count == 0 || _separator != '_') return text ?? "";
        return TokenRegex().Replace(text, m => _byToken.TryGetValue(m.Value, out var original) ? original : m.Value);
    }

    /// <summary>
    /// The IBAN pattern may swallow the start of the next word ("RO49…0000 and" → "…0000 and"), so drop
    /// trailing space-separated parts until the checksum is valid.
    /// </summary>
    private string MaskIban(Match m)
    {
        var candidate = m.Value;
        while (true)
        {
            if (Iban.Validate(candidate).IsValid) return Token("IBAN", candidate) + m.Value[candidate.Length..];
            var cut = candidate.LastIndexOf(' ');
            if (cut < 0) return m.Value;
            candidate = candidate[..cut];
        }
    }

    /// <summary>
    /// A country code and 6 to 12 more digits, so 7 to 15 after the prefix (E.164). The pattern may also take what
    /// follows ("+49 89 4521 7730 2 ori", "+43 1 5134567 0049 89 4521 7730"), so keep only the space-separated parts
    /// that belong to the number: none that would go over 15 digits (except an extension joined by a dash or dot,
    /// "7730-1234"), after 10 digits only a pair that continues pairs ("+380 44 123 45 67"), and none that starts
    /// another "00" number. The rest is scanned again. Over 15 digits with only card characters and a valid Luhn
    /// check is a card, as before phones were detected: CardRegex takes "+4111111111111111", this its JSON escape.
    /// A JSON-escaped "+" is kept as "+" (valid JSON too), so a number gets the same placeholder in the request
    /// and in the saved arguments of a tool call.
    /// </summary>
    private string MaskInternationalPhone(Match m)
    {
        // The rest of a match is scanned as its own text, as Regex.Replace on a copy of it would, but in a loop over
        // positions in the match: a long run of numbers is one match, and a copy per number grew with its square.
        var run = m.Value;
        var output = new StringBuilder(run.Length);
        var scans = new Stack<(int Pos, int End, Match Next)>();
        var (start, number, end) = (0, m.Groups["number"].Index - m.Index, run.Length);
        while (true)
        {
            var rest = MaskFirstNumber(run, start, number, end, output);
            if (rest < end) scans.Push((rest, end, InternationalPhoneRegex().Match(run, rest, end - rest)));
            while (true)
            {
                if (scans.Count == 0) return output.ToString();
                var (pos, scanEnd, next) = scans.Pop();
                if (!next.Success)
                {
                    output.Append(run, pos, scanEnd - pos);
                    continue;
                }
                output.Append(run, pos, next.Index - pos);
                scans.Push((next.Index + next.Length, scanEnd, next.NextMatch()));
                (start, number, end) = (next.Index, next.Groups["number"].Index, next.Index + next.Length);
                break;
            }
        }
    }

    /// <summary>Masks the first number of the match run[start..end), whose number group starts at numberStart, and returns where the rest begins.</summary>
    private int MaskFirstNumber(string run, int start, int numberStart, int end, StringBuilder output)
    {
        var prefix = run.AsSpan(start, numberStart - start);
        var number = run.AsSpan(numberStart, end - numberStart);
        if (Digits(number, max: 20) is > 15 and < 20 && IsLuhnValid(number.ToString()) && !number.ContainsAnyExcept("0123456789 -"))
        {
            output.Append(prefix).Append(Token("CARD", number.ToString()));
            return end;
        }

        var (kept, previous, cut) = (0, 0, 0);
        foreach (var range in number.Split(' '))
        {
            var part = number[range];
            var digits = Digits(part);
            if (kept >= 10 && (digits > 2 || previous > 2)) break;
            if (part is ['0', '0', >= '1' and <= '9', ..] && Digits(number[range.Start..], max: 9) - 2 >= 7) break;
            if (kept + digits > 15)
            {
                if (part.IndexOfAny('-', '.') >= 0) (kept, cut) = (kept + digits, range.End.GetOffset(number.Length));
                break;
            }
            (kept, previous, cut) = (kept + digits, digits, range.End.GetOffset(number.Length));
        }
        if (kept < 7)
        {
            // Not a phone ("+12"): keep its first part and scan the rest, which may hold one ("+12 0049 89 4521 7730").
            var first = number.IndexOf(' ') is var space and >= 0 ? space : number.Length;
            output.Append(run, start, numberStart + first - start);
            return numberStart + first;
        }
        var phone = (prefix[0] == '\\' ? "+" : prefix.ToString()) + number[..cut].ToString();
        output.Append(Token("PHONE", phone));
        return numberStart + cut;
    }

    /// <summary>Digits of a phone part, without the trunk "(0)" that is not dialled from abroad ("+49 (0)151 …").</summary>
    private static int Digits(ReadOnlySpan<char> part)
    {
        var digits = 0;
        foreach (var c in part) if (char.IsAsciiDigit(c)) digits++;
        return digits - (part.IndexOf("(0)") >= 0 ? 1 : 0);
    }

    /// <summary>Digits of the space-separated parts of a text, counted until <paramref name="max"/>: only a few are ever needed.</summary>
    private static int Digits(ReadOnlySpan<char> text, int max)
    {
        var digits = 0;
        foreach (var range in text.Split(' '))
            if ((digits += Digits(text[range])) >= max) break;
        return digits;
    }

    private string Token(string type, string value)
    {
        var key = value.Trim();
        if (_byValue.TryGetValue(key, out var existing)) return existing;
        _counters[type] = _counters.GetValueOrDefault(type) + 1;
        var token = $"[{type}{_separator}{_counters[type]}]";
        _byValue[key] = token;
        _byToken[token] = key;
        return token;
    }

    /// <summary>Romanian CNP: 13 digits, last one is a checksum with weights 279146358279.</summary>
    public static bool IsValidCnp(string cnp)
    {
        if (cnp.Length != 13 || !cnp.All(char.IsDigit) || cnp[0] == '0') return false;
        const string weights = "279146358279";
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (cnp[i] - '0') * (weights[i] - '0');
        var check = sum % 11 == 10 ? 1 : sum % 11;
        return check == cnp[12] - '0';
    }

    public static bool IsLuhnValid(string number)
    {
        var digits = number.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length is < 13 or > 19) return false;
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i];
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 == 0;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b[A-Za-z]{2}\d{2}(?:[ ]?[A-Za-z0-9]{4}){2,7}(?:[ ]?[A-Za-z0-9]{1,4})?\b")]
    private static partial Regex IbanRegex();

    // Card and CNP never follow a "+" that InternationalPhoneRegex takes as a prefix: "+4915144201187" is a phone even
    // if its digits pass the Luhn or CNP check. After such a "+" only a run too long for a phone (16 to 19 digits) is
    // a card ("+4111111111111111"). A "+" after a word is no prefix: "Card+4111111111111111", "q=CNP+1900515400013".
    [GeneratedRegex(@"(?<!(?<![\w+]|\w-)\+)\b(?:\d[ -]?){12,18}\d\b|(?<=(?<![\w+]|\w-)\+)(?:\d[ -]?){15,18}\d\b")]
    private static partial Regex CardRegex();

    [GeneratedRegex(@"(?<!(?<![\w+]|\w-)\+)\b[1-9]\d{12}\b")]
    private static partial Regex CnpRegex();

    [GeneratedRegex(@"(?<=\+)[1-9]\d{12}\b")]
    private static partial Regex CnpAfterPlusRegex();

    [GeneratedRegex(@"(?<=\+)(?:\d[ -]?){12,18}\d\b")]
    private static partial Regex CardAfterPlusRegex();

    // Romanian mobile/landline numbers: +40 / 0040 / 0 prefix, optional separators.
    [GeneratedRegex(@"(?<![\d\w])(?:\+40|0040|0)\s?[237]\d{2}[\s.-]?\d{3}[\s.-]?\d{3}(?!\d)")]
    private static partial Regex PhoneRegex();

    // International numbers: "+" / "00" (or the JSON escape of "+" that RunStore.Json writes), a country code, then
    // digit groups split by single spaces, dashes or dots, with an optional "(212)" / "(0)" area code. Never inside a
    // longer token, after a decimal point of a lone 0 ("0.0049…", "0,0049…"; "1001,0049…" and "1,+49…" are CSV rows), in
    // a space-separated IBAN ("DE89 3704 0044 …") or an amount ("+1.234.567", "+1234567.89", "+1234567,89"). After a
    // comma, more than 2 digits, or a number of 10+ digits before it, is a CSV field, not decimals: "+498945217730,1985",
    // "+498945217730,35", "+1 (212) 555-0147,35".
    // No atomic group: when the last part runs into a word ("+1 212 555 0147 9am") the match ends at the part before.
    [GeneratedRegex(@"(?<![\w+]|\w-)(?:\+|\\u002[Bb]|(?<!(?<!\d)0[.,]|\b[A-Za-z]{2}\d{2}(?: [A-Za-z0-9]{4}){0,7} )00)(?!\d{1,3}(?:\.\d{3})+(?![\d.])|\d+\.\d{1,2}(?![\d.]))(?<number>[1-9]\d*(?:[ .-]?\(\d{1,4}\)[ .-]?\d+)?(?:[ .-]\d+)*)(?!\w|(?<!\d(?:[ .()-]{0,2}\d){9}),\d{1,2}(?!\d))")]
    private static partial Regex InternationalPhoneRegex();

    [GeneratedRegex(@"\[(?:EMAIL|IBAN|CARD|CNP|PHONE)_\d+\]")]
    private static partial Regex TokenRegex();
}

/// <summary>A personal data item found in a text: its type (CNP, IBAN, CARD, EMAIL, PHONE), value and placeholder.</summary>
public record PiiItem(string Type, string Value, string Token);
