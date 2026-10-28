using System.Globalization;
using System.Text;

namespace HackathonStarter.Services;

/// <summary>Language-tolerant keyword matching (lower-case, no diacritics): "Arhivează" matches "arhiv".</summary>
public static partial class TextMatch
{
    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>True if the text contains any of the keywords (compared without case or diacritics).</summary>
    public static bool ContainsAny(string text, params string[] keywords)
    {
        var normalized = Normalize(text);
        return keywords.Any(k => normalized.Contains(Normalize(k), StringComparison.Ordinal));
    }

    /// <summary>Lower-case, diacritic-free words of at least 2 characters.</summary>
    public static string[] Words(string text) =>
        NonWord().Split(Normalize(text)).Where(w => w.Length >= 2).ToArray();

    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial System.Text.RegularExpressions.Regex NonWord();
}
