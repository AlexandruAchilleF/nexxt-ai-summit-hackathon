using HackathonStarter.Features.Clients;
using HackathonStarter.Services;
using UglyToad.PdfPig;

namespace HackathonStarter.Features.Shield;

/// <summary>Phase 0 placeholder: plain text per page, no hidden-text detection yet. R3 replaces it (letters, colour, size, position).</summary>
public sealed class HiddenTextShield : IHiddenTextShield
{
    public ShieldResult Scan(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        var pages = document.GetPages()
            .Select(p => new ShieldedPage(p.Number, string.Join(" ", p.GetWords().Select(w => w.Text))))
            .ToList();
        return new ShieldResult(ShieldVerdict.Clean, pages, []);
    }
}

/// <summary>Phase 0 placeholder: normalized substring search in the visible text. R3 owns the final rules.</summary>
public sealed class EvidenceVerifier : IEvidenceVerifier
{
    public EvidenceCheck Verify(string quote, ShieldResult document, int? page = null)
    {
        var needle = TextMatch.Normalize(quote).Trim();
        if (needle.Length == 0) return new(false, null, "Empty quote.");
        foreach (var p in document.Pages.Where(p => page is null || p.Number == page))
            if (TextMatch.Normalize(p.VisibleText).Contains(needle, StringComparison.Ordinal))
                return new(true, p.Number, "Quote found in the visible text.");
        return new(false, null, "Quote not found in the visible text.");
    }
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
