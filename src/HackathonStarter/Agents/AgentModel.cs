using System.Text.Json;
using HackathonStarter.Config;
using HackathonStarter.Features.Checks;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Agents;

/// <summary>One step of an agent run. The UI renders the list of steps as a live timeline.</summary>
public class AgentStep
{
    public StepKind Kind { get; set; }
    public string Id { get; set; } = NewId();
    /// <summary>Speaker name in multi-agent panels.</summary>
    public string? Agent { get; set; }
    public string Text { get; set; } = "";
    public string? Tool { get; set; }
    /// <summary>Tool input / output as JSON.</summary>
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    /// <summary>Approval: what the agent wants to do, in plain words.</summary>
    public string? Summary { get; set; }
    public ApprovalState? State { get; set; }
    public string? DecidedBy { get; set; }
    /// <summary>Verdict (panels): winner, scores 1-10, rationale in <see cref="Text"/>.</summary>
    public string? Winner { get; set; }
    public Dictionary<string, int>? Scores { get; set; }
    /// <summary>Deterministic check results (<see cref="StepKind.Check"/>); the report fingerprint is in <see cref="Output"/>.</summary>
    public List<CheckResult>? Checks { get; set; }
    /// <summary>
    /// Pending approval only: the real arguments, encrypted, so the action can run once approved even though
    /// <see cref="Input"/> is saved with personal data masked. Removed once the action is decided.
    /// </summary>
    public string? SealedInput { get; set; }

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}

public enum StepKind { Text, Reasoning, ToolCall, ToolResult, Approval, Verdict, Check }

public enum ApprovalState { Pending, Approved, Rejected }

public enum RunStatus { Running, Completed, AwaitingApproval, Cancelled, Error }

/// <summary>live = real model · replay = recorded run · mock = scripted fallback using real tools.</summary>
public enum RunSource { Live, Replay, Mock }

