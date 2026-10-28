using HackathonStarter.Config;
using HackathonStarter.Features.Audit;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Deadlines;

/// <summary>
/// The only way to move the demo date (docs/PLAN.md 9.1): operator role, one <c>clock.changed</c> audit entry per move.
/// The entry is written before the clock moves, so the watcher's reminders and expiries follow it in the log, and a failed
/// audit leaves the clock where it was. Scoped (it checks the current user). Owner: R2.
/// </summary>
public sealed class SimClockControl(ISimClock clock, CurrentUser user, IAuditLog audit)
{
    public const int MaxDays = 400;

    public TimeSpan Offset => clock.Offset;
    public DateTime UtcNow => clock.UtcNow;

    /// <returns>The new offset.</returns>
    public async Task<TimeSpan> AdvanceDaysAsync(int days)
    {
        var who = await user.Require(Roles.Operator);
        if (days is < 1 or > MaxDays)
            throw new ArgumentOutOfRangeException(nameof(days), days, $"The demo date moves forward by 1 to {MaxDays} days.");
        var before = clock.Offset;
        var after = before + TimeSpan.FromDays(days);
        await audit.AppendAsync("user:" + who.Id, AuditKinds.ClockChanged, Payload("advance", days, before, after));
        clock.Advance(TimeSpan.FromDays(days));
        return clock.Offset;
    }

    /// <returns>The new offset (zero).</returns>
    public async Task<TimeSpan> ResetAsync()
    {
        var who = await user.Require(Roles.Operator);
        var before = clock.Offset;
        await audit.AppendAsync("user:" + who.Id, AuditKinds.ClockChanged, Payload("reset", null, before, TimeSpan.Zero));
        clock.Reset();
        return clock.Offset;
    }

    private static object Payload(string action, int? days, TimeSpan before, TimeSpan after) => new
    {
        action,
        days,
        offsetBefore = before.ToString("c"),
        offsetAfter = after.ToString("c"),
        offsetBeforeDays = before.TotalDays,
        offsetAfterDays = after.TotalDays,
    };
}
