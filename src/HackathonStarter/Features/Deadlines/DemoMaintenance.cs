namespace HackathonStarter.Features.Deadlines;

/// <summary>
/// Keeps DeadlineWatcher out of Settings → "Reset demo data" (DemoData.ResetAsync). The reset sets the demo clock back to
/// today, which wakes the watcher at once; on a slow CPU its tick could land between the deletes and the seed and expire a
/// seeded case without its reminder, a state a new database never has. While a pause is held the watcher skips its ticks;
/// when the last one ends, <see cref="Resumed"/> fires and the watcher ticks once, so the deadlines catch up as after a new
/// start. Pauses nest and may come from any thread. Singleton, registered by AddDeadlines. Owner: R2.
/// </summary>
public sealed class DemoMaintenance
{
    private int _pauses;

    /// <summary>True while a handle returned by <see cref="Pause"/> is not yet disposed.</summary>
    public bool IsPaused => Volatile.Read(ref _pauses) > 0;

    /// <summary>Raised when the last pause ends, on the thread that ends it: handlers only signal (the watcher wakes up).</summary>
    public event Action? Resumed;

    /// <summary><c>using var _ = maintenance.Pause();</c> The pause ends when the handle is disposed; a second dispose does nothing.</summary>
    public IDisposable Pause()
    {
        Interlocked.Increment(ref _pauses);
        return new Handle(this);
    }

    private void End()
    {
        if (Interlocked.Decrement(ref _pauses) == 0) Resumed?.Invoke();
    }

    private sealed class Handle(DemoMaintenance owner) : IDisposable
    {
        private int _ended;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 0) owner.End();
        }
    }
}
