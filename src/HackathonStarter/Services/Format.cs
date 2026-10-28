using System.Globalization;
using HackathonStarter.Config;

namespace HackathonStarter.Services;

/// <summary>Formatting helpers using <see cref="SiteConfig.Culture"/> and <see cref="SiteConfig.Currency"/>.</summary>
public static class Format
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo(SiteConfig.Culture);

    /// <param name="compact">Short form for dashboards: "6.050 EUR", "125k EUR".</param>
    public static string Money(decimal value, bool compact = false) =>
        compact
            ? Math.Abs(value) >= 100_000
                ? $"{(value / 1000m).ToString("0.#", Culture)}k {SiteConfig.Currency}"
                : $"{value.ToString("N0", Culture)} {SiteConfig.Currency}"
            : $"{value.ToString(Math.Abs(value) >= 10_000 ? "N0" : "N2", Culture)} {SiteConfig.Currency}";

    public static string Number(decimal value, int decimals = 0) => value.ToString("N" + decimals, Culture);

    public static string Percent(decimal value) => value.ToString("0.#", Culture) + "%";

    public static string Date(DateTime value) => value.ToLocalTime().ToString("d MMM yyyy", Culture);

    public static string DateTime(DateTime value) => value.ToLocalTime().ToString("d MMM yyyy, HH:mm", Culture);

    /// <summary>"3 days ago", "just now"…</summary>
    public static string Relative(DateTime value)
    {
        var diff = System.DateTime.UtcNow - value.ToUniversalTime();
        return diff.TotalSeconds switch
        {
            < 60 => Lang.T("just now"),
            < 3600 => Lang.T("{0} min ago", (int)diff.TotalMinutes),
            < 86400 => Lang.T("{0} h ago", (int)diff.TotalHours),
            < 172800 => Lang.T("yesterday"),
            _ => Lang.T("{0} days ago", (int)diff.TotalDays),
        };
    }
}
