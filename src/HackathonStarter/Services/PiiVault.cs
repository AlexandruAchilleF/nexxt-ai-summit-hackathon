using System.Text.RegularExpressions;
using HackathonStarter.Features.Finance;

namespace HackathonStarter.Services;

/// <summary>
/// Masks personal data before text is sent to a model, and restores it in the model's answer.
/// "Plata din RO49AAAA1B31007593840000 pentru ion@x.ro" → "Plata din [IBAN_1] pentru [EMAIL_1]".
/// The same value always gets the same placeholder within one vault (one agent run), so the model can
/// still reason about it ("send to [IBAN_1]"). Detectors are validated (checksums), to avoid masking
/// random numbers: CNP (Romanian personal numeric code), IBAN, payment card (Luhn), email, phone.
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

    [GeneratedRegex(@"\b(?:\d[ -]?){12,18}\d\b")]
    private static partial Regex CardRegex();

    [GeneratedRegex(@"\b[1-9]\d{12}\b")]
    private static partial Regex CnpRegex();

    // Romanian mobile/landline numbers: +40 / 0040 / 0 prefix, optional separators.
    [GeneratedRegex(@"(?<![\d\w])(?:\+40|0040|0)\s?[237]\d{2}[\s.-]?\d{3}[\s.-]?\d{3}(?!\d)")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\[(?:EMAIL|IBAN|CARD|CNP|PHONE)_\d+\]")]
    private static partial Regex TokenRegex();
}

/// <summary>A personal data item found in a text: its type (CNP, IBAN, CARD, EMAIL, PHONE), value and placeholder.</summary>
public record PiiItem(string Type, string Value, string Token);
