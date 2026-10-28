using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Amazon;
using Amazon.BedrockRuntime;
using Anthropic;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI;

namespace HackathonStarter.Services;

/// <summary>
/// AI settings, from the "AI" section of appsettings.json or environment variables (AI__ApiKey=...).
/// Put secrets in appsettings.Local.json (git-ignored) or `dotnet user-secrets`, never in appsettings.json.
/// </summary>
public class AiOptions
{
    /// <summary>None | Anthropic | Bedrock | OpenAI (any OpenAI-compatible API: OpenAI, Featherless, Ollama, Groq…) | AzureOpenAI</summary>
    public string Provider { get; set; } = "None";
    /// <summary>Model id (or the deployment name for Azure OpenAI).</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";
    public string? ApiKey { get; set; }
    /// <summary>Base URL for OpenAI-compatible APIs (e.g. https://api.featherless.ai/v1) or the Azure OpenAI endpoint.</summary>
    public string? Endpoint { get; set; }
    /// <summary>
    /// AWS region for Bedrock (default us-west-2). Credentials come from the standard AWS chain (environment variables,
    /// profile, or the IAM role of the container), never from this file.
    /// </summary>
    public string? Region { get; set; }
    /// <summary>true if the endpoint enforces JSON schemas (OpenAI, Azure OpenAI). Otherwise the schema goes in the prompt.</summary>
    public bool StructuredOutputs { get; set; }

    /// <summary>Auto (native tool calling, JSON fallback) | Native | Json (works with any model).</summary>
    public string ToolMode { get; set; } = "Auto";
    /// <summary>Auto (live → recorded run → mock) | Live (no fallback) | Replay (never call the model).</summary>
    public string AgentMode { get; set; } = "Auto";
    /// <summary>Development only: save successful live agent runs to Recordings/ (commit them).</summary>
    public bool Record { get; set; }
    public int MaxSteps { get; set; } = 8;
    public int TimeoutSeconds { get; set; } = 60;
    /// <summary>A single tool call taking longer than this becomes an error the agent can recover from.</summary>
    public int ToolTimeoutSeconds { get; set; } = 15;
    /// <summary>Replay a recording only if its question is at least this similar (0-1). Below: scripted mock.</summary>
    public double ReplayMinSimilarity { get; set; } = 0.5;
    /// <summary>Conversation history sent to the model is trimmed to roughly this many characters (latest kept).</summary>
    public int MaxHistoryChars { get; set; } = 24_000;
    /// <summary>Mask personal data (CNP, IBAN, card, email, phone) before anything reaches the model.</summary>
    public bool MaskPii { get; set; } = true;
    /// <summary>Prices in USD per 1M tokens, for the cost estimate on each run (0 = don't show).</summary>
    public decimal InputPricePerMillion { get; set; }
    public decimal OutputPricePerMillion { get; set; }

    /// <summary>
    /// Named models for different roles, e.g. "attacker" on a local open-weight model, "judge" on another provider:
    /// AI:Profiles:attacker:Model = "...". Unset fields inherit from the main AI settings above; agent behaviour
    /// settings (ToolMode, AgentMode, timeouts, masking) always come from the main settings.
    /// </summary>
    public Dictionary<string, AiProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>These options with a profile's overrides applied (or unchanged if the profile isn't configured).</summary>
    public AiOptions ForProfile(string? profile)
    {
        var p = FindProfile(profile);
        if (p is null) return this;
        var o = (AiOptions)MemberwiseClone();
        // Another provider or endpoint must not inherit the main API key (it would be sent to the wrong service).
        var sameService = (p.Provider is null || p.Provider.Equals(Provider, StringComparison.OrdinalIgnoreCase))
                          && (p.Endpoint is null || p.Endpoint == Endpoint);
        o.Provider = p.Provider ?? Provider;
        o.Model = p.Model ?? Model;
        o.Endpoint = p.Endpoint ?? Endpoint;
        o.Region = p.Region ?? Region;
        o.ApiKey = p.ApiKey ?? (sameService ? ApiKey : null);
        o.StructuredOutputs = p.StructuredOutputs ?? StructuredOutputs;
        o.InputPricePerMillion = p.InputPricePerMillion ?? InputPricePerMillion;
        o.OutputPricePerMillion = p.OutputPricePerMillion ?? OutputPricePerMillion;
        return o;
    }

    /// <summary>The Bedrock region actually used: <see cref="Region"/> or us-west-2.</summary>
    public string BedrockRegion => string.IsNullOrEmpty(Region) ? "us-west-2" : Region;

    public AiProfile? FindProfile(string? profile) =>
        string.IsNullOrEmpty(profile) ? null
        : Profiles.FirstOrDefault(p => p.Key.Equals(profile, StringComparison.OrdinalIgnoreCase)).Value;
}

/// <summary>Overrides for one model profile; null = same as the main AI settings.</summary>
public class AiProfile
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public bool? StructuredOutputs { get; set; }
    public decimal? InputPricePerMillion { get; set; }
    public decimal? OutputPricePerMillion { get; set; }
}

/// <summary>Builds the configured <see cref="IChatClient"/> (Microsoft.Extensions.AI), or null when AI isn't set up.</summary>
public class ChatClientProvider
{
    private readonly Lazy<IChatClient?> _client;
    private readonly ConcurrentDictionary<string, Lazy<IChatClient?>> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, IChatClient>? _fixedProfiles;
    private readonly ILogger _logger;

