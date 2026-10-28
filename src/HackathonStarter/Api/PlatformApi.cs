using System.Text;
using HackathonStarter.Features.Checks;
using HackathonStarter.Features.Exports;
using HackathonStarter.Agents;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Features.Targets;
using HackathonStarter.Services;

namespace HackathonStarter.Api;

/// <summary>
/// Checks, synthetic data and exports over HTTP: for CI pipelines (exported tests call /api/checks/evaluate),
/// other teams' tools and scripts. Same auth as the agent API (cookie or X-Api-Key).
///
///   curl -X POST http://localhost:5000/api/checks/evaluate -H "Content-Type: application/json" \
///        -d '{"text":"Your CNP is 1900515400013","checks":["pii-leak"]}'
/// </summary>
public static class PlatformApi
{
    public record EvaluateRequest(string Text, string? Prompt = null, Dictionary<string, string>? Facts = null, List<string>? Checks = null);
    public record CheckInfo(string Id, string Version, string Title, string Description);
    public record CreateExportRequest(string Format, List<string>? Checks = null);
    public record ExportDecision(bool Approved);
    public record TargetRequest(List<AgentApi.MessageDto> Messages);
    /// <summary>An export without its content (lists).</summary>
    public record ExportInfo(string Id, string RunId, string Agent, string Title, string Format, string FileName, string Checks,
                             string Status, string CreatedBy, DateTime CreatedAt, string? DecidedBy, DateTime? DecidedAt)
    {
        public static ExportInfo From(RunExport e) => new(e.Id, e.RunId, e.Agent, e.Title, e.Format, e.FileName, e.Checks,
            e.Status.ToString(), e.CreatedBy, e.CreatedAt, e.DecidedBy, e.DecidedAt);
    }

    public static void MapPlatformApi(this WebApplication app)
    {
        // ─── Checks ───────────────────────────────────────────────────────────────────────────────
        var checks = app.MapApiGroup("Checks");

        checks.MapGet("/checks", (CheckEngine engine) =>
            engine.All.Select(c => new CheckInfo(c.Id, c.Version, c.Title, c.Description)))
            .WithSummary("Deterministic checks available (verdicts computed by code, never by a model)");

        checks.MapPost("/checks/evaluate", (EvaluateRequest request, CheckEngine engine) =>
        {
            if (request.Text is null) return Results.BadRequest(new { error = "text is required" });
            try
            {
                return Results.Ok(engine.Evaluate(new CheckSubject(request.Text, request.Prompt, request.Facts), request.Checks));
            }
            catch (KeyNotFoundException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).WithSummary("Evaluate checks on a text (all checks if none listed). Same input → same verdict and fingerprint");

        // ─── Synthetic data ───────────────────────────────────────────────────────────────────────
        var synthetic = app.MapApiGroup("Synthetic data");

        synthetic.MapGet("/synthetic/people", (int? count, int? seed) =>
            new SyntheticData(seed ?? 42).People(Math.Clamp(count ?? 10, 1, 500)))
            .WithSummary("Fake people with valid CNP, IBAN, card (test prefix), phone and example.com email. Same seed → same data");

        synthetic.MapGet("/synthetic/companies", (int? count, int? seed) =>
            new SyntheticData(seed ?? 42).Companies(Math.Clamp(count ?? 10, 1, 500)))
            .WithSummary("Fake companies with valid CUI, trade register number and IBAN. Same seed → same data");

        // ─── External targets ───────────────────────────────────────────────────────────────────────
        var targets = app.MapApiGroup("Targets");

        targets.MapGet("/targets", (TargetClient client) => client.List())
            .WithSummary("External systems configured in Targets:<name> (no secrets)");

        targets.MapPost("/targets/{name}", async (string name, TargetRequest request, TargetClient client, HttpContext http) =>
        {
            if (request.Messages is not { Count: > 0 } || request.Messages[^1].Role != "user")
                return Results.BadRequest(new { error = "messages must be non-empty and end with a user message" });
            if (client.Find(name) is null) return Results.NotFound(new { error = $"Unknown target '{name}'" });
            var conversation = request.Messages.Select(m => new ChatTurn(m.Role == "user" ? "user" : "assistant", m.Content)).ToList();
            return Results.Ok(await client.SendAsync(name, conversation, http.RequestAborted));
        }).WithSummary("Send a conversation to a target; falls back to its recorded answer when it is down");

        targets.MapGet("/connections/check", async (ConnectionCheck check, HttpContext http) =>
            await Guarded(async () => Results.Ok(await check.RunAsync(http.RequestAborted))))
            .WithSummary("Send a one-line request to the main model, every profile and every target (admin/member)");

        // ─── Exports ──────────────────────────────────────────────────────────────────────────────
        var exports = app.MapApiGroup("Exports");

        exports.MapGet("/exports/formats", (IEnumerable<IRunExporter> exporters) =>
            exporters.Select(e => new { e.Format, e.Label, e.FileExtension }))
            .WithSummary("Export formats (test case JSON, xUnit test, Markdown report…)");

        exports.MapGet("/exports", async (RunExportService service, string? runId, ExportStatus? status, int? limit) =>
            (await service.ListAsync(runId, status, Math.Clamp(limit ?? 100, 1, 500))).Select(ExportInfo.From))
            .WithSummary("Exports, newest first. ?status=Approved lists what CI should run");

        exports.MapPost("/runs/{runId}/exports", async (string runId, CreateExportRequest request, RunExportService service) =>
            await Guarded(async () => Results.Ok(ExportInfo.From(await service.CreateAsync(runId, request.Format, request.Checks)))))
            .WithSummary("Export a run as a draft (needs approval before it can be downloaded)");

        exports.MapPost("/exports/{id}/decision", async (string id, ExportDecision body, RunExportService service) =>
            await Guarded(async () => Results.Ok(ExportInfo.From(await service.DecideAsync(id, body.Approved)))))
            .WithSummary("Approve or reject a draft export (admin/member)");

        exports.MapGet("/exports/{id}/download", async (string id, RunExportService service) =>
        {
            var export = await service.GetAsync(id);
            if (export is null) return Results.NotFound();
            if (export.Status != ExportStatus.Approved)
                return Results.Conflict(new { error = $"Only approved exports can be downloaded (this one is {export.Status})." });
            return Results.File(Encoding.UTF8.GetBytes(export.Content), export.ContentType + "; charset=utf-8", export.FileName);
        }).WithSummary("Download an approved export");
    }

    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    }
}
