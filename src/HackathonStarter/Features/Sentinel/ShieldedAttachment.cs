using HackathonStarter.Agents;
using HackathonStarter.Features.Shield;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// A PDF attached in the chat goes through the hidden-text shield, like a client document (docs/PLAN.md 10.1): the model
/// gets only the text a person sees. DocumentText.ExtractAsync reads every letter, white, microscopic and off-page included,
/// so on its own it would let the case 10 injection ("Ignora instructiunile anterioare...") reach the model.
/// </summary>
public static class ShieldedAttachment
{
    private const int MaxChars = 200_000; // as DocumentText

    /// <summary>The attachment for the model and how many hidden fragments the shield removed (0 for other file types).</summary>
    public static async Task<(AgentFile File, int Hidden)> ReadAsync(IHiddenTextShield shield, Stream stream, string fileName, CancellationToken ct = default)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return (await DocumentText.ExtractAsync(stream, fileName, ct), 0);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var scan = shield.Scan(buffer.ToArray());

        // Same layout as DocumentText: pages split by a blank line, so a clean PDF reads exactly as before.
        var text = string.Join("\n\n", scan.Pages.Select(p => p.VisibleText)).Trim();
        if (text.Length == 0) throw new InvalidOperationException("No visible text found (scanned PDF, or only hidden text). Try a text-based PDF.");
        if (text.Length > MaxChars) text = text[..MaxChars];
        return (new AgentFile(Guid.NewGuid().ToString("N")[..12], fileName, text, scan.Pages.Count), scan.Hidden.Count);
    }
}
