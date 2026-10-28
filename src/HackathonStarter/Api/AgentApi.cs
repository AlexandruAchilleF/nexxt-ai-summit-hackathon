using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Services;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Api;

/// <summary>
/// HTTP API for the agents, for anything outside the Blazor UI: a mobile app, a webhook, another team's
/// agent, a judge with curl. Interactive docs: /api-docs (OpenAPI JSON at /openapi/v1.json).
///
///   curl -N -X POST http://localhost:5000/api/agents/assistant \
///        -H "Content-Type: application/json" -d '{"messages":[{"role":"user","content":"Give me an overview"}]}'
///
/// The run endpoint streams newline-delimited JSON: run → step / delta / text / reset … → done (result).
/// With ?stream=false it waits and returns one JSON object: { runId, status, answer, pendingApprovals, run }.
/// Calling it from another origin (a Lovable/React frontend, a mobile web app): set Security:AllowedOrigins.
/// See docs/CLIENTS.md.
/// </summary>
public static class AgentApi
{
    public record MessageDto(string Role, string Content);
    /// <summary>A text document (<c>Text</c>) or an image (<c>Base64</c> + <c>MediaType</c>, e.g. a phone photo).</summary>
    public record FileDto(string Name, string? Text = null, string? Base64 = null, string? MediaType = null);
    public record RunRequest(List<MessageDto> Messages, List<FileDto>? Files = null, string? ConversationId = null);
    public record DecisionRequest(bool Approved);
    public record PendingApproval(string Id, string? Tool, string? Summary);
    /// <summary>The run, plus the two things clients usually need: the final answer and what awaits approval.</summary>
    public record RunResult(string RunId, string Status, string Source, string? Answer, string? FallbackReason,
                            List<PendingApproval> PendingApprovals, AgentRun Run)
    {
        public static RunResult From(AgentRun run)
        {
            var steps = run.Snapshot();
            return new(run.Id, run.Status.ToString(), run.Source.ToString(),
                steps.LastOrDefault(s => s.Kind is StepKind.Text or StepKind.Verdict)?.Text, run.FallbackReason,
                steps.Where(s => s is { Kind: StepKind.Approval, State: ApprovalState.Pending }).Select(s => new PendingApproval(s.Id, s.Tool, s.Summary)).ToList(),
                run);
        }
    }
    public record AgentInfo(string Name, string Label, string Description, string Kind, string[] Suggestions, string[] Tools);

