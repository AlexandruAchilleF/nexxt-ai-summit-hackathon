using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Deadlines;

/// <param name="KnownAt">When the bank was notified of / discovered the change (ChangeEvent.ReceivedAt), not when it
/// happened: the date the self-certification loses its validity (guide p. 29, 35-36).</param>
/// <param name="AccountOpenedOn">Preexisting or new is computed per regime from this date (<see cref="Preexisting.For"/>, p. 20).</param>
public record DeadlineInput(DateTime KnownAt, Regime Regime, DateOnly AccountOpenedOn);

/// <param name="DueAt">Null when the guide gives no deadline (FATCA change of circumstances, p. 38, docs/GOLDEN-CASES.md I1):
/// the case shows "no deadline in the guide", NEVERIFICAT; we do not borrow the CRS 90 days.</param>
/// <param name="DesignChoice">True when part of the result is our design, not the guide (reminder day, "the later date").</param>
public record DeadlineResult(DateTime? DueAt, DateTime? RemindAt, bool Preexisting, string RuleId, string GuidePage,
                             string Explanation, string ExplanationRo, bool DesignChoice);

/// <summary>Deadlines as pure code: no clock, no database (docs/PLAN.md 9.2). Owner: R2.</summary>
public interface IDeadlineCalculator
{
    DeadlineResult Compute(DeadlineInput input);
}

/// <summary>
/// Phase 0 version of the rules decided in PLAN 9.2: new CRS account (DL-NEW-90) = KnownAt + 90 x 24 h; preexisting CRS
/// account (DL-PRE-YEAR) = the later of 31 Dec 23:59:59 Bucharest time and KnownAt + 90 days; FATCA = no deadline.
/// Reminder 5 days before. R2 owns the tests and the refinements.
/// </summary>
public sealed class DeadlineCalculator : IDeadlineCalculator
{
    public const int Days = 90;
    public const int ReminderDaysBefore = 5;
    // Needs tzdata: present in mcr.microsoft.com/dotnet/aspnet:10.0 (our Dockerfile); chiseled/Alpine images lack it.
    private static readonly TimeZoneInfo Bucharest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    public DeadlineResult Compute(DeadlineInput input)
    {
        var known = input.KnownAt.Kind == DateTimeKind.Local ? input.KnownAt.ToUniversalTime() : DateTime.SpecifyKind(input.KnownAt, DateTimeKind.Utc);
        var preexisting = Preexisting.For(input.Regime == Regime.Fatca ? Regime.Fatca : Regime.Crs, input.AccountOpenedOn);

        if (input.Regime == Regime.Fatca)
            return new(null, null, preexisting, "FATCA-SC", "38",
                "FATCA: a new self-certification is required; the guide gives no deadline (NEVERIFICAT).",
                "FATCA: e necesară o autocertificare nouă; ghidul nu dă un termen (NEVERIFICAT).", DesignChoice: false);

        var plus90 = known.AddDays(Days);
        if (!preexisting)
            return new(plus90, plus90.AddDays(-ReminderDaysBefore), false, "DL-NEW-90", "20-21, 35-36",
                "New account: 90 calendar days from the date the bank knew of the change.",
                "Cont nou: 90 de zile calendaristice de la data la care banca a aflat de schimbare.", DesignChoice: true);

        var yearEndLocal = new DateTime(TimeZoneInfo.ConvertTimeFromUtc(known, Bucharest).Year, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
        var yearEnd = TimeZoneInfo.ConvertTimeToUtc(yearEndLocal, Bucharest);
        var due = yearEnd > plus90 ? yearEnd : plus90;
        return new(due, due.AddDays(-ReminderDaysBefore), true, "DL-PRE-YEAR", "29",
            "Preexisting account, electronic record search: year end or 90 days, whichever is later, is our interpretation (Fiscal Procedure Code, Annex 1, Section III, subsection B, point 16 says \"or\"), aligned with OECD Commentary on Section III, paragraph 16. For the residence address test, the later date is required by law (Annex 2, point 1; Directive 2011/16/EU, Annex II, point 1; OECD Commentary on Section III, paragraph 13).",
            "Cont preexistent, indicii din căutarea electronică: sfârșitul anului sau 90 de zile, data mai târzie, este interpretarea noastră (Codul de procedură fiscală, Anexa nr. 1, secțiunea III, subsecțiunea B, pct. 16 spune „ori”), aliniată cu Comentariul OCDE la Secțiunea III, pct. 16. Pentru testul adresei de rezidență, data mai târzie este prevăzută de lege (Anexa nr. 2, pct. 1; Directiva 2011/16/UE, Anexa II, pct. 1; Comentariul OCDE la Secțiunea III, pct. 13).", DesignChoice: true);
    }
}

public static class DeadlinesSetup
{
    /// <summary>Registers <see cref="ISimClock"/> on top of the app's <see cref="TimeProvider"/> (the system clock, unchanged),
    /// and the one <see cref="DemoMaintenance"/> that the demo reset pauses and DeadlineWatcher obeys.</summary>
    public static IServiceCollection AddDeadlines(this IServiceCollection services)
    {
        services.AddSingleton<ISimClock>(sp => new SimClock(sp.GetService<TimeProvider>() ?? TimeProvider.System));
        services.AddSingleton<DemoMaintenance>();
        services.AddSingleton<IDeadlineCalculator, DeadlineCalculator>();
        services.AddScoped<SimClockControl>();
        return services;
    }
}
