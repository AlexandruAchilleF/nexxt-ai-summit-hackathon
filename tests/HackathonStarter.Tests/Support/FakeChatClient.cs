using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Tests.Support;

/// <summary>
/// Scripted IChatClient: each call to the model consumes the next scripted response.
/// Lets us test the agent runtime (tools, JSON mode, fallbacks, privacy) without network or API keys.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly Queue<Func<IList<ChatMessage>, ChatResponse>> _script = new();
    private int _callIds;

    /// <summary>Messages received on every call, in order.</summary>
    public List<List<ChatMessage>> Calls { get; } = [];
    /// <summary>Options received on every call (tools offered, tool mode), in the same order as <see cref="Calls"/>.</summary>
    public List<ChatOptions?> CallOptions { get; } = [];
    /// <summary>Simulates a model/provider without tool calling: requests with tools throw.</summary>
    public bool RejectTools { get; set; }
    public TimeSpan Delay { get; set; }

    public FakeChatClient Reply(string text) => Enqueue(_ => Text(text));
    public FakeChatClient ReplyWith(Func<IList<ChatMessage>, string> text) => Enqueue(m => Text(text(m)));
    public FakeChatClient Json(object value) => Reply(JsonSerializer.Serialize(value, Web));
    public FakeChatClient Fail(string message) => Enqueue(_ => throw new HttpRequestException(message));

    public FakeChatClient CallTool(string name, object args)
    {
        var id = $"call_{++_callIds}";
        var dict = JsonSerializer.SerializeToElement(args, Web).EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        return Enqueue(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(id, name, dict)]))
        {
            Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10 },
        });
    }

    /// <summary>Last user-visible text sent to the model (system prompts excluded).</summary>
    public string AllUserText => string.Join("\n", Calls.SelectMany(c => c).Where(m => m.Role != ChatRole.System).Select(m => m.Text));

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        Calls.Add(list);
        CallOptions.Add(options);
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
        if (RejectTools && options?.Tools is { Count: > 0 }) throw new NotSupportedException("tools are not supported by this model");
        if (_script.Count == 0) throw new InvalidOperationException("FakeChatClient: script exhausted");
        return _script.Dequeue()(list);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates()) yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private FakeChatClient Enqueue(Func<IList<ChatMessage>, ChatResponse> step)
    {
        _script.Enqueue(step);
        return this;
    }

    private static ChatResponse Text(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { Usage = new UsageDetails { InputTokenCount = 200, OutputTokenCount = 30 } };
}