    public static void MapAgentApi(this WebApplication app)
    {
        var api = app.MapApiGroup("Agents");

        api.MapGet("/agents", (IEnumerable<AgentDefinition> agents, IServiceProvider services, CurrentUser user) =>
        {
            var ctx = new ToolContext(SiteConfig.Users[0], [], services);
            return agents.Select(a => new AgentInfo(
                a.Name, a.Label, a.Description, a switch { PanelAgent => "panel", ScenarioAgent => "scenario", _ => "tools" }, a.Suggestions,
                a is ToolAgent t ? t.Tools(ctx).Select(x => x.Name).ToArray() : []));
        }).WithSummary("List the registered agents and their tools");

        api.MapPost("/agents/{name}", RunAgentAsync)
            .WithSummary("Run an agent; streams NDJSON events (step, delta, text, reset, done). ?stream=false returns one JSON result");

        api.MapGet("/runs", (RunStore store, int? limit) => store.ListAsync(Math.Clamp(limit ?? 50, 1, 200)))
            .WithSummary("Recent agent runs (audit trail)");

        api.MapGet("/runs/{id}", async (string id, RunStore store) =>
            await store.GetAsync(id) is { } run ? Results.Ok(run) : Results.NotFound())
            .WithSummary("One run with all its steps");

        api.MapPost("/runs/{id}/approvals/{approvalId}", async (
            string id, string approvalId, DecisionRequest body, RunStore store, AgentRunner runner, IEnumerable<AgentDefinition> agents) =>
        {
            var run = await store.GetAsync(id);
            if (run is null) return Results.NotFound();
            var agent = agents.FirstOrDefault(a => a.Name == run.Agent);
            if (agent is null) return Results.NotFound();
            try
            {
                return Results.Ok(RunResult.From(await runner.DecideAsync(agent, id, approvalId, body.Approved)));
            }
            catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).WithSummary("Approve or reject an action proposed by an agent");
    }

    /// <summary>
    /// A /api route group: no antiforgery (API clients), CORS for Security:AllowedOrigins, and every call acts as a
    /// demo user: X-Api-Key → Security:ApiUser, otherwise the cookie user.
    /// </summary>
    public static RouteGroupBuilder MapApiGroup(this WebApplication app, string tag)
    {
        var api = app.MapGroup("/api").WithTags(tag).DisableAntiforgery().RequireCors(ApiCors.Policy);
        api.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var options = http.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value;
            var currentUser = http.RequestServices.GetRequiredService<CurrentUser>();
            if (AccessGate.IsValidApiKey(http, options))
                currentUser.Use(SiteConfig.Users.FirstOrDefault(u => u.Id == options.ApiUser) ?? SiteConfig.Users[0]);
            else
                currentUser.Use(http.User);
            return await next(context);
        });
        return api;
    }

    private static async Task<IResult> RunAgentAsync(
        string name, RunRequest request, HttpContext http, IEnumerable<AgentDefinition> agents, AgentRunner runner, bool stream = true)
    {
        var agent = agents.FirstOrDefault(a => a.Name == name);
        if (agent is null) return Results.NotFound(new { error = $"Unknown agent '{name}'" });
        if (request.Messages is not { Count: > 0 } || request.Messages[^1].Role != "user")
            return Results.BadRequest(new { error = "messages must be non-empty and end with a user message" });

        var history = request.Messages.Select(m => new ChatTurn(m.Role == "user" ? "user" : "assistant", m.Content)).ToList();
        List<AgentFile> files;
        try
        {
            files = (request.Files ?? []).Select(f => f.Base64 is { Length: > 0 }
                ? DocumentText.FromImage(Convert.FromBase64String(f.Base64), f.Name, f.MediaType)
                : new AgentFile(Guid.NewGuid().ToString("N")[..12], f.Name, f.Text ?? "", 1)).ToList();
        }
        catch (FormatException) { return Results.BadRequest(new { error = "files[].base64 is not valid base64" }); }
        var ct = http.RequestAborted;

        if (!stream)
        {
            var completed = await runner.RunAsync(agent, history, files, _ => Task.CompletedTask, ct, new AgentRun { ConversationId = request.ConversationId });
            return Results.Ok(RunResult.From(completed));
        }

        http.Response.ContentType = "application/x-ndjson; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var gate = new SemaphoreSlim(1, 1);
        var sent = new List<(string Id, string Text)>();

        async Task WriteAsync(object evt)
        {
            await http.Response.WriteAsync(JsonSerializer.Serialize(evt, RunStore.Json) + "\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }

        // Turns "the whole run changed" callbacks into small incremental events.
        async Task OnUpdate(AgentRun run)
        {
            await gate.WaitAsync(ct);
            try
            {
                var steps = run.Snapshot();
                if (steps.Count < sent.Count || steps.Take(sent.Count).Select(s => s.Id).Where((id, i) => id != sent[i].Id).Any())
                {
                    sent.Clear();
                    await WriteAsync(new { type = "reset", source = run.Source.ToString(), reason = run.FallbackReason });
                }
                for (var i = 0; i < sent.Count; i++)
                {
                    var (id, previous) = sent[i];
                    var current = steps[i].Text;
                    if (current == previous) continue;
                    await WriteAsync(current.StartsWith(previous, StringComparison.Ordinal)
                        ? new { type = "delta", stepId = id, text = current[previous.Length..] }
                        : (object)new { type = "text", stepId = id, text = current });
                    sent[i] = (id, current);
                }
                foreach (var step in steps.Skip(sent.Count))
                {
                    await WriteAsync(new { type = "step", step });
                    sent.Add((step.Id, step.Text));
                }
            }
            finally { gate.Release(); }
        }

        var run = new AgentRun { ConversationId = request.ConversationId };
        await WriteAsync(new { type = "run", runId = run.Id });
        await runner.RunAsync(agent, history, files, OnUpdate, ct, run);
        var result = RunResult.From(run);
        await WriteAsync(new { type = "done", result.Answer, result.PendingApprovals, run });
        return Results.Empty;
    }
}