public class AgentRun
{
    public string Id { get; set; } = AgentStep.NewId();
    /// <summary>Groups the runs of one chat, so a conversation survives page reloads and reconnects.</summary>
    public string? ConversationId { get; set; }
    public string Agent { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Input { get; set; } = "";
    public RunStatus Status { get; set; } = RunStatus.Running;
    public RunSource Source { get; set; } = RunSource.Live;
    public List<AgentStep> Steps { get; set; } = [];
    public string? Error { get; set; }
    /// <summary>Set when the live model failed and a fallback took over.</summary>
    public string? FallbackReason { get; set; }
    /// <summary>Replay of a recording made for a different (similar) question: shown so the presenter notices.</summary>
    public string? ReplayOf { get; set; }
    public string? Model { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    /// <summary>From AI:InputPricePerMillion / OutputPricePerMillion (USD). Null when prices aren't configured.</summary>
    public decimal? EstimatedCost { get; set; }
    /// <summary>Personal data items masked before anything was sent to the model.</summary>
    public int MaskedCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    /// <summary>Thread-safe copy of the steps, for rendering while a background run is still writing.</summary>
    public List<AgentStep> Snapshot()
    {
        lock (Steps) return [.. Steps];
    }
}

/// <summary>Database row for a run (steps stored as JSON).</summary>
public class AgentRunRecord
{
    public string Id { get; set; } = "";
    public string? ConversationId { get; set; }
    public string Agent { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Input { get; set; } = "";
    public string Status { get; set; } = "";
    public string Source { get; set; } = "";
    public string StepsJson { get; set; } = "[]";
    public string? Error { get; set; }
    public string? ReplayOf { get; set; }
    public string? FallbackReason { get; set; }
    public string? Model { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public int MaskedCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public record ChatTurn(string Role, string Content);

/// <summary>An uploaded document converted to text, or an image (sent as-is to vision-capable models).</summary>
public record AgentFile(string Id, string Name, string Text, int Pages, byte[]? Image = null, string? MediaType = null)
{
    public bool IsImage => Image is not null;
}

/// <summary>What tools receive: the current user, attached files and DI services.</summary>
public record ToolContext(DemoUser User, IReadOnlyList<AgentFile> Files, IServiceProvider Services);

/// <summary>
/// A tool the agent can call. The C# delegate's parameters become the JSON schema the model sees,
/// so use clear parameter names and [Description] attributes.
/// </summary>
public class AgentTool
{
    public required AIFunction Function { get; init; }
    public string Name => Function.Name;
    public string Description => Function.Description;
    /// <summary>Human-in-the-loop: the agent only PROPOSES the call; a user approves it in the UI, then it runs.</summary>
    public bool NeedsApproval { get; init; }
    /// <summary>Plain-language description of a proposed call, shown on the approval card.</summary>
    public Func<IReadOnlyDictionary<string, JsonElement>, string>? Summarize { get; init; }

    public static AgentTool Create(
        string name, string description, Delegate run,
        bool needsApproval = false, Func<IReadOnlyDictionary<string, JsonElement>, string>? summarize = null) =>
        new()
        {
            Function = AIFunctionFactory.Create(run, name, description),
            NeedsApproval = needsApproval,
            Summarize = summarize,
        };
}

public abstract class AgentDefinition
{
    /// <summary>URL-safe id.</summary>
    public abstract string Name { get; }
    public abstract string Label { get; }
    public abstract string Description { get; }
    /// <summary>Prompt chips shown in the empty chat.</summary>
    public virtual string[] Suggestions => [];
    /// <summary>
    /// Deterministic checks (ids from <see cref="CheckEngine"/>) run on the agent's answer after every run, live,
    /// replayed or mocked. The result is a step in the timeline: same answer, same verdict, no model involved.
    /// </summary>
    public virtual string[] Checks => [];
    /// <summary>Model profile (AI:Profiles:&lt;name&gt;) this agent runs on. Null or unknown = the default model.</summary>
    public virtual string? ModelProfile => null;
}

/// <summary>An LLM that calls your tools in a loop until it can answer.</summary>
public abstract class ToolAgent : AgentDefinition
{
    public abstract string SystemPrompt(ToolContext ctx);
    public abstract IEnumerable<AgentTool> Tools(ToolContext ctx);
    public virtual int? MaxSteps => null;

    /// <summary>
    /// Scripted stand-in for the LLM when no model is available and no recording matches.
    /// Call your REAL tools with <see cref="MockContext.CallAsync"/> so the demo shows real data.
    /// </summary>
    public abstract Task MockAsync(MockContext mock);
}

/// <param name="Profile">Model profile (AI:Profiles:&lt;name&gt;) for this participant, e.g. attacker and judge on different models.</param>
public record PanelParticipant(string Name, string Role, string SystemPrompt, string? Profile = null);

public record PanelScript(List<(string Agent, string Text)> Turns, string Winner, Dictionary<string, int> Scores, string Rationale);

/// <summary>Multi-agent panel: participants take turns on a topic, then a judge scores them.</summary>
public abstract class PanelAgent : AgentDefinition
{
    public abstract PanelParticipant[] Participants { get; }
    public abstract PanelParticipant Judge { get; }
    public virtual int Rounds => 2;
    public abstract PanelScript Mock(string topic);
}

/// <summary>
/// A multi-turn conversation with an external target (<see cref="Features.Targets.TargetClient"/>): the
/// <see cref="Driver"/> (a model playing a customer, a tester, an attacker…) writes each user message, the target
/// answers, and the <see cref="StopOn"/> checks run on every answer; the conversation stops at the first failure.
/// The run's input is the goal. Without a model the driver plays <see cref="MockTurns"/>.
/// The final <see cref="AgentDefinition.Checks"/> report covers only the target's answers, never the driver's.
/// </summary>
public abstract class ScenarioAgent : AgentDefinition
{
    /// <summary>Name of a target in the "Targets" configuration section.</summary>
    public abstract string Target { get; }
    public virtual string TargetLabel => "Target";
    /// <summary>The simulated side. Its <see cref="PanelParticipant.Profile"/> picks the model (e.g. "driver").</summary>
    public abstract PanelParticipant Driver { get; }
    public virtual int MaxTurns => 4;
    /// <summary>Checks evaluated on each answer; a failure ends the conversation. Default: the agent's checks.</summary>
    public virtual string[] StopOn => Checks;
    /// <summary>Scripted driver messages when no model is available (the first one is usually the goal itself).</summary>
    public abstract string[] MockTurns(string goal);
}

public class MockContext
{
    public required string Input { get; init; }
    public required ToolContext Context { get; init; }
    /// <summary>Calls a real tool (shown in the timeline) and returns its JSON result.</summary>
    public required Func<string, object, Task<JsonElement>> CallAsync { get; init; }
    /// <summary>Streams text to the user.</summary>
    public required Func<string, Task> SayAsync { get; init; }
}
