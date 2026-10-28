using HackathonStarter.Agents;

namespace HackathonStarter.Services;

/// <summary>
/// Keyword search inside attached documents (BM25-style ranking over ~800-character passages).
/// Lets agents find the relevant part of long documents instead of reading only the beginning.
/// No external service needed; swap for embeddings later if you need semantic search.
/// </summary>
public static class DocumentSearch
{
    public record Passage(string Document, int Index, string Text, double Score);

    private const int ChunkSize = 800;
    private const int Overlap = 150;

    public static List<Passage> Search(IReadOnlyList<AgentFile> files, string query, int top = 5)
    {
        files = files.Where(f => !f.IsImage).ToList();
        var chunks = files.SelectMany(f => Chunk(f.Text).Select((text, i) => (Doc: f.Name, Index: i, Text: text))).ToList();
        var terms = TextMatch.Words(query).Distinct().ToArray();
        if (chunks.Count == 0 || terms.Length == 0) return [];

        var tokenized = chunks.Select(c => TextMatch.Words(c.Text)).ToList();
        var avgLength = tokenized.Average(t => (double)t.Length);
        var n = chunks.Count;
        var df = terms.ToDictionary(t => t, t => tokenized.Count(words => words.Any(w => w.StartsWith(t, StringComparison.Ordinal))));

        const double k1 = 1.2, b = 0.75;
        return chunks
            .Select((c, i) =>
            {
                var words = tokenized[i];
                var score = terms.Sum(t =>
                {
                    var tf = words.Count(w => w.StartsWith(t, StringComparison.Ordinal)); // prefix: "rate" matches "ratei"
                    if (tf == 0) return 0;
                    var idf = Math.Log(1 + (n - df[t] + 0.5) / (df[t] + 0.5));
                    return idf * tf * (k1 + 1) / (tf + k1 * (1 - b + b * words.Length / avgLength));
                });
                return new Passage(c.Doc, c.Index, c.Text, Math.Round(score, 3));
            })
            .Where(p => p.Score > 0)
            .OrderByDescending(p => p.Score)
            .Take(top)
            .ToList();
    }

    private static IEnumerable<string> Chunk(string text)
    {
        for (var start = 0; start < text.Length; start += ChunkSize - Overlap)
        {
            yield return text.Substring(start, Math.Min(ChunkSize, text.Length - start));
            if (start + ChunkSize >= text.Length) yield break;
        }
    }
}
