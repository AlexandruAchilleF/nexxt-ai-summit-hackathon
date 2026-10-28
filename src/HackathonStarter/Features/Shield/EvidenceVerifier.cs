using System.Text;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Shield;

/// <summary>
/// Code checks every quote the model gives as evidence, in the visible text only. The quote must be exact
/// (no "…"); case, diacritics, quote marks, dashes and line breaks don't matter.
/// </summary>
public sealed class EvidenceVerifier : IEvidenceVerifier
{
    /// <summary>After normalization.</summary>
    public const int MinQuoteLength = 4;

    /// <summary>
    /// Verified on the page where the quote is: the given one first, then any other (the caller saves the returned page).
    /// Found only in hidden text = not verified and a red flag.
    /// </summary>
    public EvidenceCheck Verify(string quote, ShieldResult document, int? page = null)
    {
        var q = NormalizeQuote(quote);
        if (q.Length < MinQuoteLength) return new(false, null, $"Quote too short (under {MinQuoteLength} characters).");
        if (page is { } p && (p < 1 || p > document.Pages.Count))
            return new(false, null, $"Page {p} is not in the document ({document.Pages.Count} page(s)).");

        if (document.Pages.FirstOrDefault(x => x.Number == page && Contains(x.VisibleText, q)) is { } onPage)
            return new(true, onPage.Number, "Quote found in the visible text.");
        if (document.Pages.FirstOrDefault(x => Contains(x.VisibleText, q)) is { } other)
            return new(true, other.Number, page is null
                ? "Quote found in the visible text."
                : $"Quote found in the visible text of page {other.Number}, not page {page}.");

        return document.Hidden.FirstOrDefault(h => Contains(h.Text, q)) is { } hidden
            ? new(false, hidden.Page, "Quote found only in hidden text: not verified, red flag.", FoundOnlyInHiddenText: true)
            : new(false, null, "Quote not found in the visible text.");
    }

    public static bool Matches(string quote, ShieldedPage page) => Contains(page.VisibleText, NormalizeQuote(quote));

    /// <summary>Lower-case, no diacritics, plain quotes and dashes, no zero-width characters, single spaces.</summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in TextMatch.Normalize(text))
        {
            var mapped = c switch
            {
                '„' or '“' or '”' or '«' or '»' or '″' => '"',
                '‚' or '‘' or '’' or '´' or '`' => '\'',
                '‐' or '‑' or '‒' or '–' or '—' or '−' => '-',
                '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF' => '\0', // zero-width
                _ when char.IsWhiteSpace(c) => ' ',
                _ => c,
            };
            if (mapped == '\0' || (mapped == ' ' && (sb.Length == 0 || sb[^1] == ' '))) continue;
            sb.Append(mapped);
        }
        return sb.ToString().TrimEnd();
    }

    private static string NormalizeQuote(string quote) => Normalize(quote).Trim('"', '\'', ' ');

    private static bool Contains(string text, string normalizedQuote) =>
        Normalize(text).Contains(normalizedQuote, StringComparison.Ordinal);
}
