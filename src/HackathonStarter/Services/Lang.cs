using System.Globalization;
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
