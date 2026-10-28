using System.Text.Json;
using HackathonStarter.Services;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Agents;

/// <summary>
/// Record / replay: makes the demo (and the pitch video) identical every time.
/// Recordings are committed to git, so personal data in them is masked ([IBAN-1]) unless
/// Privacy:RedactRecordings = false (only for rehearsals that use synthetic data you want to see on screen).
/// 1. Rehearse with a real model and AI:Record = true (Development) → successful runs are saved to
///    Recordings/&lt;agent&gt;.json in the project. Commit them.
/// 2. On stage, if the model is slow or down, the closest recording is replayed with realistic timing.
///    Force it with AI:AgentMode = "Replay".
/// </summary>
public class Recordings(IHostEnvironment env, IOptions<AiOptions> options, IOptions<PrivacyOptions> privacy, ILogger<Recordings> logger)
{
    public record Recording(string Input, List<AgentStep> Steps, DateTime RecordedAt);

    private string Dir => Path.Combine(env.ContentRootPath, "Recordings");

    /// <summary>
    /// The recording to replay for this question: an exact match, or the most similar one if its similarity
    /// reaches AI:ReplayMinSimilarity. Below that it returns null (better a mock than an answer to another question).
    /// </summary>
    public (Recording Recording, bool Exact)? Find(string agent, string input)
    {
        if (privacy.Value.RedactRecordings) input = PiiVault.ForStorage().Mask(input); // compare like with like
        var list = Load(agent);
        if (list.Count == 0) return null;

        var exact = list.FirstOrDefault(r => Normalize(r.Input) == Normalize(input));
        if (exact is not null) return (exact, true);

        var best = list.Select(r => (Recording: r, Score: Similarity(r.Input, input))).MaxBy(x => x.Score);
        return best.Score >= options.Value.ReplayMinSimilarity ? (best.Recording, false) : null;
    }

    /// <summary>Jaccard similarity of the two questions' words (0 = nothing in common, 1 = same words).</summary>
    public static double Similarity(string a, string b)
    {
        var wa = Words(a);
        var wb = Words(b);
        var union = wa.Union(wb).Count();
        return union == 0 ? 0 : (double)wa.Intersect(wb).Count() / union;
    }

    public void Save(string agent, string input, List<AgentStep> steps)
    {
        if (!options.Value.Record || !env.IsDevelopment()) return;
        try
        {
            Directory.CreateDirectory(Dir);
            var vault = PiiVault.ForStorage();
            if (privacy.Value.RedactRecordings) input = vault.Mask(input);
            var clean = RunRedaction.Redact(steps, privacy.Value.RedactRecordings ? vault : null)
                .Select(copy =>
                {
                    if (copy.Kind == StepKind.Approval) { copy.State = ApprovalState.Pending; copy.DecidedBy = null; }
                    return copy;
                }).ToList();

            var list = Load(agent).Where(r => Normalize(r.Input) != Normalize(input)).ToList();
            list.Insert(0, new Recording(input, clean, DateTime.UtcNow));
            File.WriteAllText(Path.Combine(Dir, $"{agent}.json"),
                JsonSerializer.Serialize(list.Take(20), new JsonSerializerOptions(RunStore.Json) { WriteIndented = true }));
            logger.LogInformation("Recorded \"{Input}\" in Recordings/{Agent}.json", input, agent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save recording");
        }
    }

    private List<Recording> Load(string agent)
    {
        var file = Path.Combine(Dir, $"{agent}.json");
        if (!File.Exists(file)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Recording>>(File.ReadAllText(file), RunStore.Json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid recordings file {File}", file);
            return [];
        }
    }

    private static string Normalize(string s) => string.Join(' ', TextMatch.Words(s));

    private static HashSet<string> Words(string s) => TextMatch.Words(s).ToHashSet();
}
