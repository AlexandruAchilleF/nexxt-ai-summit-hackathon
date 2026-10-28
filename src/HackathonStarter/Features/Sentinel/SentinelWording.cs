using System.Globalization;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// The fixed phrases of CRS Sentinel, written by code so the tools and the mock always say the same thing: "format valid"
/// (never "TIN valid"), "semnal, fără alertă", and the deadline with its rule and guide page. Language from Services.Lang.
/// </summary>
public static class SentinelWording
{
    private static readonly TimeZoneInfo Bucharest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>A case without DueAt (FATCA, p. 38): the guide gives no deadline and we do not borrow the CRS 90 days.</summary>
    public static string NoDeadline => Lang.Pick("fără termen legal în ghid (p. 38)", "no legal deadline in the guide (p. 38)");

    public static string SignalLabel => Lang.Pick("semnal, fără alertă", "signal, no alert");

    /// <summary>The date as the bank's operators read it (Bucharest), in the current UI language.</summary>
    public static string Date(DateTime utc) => Lang.Pick(DateRo(utc), Local(utc).ToString("d MMM yyyy", English));

    /// <summary>Romanian date for the letter to the client, whatever the UI language.</summary>
    public static string DateRo(DateTime utc) => Local(utc).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>"26.01.2027 (DL-NEW-90, p. 20-21, 35-36)", or the no-deadline text.</summary>
    public static string Deadline(Case c) =>
        c.DueAt is { } due ? $"{Date(due)} ({c.DeadlineRuleId}, p. {c.DeadlineGuidePage})" : NoDeadline;

    /// <summary>"format valid", "format invalid" or, when no rule checked it, "format neverificat". Never "TIN valid".</summary>
    public static string TinFormat(TinCheck check) => check.Source switch
    {
        TinCheckSource.NotChecked => Lang.Pick("format neverificat", "format not checked"),
        _ => check.FormatValid ? "format valid" : "format invalid",
    };

    /// <summary>"Germania" / "Germany" for "DE"; unknown codes stay as they are.</summary>
    public static string Country(string code, bool? en = null)
    {
        var display = Jurisdictions.Display(code, en ?? Lang.IsEn);
        var cut = display.LastIndexOf(" (", StringComparison.Ordinal);
        return cut > 0 ? display[..cut] : display;
    }

    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc), // SQLite gives Unspecified back; we always store UTC
    };

    public static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;

    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), Bucharest);
}
