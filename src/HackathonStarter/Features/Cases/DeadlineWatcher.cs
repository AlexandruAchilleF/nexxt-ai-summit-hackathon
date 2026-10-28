using HackathonStarter.Features.Deadlines;

namespace HackathonStarter.Features.Cases;

/// <summary>
/// Moves cases on the simulated date (docs/PLAN.md 9.4): every 30 seconds and at once when the demo clock changes, it runs
/// <see cref="ICaseService.TickAsync"/> in a fresh scope. A failed tick is logged and retried on the next one; it never
/// stops the host. Time comes from <see cref="ISimClock"/> only. Owner: R2.
/// </summary>
public sealed class DeadlineWatcher(IServiceScopeFactory scopes, ISimClock clock, ILogger<DeadlineWatcher> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    // At most one pending wake-up: several clock changes during a tick give one more tick, not a queue.
    private readonly SemaphoreSlim _wake = new(0, 1);

    /// <summary>One pass at the simulated now. Null when it failed (already logged).</summary>
    public async Task<DeadlineTick?> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var cases = scope.ServiceProvider.GetRequiredService<ICaseService>();
            var tick = await cases.TickAsync(clock.UtcNow);
            if (tick.Reminded + tick.Escalated + tick.Expired > 0)
                logger.LogInformation("Deadline tick: {Reminded} reminded, {Escalated} escalated, {Expired} expired", tick.Reminded, tick.Escalated, tick.Expired);
            else
                logger.LogDebug("Deadline tick: nothing due");
            return tick;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deadline tick failed; the next tick retries");
            return null;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        clock.Changed += Wake;
        try
        {
            await Task.Yield(); // never block the host's start
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                try { await _wake.WaitAsync(Interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            clock.Changed -= Wake;
        }
    }

    private void Wake()
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { /* a wake-up is already pending */ }
        catch (ObjectDisposedException) { /* the host is stopping */ }
    }

    public override void Dispose()
    {
        clock.Changed -= Wake;
        _wake.Dispose();
        base.Dispose();
    }
}
