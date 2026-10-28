namespace HackathonStarter.Features.Deadlines;

/// <summary>
/// The demo clock: real time plus an offset ("+85 days"), so a 90-day deadline can be shown in a 3-minute video.
/// Time keeps flowing (events stay ordered); only "now" moves. Offset 0 = real time.
/// Inject <see cref="SimClock"/> in the CRS services (cases, deadlines, changes, audit). The app-wide
/// <see cref="TimeProvider"/> stays the system clock on purpose: auth cookies, data protection keys and run retention
/// must not jump 85 days ahead.
/// </summary>
public sealed class SimClock(TimeProvider real) : TimeProvider
{
    private long _offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    /// <summary>Raised after every change of the offset (DeadlineWatcher re-checks deadlines at once, the UI refreshes).</summary>
    public event Action? Changed;

    public override DateTimeOffset GetUtcNow() => real.GetUtcNow() + Offset;
    public override TimeZoneInfo LocalTimeZone => real.LocalTimeZone;
    // Timers and stopwatches measure real elapsed time; only the date moves.
    public override long GetTimestamp() => real.GetTimestamp();
    public override long TimestampFrequency => real.TimestampFrequency;

    public DateTime UtcNow => GetUtcNow().UtcDateTime;

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
