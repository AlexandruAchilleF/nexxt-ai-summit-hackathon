using System.Globalization;
using System.Text.RegularExpressions;
using HackathonStarter.Config;

namespace HackathonStarter.Services;

/// <summary>
/// Two UI languages, Romanian (default) and English, without resource files.
/// UI text: write the English in markup as <c>@T("Save")</c> and add the Romanian to <c>Lang.Ro.cs</c>
/// (a test fails if a translation is missing). Content in SiteConfig: <c>Lang.Pick(ro: "...", en: "...")</c>.
/// The choice is a cookie set by <c>/lang/{code}</c>; it applies to the request and to the Blazor circuit it starts.
/// </summary>
public static partial class Lang
{
    public const string Cookie = "app_lang";
    public static readonly string[] Supported = ["ro", "en"];

    private static readonly CultureInfo Ro = CultureInfo.GetCultureInfo("ro-RO");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>"ro" or "en", from the current UI culture.</summary>
    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "ro";
    public static bool IsEn => Current == "en";

    /// <summary>The UI text in the current language. <paramref name="en"/> is the key; unknown keys are shown as they are.</summary>
    public static string T(string en) => IsEn ? en : RoText.GetValueOrDefault(en, en);

    /// <summary><see cref="T(string)"/> with <see cref="string.Format(string, object?[])"/> placeholders: <c>T("Approved by {0}", name)</c>.</summary>
    public static string T(string en, params object?[] args) => string.Format(T(en), args);

    /// <summary>
    /// Saved English text that was filled in from a key with placeholders, such as a check reason
    /// ("All 2 quotation(s) are verified evidence." from "All {0} quotation(s) are verified evidence."):
    /// translated with the same values. Text that matches no key is shown as it is.
    /// </summary>
    public static string TFilled(string text)
    {
        if (IsEn || RoText.ContainsKey(text)) return T(text);
        foreach (var (pattern, slots, ro) in Patterns.Value)
        {
            var match = pattern.Match(text);
            if (match.Success) return string.Format(ro, [.. Enumerable.Range(0, slots).Select(i => (object?)match.Groups["p" + i].Value)]);
        }
        return text;
    }

    // Keys with {0}… and enough fixed text to be told apart, the most specific first.
    private static readonly Lazy<List<(Regex Pattern, int Slots, string Ro)>> Patterns = new(() =>
        [.. RoText
            .Where(e => e.Key.Contains("{0}") && !e.Key.Contains("{{") && Regex.Replace(e.Key, @"\{\d+\}", "").Trim().Length >= 12)
            .OrderByDescending(e => Regex.Replace(e.Key, @"\{\d+\}", "").Length)
            .Select(e =>
            {
                var seen = new HashSet<string>();
                var regex = Regex.Replace(Regex.Escape(e.Key), @"\\\{(\d+)}", m =>
                    seen.Add(m.Groups[1].Value) ? $"(?<p{m.Groups[1].Value}>.+?)" : $@"\k<p{m.Groups[1].Value}>");
                return (new Regex($"^{regex}$", RegexOptions.Singleline),
                    seen.Select(int.Parse).Max() + 1, e.Value);
            })]);

    /// <summary>Content written in both languages (SiteConfig copy, longer texts).</summary>
    public static TValue Pick<TValue>(TValue ro, TValue en) => IsEn ? en : ro;

    /// <summary>Every English key that has a Romanian translation (used by tests).</summary>
    public static IReadOnlyDictionary<string, string> Translations => RoText;

    public static void UseLang(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var code = context.Request.Cookies[Cookie];
            var culture = code == "en" || (code is null && SiteConfig.DefaultLanguage == "en") ? En : Ro;
            CultureInfo.CurrentUICulture = culture;
            await next();
        });

        // GET /lang/en?returnUrl=/dashboard → remembers the choice for a year and goes back (reloads the circuit).
        app.MapGet("/lang/{code}", (HttpContext context, string code, string? returnUrl) =>
        {
            if (Supported.Contains(code))
            {
                context.Response.Cookies.Append(Cookie, code, new CookieOptions
                {
                    MaxAge = TimeSpan.FromDays(365), HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true,
                    Secure = context.Request.IsHttps,
                });
            }
            return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//") ? "/" : returnUrl);
        });
    }
}
