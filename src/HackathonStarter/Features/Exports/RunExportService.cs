using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Checks;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Exports;

/// <summary>
/// An exported run (test case, report…). Created as a Draft; a person approves it before it counts:
/// only approved exports can be downloaded, so nothing becomes an "official" test without a human decision.
/// </summary>
public class RunExport
{
    public string Id { get; set; } = AgentStep.NewId();
    public string RunId { get; set; } = "";
    public string Agent { get; set; } = "";
    /// <summary>The run's request (masked), for lists.</summary>
    public string Title { get; set; } = "";
    public string Format { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "text/plain";
    public string Content { get; set; } = "";
    /// <summary>Comma-separated check ids the test asserts.</summary>
    public string Checks { get; set; } = "";
    public ExportStatus Status { get; set; } = ExportStatus.Draft;
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public enum ExportStatus { Draft, Approved, Rejected }

public class RunExportService(
    IDbContextFactory<AppDbContext> dbFactory,
    RunStore runs,
    IEnumerable<IRunExporter> exporters,
    CheckEngine checks,
    CurrentUser currentUser)
{
    public IReadOnlyList<IRunExporter> Exporters { get; } = [.. exporters];

    /// <summary>
    /// Creates a draft export of a saved run. <paramref name="checkIds"/>: the checks the test must pass;
    /// null = the checks recorded on the run (none = the test only asserts that the agent answers).
    /// </summary>
    public async Task<RunExport> CreateAsync(string runId, string format, IReadOnlyList<string>? checkIds = null)
    {
        var user = await currentUser.Require(Roles.Admin, Roles.Member);
        var exporter = Exporters.FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
                       ?? throw new ArgumentException($"Unknown export format '{format}'. Available: {string.Join(", ", Exporters.Select(e => e.Format))}");
        var run = await runs.GetAsync(runId) ?? throw new KeyNotFoundException("Run not found");
        if (run.Status is RunStatus.Running) throw new InvalidOperationException("The run is still in progress.");

        var ids = checkIds ?? run.Steps.LastOrDefault(s => s.Kind == StepKind.Check)?.Checks?.Select(c => c.CheckId).ToList() ?? [];
        var unknown = ids.Where(id => checks.Find(id) is null).ToList();
        if (unknown.Count > 0) throw new ArgumentException($"Unknown check(s): {string.Join(", ", unknown)}");

        // Exporters work on the SAVED run, which is already masked (RunStore), so exports hold no personal data.
        var content = exporter.Export(new RunExportContext(run, ids, SiteConfig.Name));
        var export = new RunExport
        {
            RunId = run.Id,
            Agent = run.Agent,
            Title = run.Input.Length > 200 ? run.Input[..200] + "…" : run.Input,
            Format = exporter.Format,
            FileName = $"{ExportText.Identifier(run.Agent, 24)}-{run.Id}.{exporter.FileExtension}",
            ContentType = exporter.ContentType,
            Content = content,
            Checks = string.Join(',', ids),
            CreatedBy = user.Name,
        };
        await using var db = await dbFactory.CreateDbContextAsync();
        db.RunExports.Add(export);
        await db.SaveChangesAsync();
        return export;
    }

    public async Task<List<RunExport>> ListAsync(string? runId = null, ExportStatus? status = null, int limit = 100)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.RunExports.AsNoTracking().AsQueryable();
        if (runId is not null) query = query.Where(e => e.RunId == runId);
        if (status is { } s) query = query.Where(e => e.Status == s);
        return await query.OrderByDescending(e => e.CreatedAt).Take(limit).ToListAsync();
    }

    public async Task<RunExport?> GetAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.RunExports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>The human decision: approve (the export becomes downloadable) or reject.</summary>
    public async Task<RunExport> DecideAsync(string id, bool approved)
    {
        var user = await currentUser.Require(Roles.Admin, Roles.Member);
        await using var db = await dbFactory.CreateDbContextAsync();
        var export = await db.RunExports.FirstOrDefaultAsync(e => e.Id == id) ?? throw new KeyNotFoundException("Export not found");
        if (export.Status != ExportStatus.Draft) throw new InvalidOperationException($"Already {export.Status}");
        export.Status = approved ? ExportStatus.Approved : ExportStatus.Rejected;
        export.DecidedBy = user.Name;
        export.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return export;
    }
}
