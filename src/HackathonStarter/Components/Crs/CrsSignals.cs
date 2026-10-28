using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Deadlines;

namespace HackathonStarter.Components.Crs;

/// <summary>
/// Tells every open CRS page that the data may have moved: the demo date changed and the deadline tick ran. The demo date
/// is one for the whole app (ISimClock is a singleton), so one app-wide event is the right scope.
/// </summary>
public static class CrsSignals
{
    public static event Func<Task>? Changed;

    public static async Task RaiseAsync()
    {
        if (Changed is not { } handlers) return;
        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            try { await handler(); }
            catch (ObjectDisposedException) { /* a page closed meanwhile */ }
        }
    }

    /// <summary>
    /// Moves the demo date through <see cref="SimClockControl"/> (operator role, audit entry), then runs the deadline tick
    /// at once so reminders and expiries show without waiting for the watcher (TickAsync is serialized, so the watcher's
    /// own pass finds nothing left to do), then refreshes the pages.
    /// </summary>
    public static async Task<DeadlineTick> MoveClockAsync(Func<Task<TimeSpan>> move, ICaseService cases, ISimClock clock)
    {
        await move();
        var tick = await cases.TickAsync(clock.UtcNow);
        await RaiseAsync();
        return tick;
    }
}
