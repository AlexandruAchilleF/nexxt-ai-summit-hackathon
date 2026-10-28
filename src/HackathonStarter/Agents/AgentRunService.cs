using System.Collections.Concurrent;
using HackathonStarter.Config;
using HackathonStarter.Services;

namespace HackathonStarter.Agents;

/// <summary>
/// Runs agents in the background, in their own DI scope, independent of the page that started them.
/// If the browser reloads, navigates away or the connection drops (venue Wi-Fi!), the run still finishes
/// and is saved; the chat re-attaches to it (or reloads it from the database) via its conversation id.
/// </summary>
public class AgentRunService(IServiceScopeFactory scopes, IEnumerable<AgentDefinition> agents, ILogger<AgentRunService> logger)
{
    private readonly ConcurrentDictionary<string, ActiveRun> _active = new();

    /// <summary>
    /// Saves the run (status Running) before starting it, so a reload a millisecond later still finds it,
    /// then executes it in the background.
    /// </summary>
    public async Task<ActiveRun> StartAsync(string agentName, IReadOnlyList<ChatTurn> history, IReadOnlyList<AgentFile> files, DemoUser user, string? conversationId)
    {
        var active = Start(agentName, history, files, user, conversationId, saveFirst: true);
        await active.Saved;
        return active;
    }

    public ActiveRun Start(string agentName, IReadOnlyList<ChatTurn> history, IReadOnlyList<AgentFile> files, DemoUser user, string? conversationId)
        => Start(agentName, history, files, user, conversationId, saveFirst: false);

    private ActiveRun Start(string agentName, IReadOnlyList<ChatTurn> history, IReadOnlyList<AgentFile> files, DemoUser user, string? conversationId, bool saveFirst)
    {
        var agent = agents.FirstOrDefault(a => a.Name == agentName) ?? throw new InvalidOperationException($"Agent '{agentName}' is not registered");
        var run = new AgentRun
        {
            Agent = agentName,
            ConversationId = conversationId,
            UserId = user.Id,
            Input = history.LastOrDefault(t => t.Role == "user")?.Content ?? "",
        };
        var active = new ActiveRun(run);
        _active[run.Id] = active;
        var saved = new TaskCompletionSource();
        active.Saved = saved.Task;

        active.Completion = Task.Run(async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(user);
            var runner = scope.ServiceProvider.GetRequiredService<AgentRunner>();
            if (saveFirst)
            {
                try { await scope.ServiceProvider.GetRequiredService<RunStore>().SaveAsync(run); }
                catch (Exception ex) { logger.LogWarning(ex, "Could not save run {RunId} before starting", run.Id); }
            }
            saved.TrySetResult();
            try
            {
                return await runner.RunAsync(agent, history, files, active.NotifyAsync, active.Token, run);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background run {RunId} failed", run.Id);
                run.Status = RunStatus.Error;
                run.Error = ex.Message;
                return run;
            }
            finally
            {
                _active.TryRemove(run.Id, out _);
                await active.NotifyAsync(run);
            }
        });
        return active;
    }

    /// <summary>A run that is still executing in this server process, if any.</summary>
    public ActiveRun? Get(string runId) => _active.TryGetValue(runId, out var active) ? active : null;
}

public class ActiveRun(AgentRun run)
{
    private readonly CancellationTokenSource _cts = new();

    public AgentRun Run => run;
    public Task<AgentRun> Completion { get; internal set; } = Task.FromResult(run);
    /// <summary>Completes once the run is persisted (status Running) and visible to other pages.</summary>
    public Task Saved { get; internal set; } = Task.CompletedTask;
    public CancellationToken Token => _cts.Token;

    /// <summary>Raised after every change. Handlers run without being awaited; exceptions are swallowed.</summary>
    public event Func<AgentRun, Task>? Updated;

    public void Cancel() => _cts.Cancel();

    /// <summary>
    /// Notifies subscribers without waiting for them: a page whose connection just dropped keeps its circuit
    /// (and its pending JS calls) for a while, and the run must not stall behind it.
    /// </summary>
    internal Task NotifyAsync(AgentRun r)
    {
        if (Updated is not { } handlers) return Task.CompletedTask;
        foreach (var handler in handlers.GetInvocationList().Cast<Func<AgentRun, Task>>())
            _ = Safe(handler, r);
        return Task.CompletedTask;

        static async Task Safe(Func<AgentRun, Task> handler, AgentRun run)
        {
            try { await handler(run); }
            catch { /* a disconnected page must not break the run */ }
        }
    }
}
