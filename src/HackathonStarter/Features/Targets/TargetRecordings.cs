using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Services;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Features.Targets;

/// <summary>
/// Record / replay for external targets, like <see cref="Recordings"/> for agents: rehearse with AI:Record = true
/// (Development), commit Recordings/targets/&lt;name&gt;.json, and on stage a target that is down answers with its
/// recorded reply to the same conversation. Personal data is masked unless Privacy:RedactRecordings = false.
/// </summary>
public class TargetRecordings(IHostEnvironment env, IOptions<AiOptions> ai, IOptions<PrivacyOptions> privacy, ILogger<TargetRecordings> logger)
{
    public record Entry(string Conversation, string Reply, DateTime RecordedAt);

    private readonly Lock _lock = new();
    private string Dir => Path.Combine(env.ContentRootPath, "Recordings", "targets");

    public string? Find(string target, IReadOnlyList<ChatTurn> conversation)
    {
        var key = Key(conversation);
        return Load(target).FirstOrDefault(e => e.Conversation == key)?.Reply;
    }

    public void Save(string target, IReadOnlyList<ChatTurn> conversation, string reply)
    {
        if (!ai.Value.Record || !env.IsDevelopment()) return;
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Dir);
                var key = Key(conversation);
                var stored = privacy.Value.RedactRecordings ? PiiVault.ForStorage().Mask(reply) : reply;
                var list = Load(target).Where(e => e.Conversation != key).Prepend(new Entry(key, stored, DateTime.UtcNow)).Take(200);
                File.WriteAllText(PathFor(target), JsonSerializer.Serialize(list, new JsonSerializerOptions(RunStore.Json) { WriteIndented = true }));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save target recording");
        }
    }

    /// <summary>The conversation as one line per turn: masked first (so no personal data is stored), then normalized.</summary>
    private string Key(IReadOnlyList<ChatTurn> conversation)
    {
        var vault = privacy.Value.RedactRecordings ? PiiVault.ForStorage() : null;
        return string.Join("\n", conversation.Select(c =>
            $"{c.Role}: {string.Join(' ', TextMatch.Words(vault?.Mask(c.Content) ?? c.Content))}"));
    }

    private List<Entry> Load(string target)
    {
        var file = PathFor(target);
        if (!File.Exists(file)) return [];
        try { return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(file), RunStore.Json) ?? []; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid target recordings file {File}", file);
            return [];
        }
    }

    private string PathFor(string target) => Path.Combine(Dir, $"{string.Concat(target.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'))}.json");
}
