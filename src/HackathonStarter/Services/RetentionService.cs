using HackathonStarter.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Services;

/// <summary>
/// Deletes agent runs older than Privacy:RetentionDays, at startup and then every hour: data that is not kept
/// can't leak. The last result is shown on the Data and privacy page.
/// </summary>
public class RetentionService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptionsMonitor<PrivacyOptions> options,
    TimeProvider time,
    ILogger<RetentionService> logger) : BackgroundService
{
    public DateTime? LastRun { get; private set; }
    public int LastDeleted { get; private set; }

    /// <summary>Deletes the runs past the retention period now. Returns how many were deleted.</summary>
    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        var days = options.CurrentValue.RetentionDays;
        var deleted = 0;
        if (days > 0)
        {
            var cutoff = time.GetUtcNow().UtcDateTime.AddDays(-days);
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            deleted = await db.AgentRuns.Where(r => r.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
            if (deleted > 0) logger.LogInformation("Retention: deleted {Count} run(s) older than {Days} days", deleted, days);
        }
        LastRun = time.GetUtcNow().UtcDateTime;
        LastDeleted = deleted;
        return deleted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), time);
        do
        {
            try { await PurgeAsync(stoppingToken); }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Retention cleanup failed; will retry in an hour");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
