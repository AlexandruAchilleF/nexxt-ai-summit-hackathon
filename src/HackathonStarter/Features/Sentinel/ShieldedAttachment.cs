using System.Text.RegularExpressions;
using HackathonStarter.Agents;
using HackathonStarter.Features.Shield;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// A file attached in the chat reaches the model only as the text a person sees, like a client document (docs/PLAN.md 10.1).
/// DocumentText.ExtractAsync reads every letter: on its own it would let the case 10 injection ("Ignora instructiunile
/// anterioare...") through white, microscopic or off-page text, a PDF renamed to .txt, an HTML element that is not shown,
/// a comment, or invisible characters.
/// </summary>
public static partial class ShieldedAttachment
{
    private const int MaxChars = 200_000; // as DocumentText

    /// <summary>The attachment for the model and how many hidden fragments were removed.</summary>
    /// <exception cref="NotSupportedException">HTML or XML, which can hide text from a reader.</exception>
    public static async Task<(AgentFile File, int Hidden)> ReadAsync(IHiddenTextShield shield, Stream stream, string fileName, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        // By content, not by name: a PDF renamed to .txt would otherwise be read raw, hidden text included.
        if (ext == ".pdf" || bytes.AsSpan().StartsWith("%PDF"u8)) return Pdf(shield, bytes, fileName);
        if (ext is ".html" or ".htm" or ".xml")
            throw new NotSupportedException("HTML and XML are not accepted in the chat: they can hide text from a reader. Attach a PDF or a text file.");

        var file = await DocumentText.ExtractAsync(new MemoryStream(bytes), fileName, ct);
        if (file.Image is not null) return (file, 0);

        var hidden = 0;
        var text = HtmlComment().Replace(file.Text, _ => { hidden++; return ""; });
        text = Invisible().Replace(text, _ => { hidden++; return ""; }).Trim();
        if (text.Length == 0) throw new InvalidOperationException("No visible text found in the file.");
        return (file with { Text = text }, hidden);
    }

    private static (AgentFile File, int Hidden) Pdf(IHiddenTextShield shield, byte[] bytes, string fileName)
    {
        var scan = shield.Scan(bytes);
        // Same layout as DocumentText: pages split by a blank line, so a clean PDF reads exactly as before.
        var text = string.Join("\n\n", scan.Pages.Select(p => p.VisibleText)).Trim();
        if (text.Length == 0) throw new InvalidOperationException("No visible text found (scanned PDF, or only hidden text). Try a text-based PDF.");
        if (text.Length > MaxChars) text = text[..MaxChars];
        return (new AgentFile(Guid.NewGuid().ToString("N")[..12], fileName, text, scan.Pages.Count), scan.Hidden.Count);
    }

    // Not shown when Markdown is rendered.
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    // Zero-width, joiner, direction and soft-hyphen characters: invisible, but read by a model.
    [GeneratedRegex(@"[­​-‏‪-‮⁠-⁤⁦-⁩﻿]")]
    private static partial Regex Invisible();
}
