using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace HackathonStarter.Features.Targets;

/// <summary>
/// An external system the app talks to: a chatbot under test, a sponsor's API, a model on your own server.
/// Configured in the "Targets" section, by name:
/// <code>
/// "Targets": {
///   "bank-bot":  { "Kind": "OpenAI", "Endpoint": "http://gpu-box:8000/v1", "Model": "qwen3-8b", "SystemPrompt": "You are a bank assistant…" },
///   "partner":   { "Kind": "Http", "Endpoint": "https://api.example.com/chat", "ApiKey": "…",
///                  "RequestTemplate": "{\"question\": {{message}}}", "ResponsePath": "data.answer" },
///   "assistant": { "Kind": "Agent", "Agent": "assistant" }
/// }
/// </code>
/// Messages are sent as-is (no masking): a target is often the system being tested for leaks, so it must
/// see the real (synthetic) data. What is saved is masked as usual.
/// </summary>
public class TargetOptions
{
    /// <summary>OpenAI (any OpenAI-compatible chat API) | Http (any JSON API) | Agent (an agent of this app).</summary>
    public string Kind { get; set; } = "OpenAI";
    public string? Label { get; set; }
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    /// <summary>OpenAI targets: the system prompt the target runs with (e.g. the assistant's own instructions).</summary>
    public string? SystemPrompt { get; set; }
    /// <summary>Agent targets: name of a registered agent.</summary>
    public string? Agent { get; set; }
    /// <summary>
    /// Http targets: request body; {{message}} = last user message, {{messages}} = the conversation
    /// [{role, content}], both inserted as JSON; {{text}} = last user message as-is (a SOAP envelope, say).
    /// Default fits this app's own /api/agents/{name}?stream=false.
    /// </summary>
    public string RequestTemplate { get; set; } = """{"messages": {{messages}}}""";
    /// <summary>Http targets: Content-Type of the request body ("text/xml; charset=utf-8" for SOAP).</summary>
    public string ContentType { get; set; } = "application/json";
    /// <summary>
    /// Http targets: dotted path to the answer in the JSON response ("answer", "choices.0.message.content");
    /// empty = the whole response body, untouched (XML, plain text).
    /// </summary>
    public string ResponsePath { get; set; } = "answer";
    /// <summary>Http targets: header carrying ApiKey ("Authorization" sends "Bearer &lt;key&gt;").</summary>
    public string AuthHeader { get; set; } = "Authorization";
    public Dictionary<string, string> Headers { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>live = the target answered · replay = recorded answer (target down) · unavailable = neither.</summary>
public enum TargetSource { Live, Replay, Unavailable }

public record TargetReply(string Text, TargetSource Source, long ElapsedMs, string? Error = null);

public record TargetInfo(string Name, string Label, string Kind, string? Host);

public class TargetClient(
    IOptionsMonitor<Dictionary<string, TargetOptions>> targets,
    IHttpClientFactory http,
    TargetRecordings recordings,
    IServiceScopeFactory scopes,
    ILogger<TargetClient> logger)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<TargetInfo> List() =>
        [.. targets.CurrentValue.Select(t => new TargetInfo(t.Key, t.Value.Label ?? t.Key, t.Value.Kind, Host(t.Value)))];

    public TargetOptions? Find(string name) =>
        targets.CurrentValue.FirstOrDefault(t => t.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// Sends the conversation (ending with the user's message) and returns the target's answer. Never throws for a
    /// target failure: falls back to a recorded answer for the same conversation, otherwise reports Unavailable.
    /// </summary>
    public async Task<TargetReply> SendAsync(string name, IReadOnlyList<ChatTurn> conversation, CancellationToken ct = default)
    {
        var target = Find(name) ?? throw new KeyNotFoundException($"Unknown target '{name}'. Configure it in Targets:{name}.");
        var watch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(target.TimeoutSeconds));
            var text = target.Kind.ToLowerInvariant() switch
            {
                "openai" => await SendOpenAiAsync(target, conversation, timeout.Token),
                "http" => await SendHttpAsync(target, conversation, timeout.Token),
                "agent" => await SendAgentAsync(target, conversation, timeout.Token),
                _ => throw new InvalidOperationException($"Unknown target kind '{target.Kind}' (OpenAI, Http or Agent)."),
            };
            recordings.Save(name, conversation, text);
            return new TargetReply(text, TargetSource.Live, watch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // A SOAP transport exception may echo the request just as a fault body can. Sanitize before both the log and reply.
            var error = ex is OperationCanceledException ? $"no answer within {target.TimeoutSeconds}s"
                : IsSoap(target)
                    ? ex is HttpRequestException { StatusCode: { } status } ? $"HTTP {(int)status}" : "SOAP transport failed"
                    : ex.Message;
            logger.LogWarning("Target {Target} failed: {Error}", name, error);
            return recordings.Find(name, conversation) is { } recorded
                ? new TargetReply(recorded, TargetSource.Replay, watch.ElapsedMilliseconds, error)
                : new TargetReply("", TargetSource.Unavailable, watch.ElapsedMilliseconds, error);
        }
    }

    private static async Task<string> SendOpenAiAsync(TargetOptions t, IReadOnlyList<ChatTurn> conversation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(t.Model)) throw new InvalidOperationException("OpenAI targets need a Model.");
        var client = new OpenAIClient(
                new ApiKeyCredential(string.IsNullOrEmpty(t.ApiKey) ? "not-needed" : t.ApiKey),
                new OpenAIClientOptions
                {
                    Endpoint = string.IsNullOrEmpty(t.Endpoint) ? null : new Uri(t.Endpoint),
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 1),
                })
            .GetChatClient(t.Model)
            .AsIChatClient();
        List<ChatMessage> messages = string.IsNullOrEmpty(t.SystemPrompt) ? [] : [new(ChatRole.System, t.SystemPrompt)];
        messages.AddRange(conversation.Select(c => new ChatMessage(c.Role == "user" ? ChatRole.User : ChatRole.Assistant, c.Content)));
        var response = await client.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text;
    }

    private async Task<string> SendHttpAsync(TargetOptions t, IReadOnlyList<ChatTurn> conversation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(t.Endpoint)) throw new InvalidOperationException("Http targets need an Endpoint.");
        var body = t.RequestTemplate
            .Replace("{{messages}}", JsonSerializer.Serialize(conversation.Select(c => new { role = c.Role, content = c.Content }), Web))
            .Replace("{{message}}", JsonSerializer.Serialize(conversation.LastOrDefault(c => c.Role == "user")?.Content ?? ""))
            .Replace("{{text}}", conversation.LastOrDefault(c => c.Role == "user")?.Content ?? "");

        var contentType = MediaTypeHeaderValue.Parse(t.ContentType);
        contentType.CharSet ??= "utf-8";
        using var request = new HttpRequestMessage(HttpMethod.Post, t.Endpoint) { Content = new StringContent(body, Encoding.UTF8, contentType) };
        if (!string.IsNullOrEmpty(t.ApiKey))
            request.Headers.TryAddWithoutValidation(t.AuthHeader, t.AuthHeader.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? $"Bearer {t.ApiKey}" : t.ApiKey);
        foreach (var (key, value) in t.Headers) request.Headers.TryAddWithoutValidation(key, value);

        using var response = await http.CreateClient("targets").SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // SOAP faults can echo identifiers from the request. Keep them out of diagnostics and logs.
            var message = IsSoapMediaType(contentType.MediaType)
                ? $"HTTP {(int)response.StatusCode}"
                : $"HTTP {(int)response.StatusCode}: {text[..Math.Min(text.Length, 200)]}";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return ReadPath(text, t.ResponsePath);
    }

    private static bool IsSoap(TargetOptions target) =>
        string.Equals(target.Kind, "Http", StringComparison.OrdinalIgnoreCase)
        && MediaTypeHeaderValue.TryParse(target.ContentType, out var type) && IsSoapMediaType(type.MediaType);

    private static bool IsSoapMediaType(string? mediaType) =>
        string.Equals(mediaType, "text/xml", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/soap+xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs an agent of this app as the target, in its own scope (no HTTP, works with zero configuration).</summary>
    private async Task<string> SendAgentAsync(TargetOptions t, IReadOnlyList<ChatTurn> conversation, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetServices<AgentDefinition>().FirstOrDefault(a => a.Name == t.Agent)
                    ?? throw new InvalidOperationException($"Agent '{t.Agent}' is not registered.");
        if (agent is ScenarioAgent) throw new InvalidOperationException("A scenario can't be a target.");
        scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(SiteConfig.Users.First(u => u.Role == Roles.Viewer));
        var run = await scope.ServiceProvider.GetRequiredService<AgentRunner>()
            .RunAsync(agent, conversation, [], _ => Task.CompletedTask, ct);
        return string.Join("\n\n", run.Steps.Where(s => s.Kind == StepKind.Text && !string.IsNullOrWhiteSpace(s.Text)).Select(s => s.Text));
    }

    /// <summary>"choices.0.message.content" → the value at that path (strings as-is, anything else as JSON); "" → all of it.</summary>
    public static string ReadPath(string json, string path)
    {
        if (string.IsNullOrEmpty(path)) return json;
        using var doc = JsonDocument.Parse(json);
        var node = doc.RootElement;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node.ValueKind switch
            {
                JsonValueKind.Array when int.TryParse(part, out var i) && i < node.GetArrayLength() => node[i],
                JsonValueKind.Object when TryGetProperty(node, part, out var child) => child,
                _ => throw new InvalidOperationException($"The response has no '{path}' (stopped at '{part}')."),
            };
        }
        return node.ValueKind == JsonValueKind.String ? node.GetString()! : node.GetRawText();
    }

    private static bool TryGetProperty(JsonElement node, string name, out JsonElement value)
    {
        foreach (var p in node.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        value = default;
        return false;
    }

    private static string? Host(TargetOptions t) =>
        t.Kind.Equals("Agent", StringComparison.OrdinalIgnoreCase) ? "this app"
        : Uri.TryCreate(t.Endpoint, UriKind.Absolute, out var uri) ? uri.Host
        : t.Kind.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ? "api.openai.com" : null;
}
