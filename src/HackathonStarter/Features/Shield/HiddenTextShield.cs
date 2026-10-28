using HackathonStarter.Features.Clients;
using HackathonStarter.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Geometry;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Util;

namespace HackathonStarter.Features.Shield;

/// <summary>
/// Runs on every PDF before its text reaches the model, letter by letter: white, microscopic, off-page and
/// invisible text is removed and reported. Text covered by a shape is not detected.
/// </summary>
public sealed class HiddenTextShield : IHiddenTextShield
{
    /// <summary>Every RGB channel at or above this is "almost white".</summary>
    public const double WhiteThreshold = 0.95;

    /// <summary>Strictly below this (in points) is microscopic. PointSize, not FontSize, which is often 1.</summary>
    public const double MinPointSize = 2.0;

    /// <summary>Throws <see cref="InvalidDataException"/> for a file PdfPig can't read (corrupt, encrypted).</summary>
    public ShieldResult Scan(byte[] pdf)
    {
        var pages = new List<ShieldedPage>();
        var hidden = new List<HiddenFragment>();
        foreach (var (number, letters, bounds) in Read(pdf))
        {
            var marked = letters.Select(l => (Letter: l, Reasons: ReasonsFor(l, bounds))).ToList();
            pages.Add(new ShieldedPage(number, Words(marked.Where(m => m.Reasons == HiddenReason.None).Select(m => m.Letter))));
            hidden.AddRange(marked
                .Where(m => m.Reasons != HiddenReason.None)
                .GroupBy(m => m.Reasons)
                .Select(g => new HiddenFragment(number, PiiVault.ForStorage().Mask(Words(g.Select(m => m.Letter))), g.Key.ToString())));
        }
        return new ShieldResult(hidden.Count > 0 ? ShieldVerdict.HiddenTextRemoved : ShieldVerdict.Clean, pages, hidden);
    }

    private static List<(int Number, IReadOnlyList<Letter> Letters, PdfRectangle Bounds)> Read(byte[] pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            return document.GetPages().Select(p => (p.Number, p.Letters, p.CropBox.Bounds)).ToList();
        }
        catch (Exception e)
        {
            throw new InvalidDataException("The file is not a readable PDF.", e);
        }
    }

    private static HiddenReason ReasonsFor(Letter letter, PdfRectangle page)
    {
        if (string.IsNullOrWhiteSpace(letter.Value)) return HiddenReason.None;
        var reasons = HiddenReason.None;
        if (IsWhite(letter.FillColor)) reasons |= HiddenReason.WhiteText;
        if (letter.PointSize < MinPointSize) reasons |= HiddenReason.TinyFont;
        if (!letter.BoundingBox.IntersectsWith(page)) reasons |= HiddenReason.OffPage;
        if (letter.RenderingMode is TextRenderingMode.Neither or TextRenderingMode.NeitherClip) reasons |= HiddenReason.Invisible;
        return reasons;
    }

    private static bool IsWhite(IColor? color)
    {
        if (color is null) return false;
        try
        {
            var (r, g, b) = color.ToRGBValues();
            return r >= WhiteThreshold && g >= WhiteThreshold && b >= WhiteThreshold;
        }
        catch (Exception)
        {
            return false; // pattern colors have no RGB value
        }
    }

    /// <summary>Words joined by spaces, like DocumentText: page.Text often loses the spaces between words.</summary>
    private static string Words(IEnumerable<Letter> letters) =>
        string.Join(" ", DefaultWordExtractor.Instance.GetWords(letters.ToList()).Select(w => w.Text));
}

public sealed class ShieldedDocuments(IClientService clients, IHiddenTextShield shield) : IShieldedDocuments
{
    public async Task<ShieldedDocument?> GetAsync(int documentId)
    {
        var doc = await clients.GetDocumentAsync(documentId);
        return doc is null ? null : new ShieldedDocument(doc.Id, doc.ClientId, doc.FileName, doc.Kind, shield.Scan(doc.Content));
    }
}

public static class ShieldSetup
{
    public static IServiceCollection AddShield(this IServiceCollection services)
    {
        services.AddSingleton<IHiddenTextShield, HiddenTextShield>();
        services.AddSingleton<IEvidenceVerifier, EvidenceVerifier>();
        services.AddScoped<IShieldedDocuments, ShieldedDocuments>();
        return services;
    }
}
