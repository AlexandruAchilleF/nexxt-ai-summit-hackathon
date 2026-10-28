using HackathonStarter.Agents;
using UglyToad.PdfPig;

namespace HackathonStarter.Services;

/// <summary>
/// Converts uploads (PDF or text files) to plain text so any model and the readDocument tool can use them.
/// Images (e.g. a phone photo of an invoice) are kept as bytes and shown to vision-capable models.
/// </summary>
public static class DocumentText
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private const int MaxChars = 200_000;
    private static readonly string[] TextExtensions = [".txt", ".md", ".csv", ".json", ".xml", ".html", ".log"];
    private static readonly Dictionary<string, string> ImageTypes = new()
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp", [".gif"] = "image/gif",
    };

    public static bool IsImage(string fileName) => ImageTypes.ContainsKey(Path.GetExtension(fileName).ToLowerInvariant());

    public static AgentFile FromImage(byte[] data, string fileName, string? mediaType = null) =>
        new(Guid.NewGuid().ToString("N")[..12], fileName,
            $"Image \"{fileName}\" ({data.Length / 1024} KB). It is attached to the user's message: look at it directly, there is no text to read.",
            1, data, mediaType ?? ImageTypes.GetValueOrDefault(Path.GetExtension(fileName).ToLowerInvariant(), "image/jpeg"));

    public static async Task<AgentFile> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        string text;
        var pages = 1;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (IsImage(fileName)) return FromImage(buffer.ToArray(), fileName);

        if (ext == ".pdf")
        {
            using var pdf = PdfDocument.Open(buffer);
            pages = pdf.NumberOfPages;
            text = string.Join("\n\n", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
        }
        else if (TextExtensions.Contains(ext))
        {
            using var reader = new StreamReader(buffer);
            text = await reader.ReadToEndAsync(ct);
        }
        else
        {
            throw new NotSupportedException("Unsupported file type. Use PDF, an image or a text file (txt, md, csv, json).");
        }

        text = text.Trim();
        if (text.Length == 0) throw new InvalidOperationException("No text found (scanned PDF?). Try a text-based PDF.");
        if (text.Length > MaxChars) text = text[..MaxChars];

        return new AgentFile(Guid.NewGuid().ToString("N")[..12], fileName, text, pages);
    }
}
