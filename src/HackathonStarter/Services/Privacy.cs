namespace HackathonStarter.Services;

/// <summary>Privacy settings for data at rest, from the "Privacy" section. Run history is always masked.</summary>
public class PrivacyOptions
{
    /// <summary>
    /// Mask personal data in Recordings/*.json (they are committed to git). Set to false only for rehearsals
    /// with synthetic data, when you want the real-looking values on screen in replays.
    /// </summary>
    public bool RedactRecordings { get; set; } = true;

    /// <summary>
    /// Agent runs older than this many days are deleted automatically (GDPR storage limitation). 0 = keep forever.
    /// Exports are kept: they are masked and approved on purpose (tests, reports).
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}

/// <summary>Where data goes, derived from the live configuration (shown on the Data and privacy page).</summary>
public static class DataFlows
{
    public record ModelEndpoint(string Name, string Provider, string Model, string Host, bool OnOwnInfrastructure);

    /// <summary>The main model and every usable profile; empty when no model is configured (replay / mock only).</summary>
    public static List<ModelEndpoint> Models(ChatClientProvider ai)
    {
        var list = new List<ModelEndpoint>();
        if (!ai.IsConfigured) return list;
        list.Add(Describe("main", ai.Options));
        foreach (var name in ai.Options.Profiles.Keys)
            if (ai.ClientFor(name) is { } client && client != ai.Client) list.Add(Describe(name, ai.OptionsFor(name)));
        return list;
    }

    private static ModelEndpoint Describe(string name, AiOptions o)
    {
        var host = Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri) ? uri.Host
            : o.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) ? "api.anthropic.com"
            : o.Provider.Equals("Bedrock", StringComparison.OrdinalIgnoreCase) ? $"bedrock-runtime.{o.BedrockRegion}.amazonaws.com"
            : "api.openai.com";
        return new ModelEndpoint(name, o.Provider, o.Model, host, IsOwnInfrastructure(host));
    }

    /// <summary>localhost, a private network address or a single-label / .local / .internal host name.</summary>
    public static bool IsOwnInfrastructure(string? host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        if (host is "localhost" || !host.Contains('.') || host.EndsWith(".local") || host.EndsWith(".internal") || host.EndsWith(".lan")) return true;
        if (!System.Net.IPAddress.TryParse(host, out var ip)) return false;
        if (System.Net.IPAddress.IsLoopback(ip)) return true;
        var b = ip.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127));
    }
}