    public ChatClientProvider(IOptions<AiOptions> options, ILogger<ChatClientProvider> logger)
    {
        Options = options.Value;
        _logger = logger;
        _client = new(() => Create(Options, logger));
    }

    private ChatClientProvider(AiOptions options, IChatClient? client, IReadOnlyDictionary<string, IChatClient>? profiles)
    {
        Options = options;
        _logger = NullLogger.Instance;
        _client = new(() => client);
        _fixedProfiles = profiles;
    }

    /// <summary>For tests: a fixed client (e.g. a scripted fake) instead of the configured provider, and optionally one per profile.</summary>
    public static ChatClientProvider Fixed(AiOptions options, IChatClient? client, IReadOnlyDictionary<string, IChatClient>? profiles = null) =>
        new(options, client, profiles);

    public AiOptions Options { get; }
    public IChatClient? Client => _client.Value;
    public bool IsConfigured => Client is not null;

    /// <summary>
    /// The client for a model profile (AI:Profiles:&lt;name&gt;). Falls back to the main client when the profile is
    /// null, not configured or can't be created, so a missing profile never breaks the demo.
    /// </summary>
    public IChatClient? ClientFor(string? profile)
    {
        if (string.IsNullOrEmpty(profile)) return Client;
        if (_fixedProfiles is not null)
            return _fixedProfiles.FirstOrDefault(p => p.Key.Equals(profile, StringComparison.OrdinalIgnoreCase)).Value ?? Client;
        if (Options.FindProfile(profile) is null) return Client;
        return _profiles.GetOrAdd(profile, name => new(() =>
        {
            var client = Create(Options.ForProfile(name), _logger);
            if (client is null) _logger.LogWarning("Model profile {Profile} is not usable; using the main model", name);
            return client;
        })).Value ?? Client;
    }

    /// <summary>The settings a profile runs with (model name, tool mode, prices…).</summary>
    public AiOptions OptionsFor(string? profile) =>
        _fixedProfiles is not null || ClientFor(profile) != Client ? Options.ForProfile(profile) : Options;

    private static IChatClient? Create(AiOptions o, ILogger logger)
    {
        try
        {
            return o.Provider.ToLowerInvariant() switch
            {
                "anthropic" when !string.IsNullOrEmpty(o.ApiKey) =>
                    new AnthropicClient { ApiKey = o.ApiKey }.AsIChatClient(o.Model, 4096),

                "openai" when !string.IsNullOrEmpty(o.Endpoint) || !string.IsNullOrEmpty(o.ApiKey) =>
                    new OpenAIClient(
                            new ApiKeyCredential(string.IsNullOrEmpty(o.ApiKey) ? "not-needed" : o.ApiKey),
                            new OpenAIClientOptions
                            {
                                Endpoint = string.IsNullOrEmpty(o.Endpoint) ? null : new Uri(o.Endpoint),
                                // Fail fast: the agent fallback chain is our retry strategy.
                                RetryPolicy = new ClientRetryPolicy(maxRetries: 1),
                            })
                        .GetChatClient(o.Model)
                        .AsIChatClient(),

                "bedrock" =>
                    new AmazonBedrockRuntimeClient(new AmazonBedrockRuntimeConfig
                        {
                            RegionEndpoint = RegionEndpoint.GetBySystemName(o.BedrockRegion),
                            // Fail fast: the agent fallback chain is our retry strategy.
                            MaxErrorRetry = 1,
                        })
                        .AsIChatClient(o.Model)
                        .AsBuilder()
                        // Same output budget as the Anthropic client above.
                        .ConfigureOptions(options => options.MaxOutputTokens ??= 4096)
                        .Build(),

                "azureopenai" when !string.IsNullOrEmpty(o.Endpoint) && !string.IsNullOrEmpty(o.ApiKey) =>
                    new AzureOpenAIClient(new Uri(o.Endpoint), new ApiKeyCredential(o.ApiKey),
                            new AzureOpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(maxRetries: 1) })
                        .GetChatClient(o.Model)
                        .AsIChatClient(),

                _ => null,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not create the AI client; agents will use recordings/mocks");
            return null;
        }
    }
}

/// <summary>
/// One-shot AI call with a typed result. Falls back to <paramref name="mock"/> when AI isn't configured,
/// the call fails or times out, so the UI keeps working (and the demo can't die because of the Wi-Fi).
/// </summary>
public class StructuredAi(ChatClientProvider provider, ILogger<StructuredAi> logger)
{
    /// <param name="profile">Model profile (AI:Profiles:&lt;name&gt;); null = the main model.</param>
    public async Task<AiResult<T>> GenerateAsync<T>(string prompt, Func<T> mock, string? system = null, CancellationToken ct = default, string? profile = null)
    {
        if (provider.ClientFor(profile) is not { } client) return new(mock(), "mock", "no AI provider configured");
        var options = provider.OptionsFor(profile);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(provider.Options.TimeoutSeconds));
            List<ChatMessage> messages = system is null ? [new(ChatRole.User, prompt)] : [new(ChatRole.System, system), new(ChatRole.User, prompt)];
            var response = await client.GetResponseAsync<T>(messages, useJsonSchemaResponseFormat: options.StructuredOutputs, cancellationToken: timeout.Token);
            if (response.TryGetResult(out var result) && result is not null) return new(result, "ai");
            return new(mock(), "mock", "model returned invalid JSON");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("AI call failed, using mock: {Message}", ex.Message);
            return new(mock(), "mock", ex.Message);
        }
    }
}

public record AiResult<T>(T Data, string Source, string? Reason = null);
