namespace HackathonStarter.Features.Deadlines;

/// <summary>
/// The business date of CRS code: real time plus an offset ("+85 days"), so a 90-day deadline can be shown in a 3-minute
/// video. Time keeps flowing (events stay ordered); only "now" moves. Offset 0 = real time.
/// A separate service on purpose: the app-wide TimeProvider stays the system clock, because auth cookies, data protection
/// keys, run retention and rate limits must not jump 85 days (docs/PLAN.md 9.1). CRS code injects ISimClock only;
/// tests/.../Crs/ClockTests.cs enforces it.
/// </summary>
public interface ISimClock
{
    DateTime UtcNow { get; }
    TimeSpan Offset { get; }
    /// <summary>Raised after every change of the offset (DeadlineWatcher re-checks deadlines at once, the UI refreshes).</summary>
    event Action? Changed;
    /// <summary>Moves the date forward. Callers check the admin role and write a clock.changed audit entry.</summary>
    void Advance(TimeSpan by);
    void Reset();
}

/// <summary>Singleton over the real <see cref="TimeProvider"/> (the only CRS type allowed to take one).</summary>
public sealed class SimClock(TimeProvider real) : ISimClock
{
    private long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));
    public DateTime UtcNow => (real.GetUtcNow() + Offset).UtcDateTime;
    public event Action? Changed;

    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(by), "The simulated clock only moves forward; use Reset.");
        Interlocked.Add(ref _offsetTicks, by.Ticks);
        Changed?.Invoke();
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _offsetTicks, 0);
        Changed?.Invoke();
    }
}
