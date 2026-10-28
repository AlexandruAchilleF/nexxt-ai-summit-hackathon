using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Deadlines;

/// <param name="KnownAt">When the bank was notified of / discovered the change (guide p. 29, 35-36), not when it happened.</param>
/// <param name="IsPreexisting">CRS: account held on 31 Dec 2015 (guide p. 20). Changes what must be obtained and the consequence.</param>
public record DeadlineInput(DateTime KnownAt, Regime Regime, bool IsPreexisting);

/// <param name="DesignChoice">True when part of the result is our design, not the guide (reminder day, FATCA deadline).</param>
public record DeadlineResult(DateTime DueAt, DateTime RemindAt, string RuleId, string GuidePage, string Explanation, string ExplanationRo, bool DesignChoice);

/// <summary>Deadlines as pure code: no clock, no database. Owner: R2.</summary>
public interface IDeadlineCalculator
{
    DeadlineResult Compute(DeadlineInput input);
}

/// <summary>Phase 0 version: 90 calendar days, reminder 5 days before. R2 refines it per regime and account age.</summary>
public sealed class DeadlineCalculator : IDeadlineCalculator
{
    public const int Days = 90;
    public const int ReminderDaysBefore = 5;

    public DeadlineResult Compute(DeadlineInput input)
    {
        var due = input.KnownAt.AddDays(Days);
        return new DeadlineResult(due, due.AddDays(-ReminderDaysBefore), "DL-CHANGE-90", "29, 35-36",
            "90 calendar days from the date the bank knew of the change.",
            "90 de zile calendaristice de la data la care banca a aflat de schimbare.", DesignChoice: true);
    }
}

public static class DeadlinesSetup
{
    /// <summary>Registers <see cref="SimClock"/> on top of the app's <see cref="TimeProvider"/> (the system clock).</summary>
    public static IServiceCollection AddDeadlines(this IServiceCollection services)
    {
        services.AddSingleton(sp => new SimClock(sp.GetService<TimeProvider>() ?? TimeProvider.System));
        services.AddSingleton<IDeadlineCalculator, DeadlineCalculator>();
        return services;
    }
}
