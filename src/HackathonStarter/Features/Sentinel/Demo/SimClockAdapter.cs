using HackathonStarter.Features.Deadlines;

namespace HackathonStarter.Features.Sentinel.Demo;

/// <summary>Adapts R2's SimClockControl to the IDemoClock interface for the demo UI and scenarios (docs/adr/0001).</summary>
public sealed class SimClockAdapter(SimClockControl control) : IDemoClock
{
    public DateTime UtcNow => control.UtcNow;
    public TimeSpan Offset => control.Offset;

    public async Task AdvanceAsync(TimeSpan by)
    {
        var days = (int)Math.Round(by.TotalDays);
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(by));
        await control.AdvanceDaysAsync(days);
    }

    public async Task ResetAsync() => await control.ResetAsync();
}
