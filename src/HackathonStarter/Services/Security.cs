using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Services;

/// <summary>
/// Protection for a PUBLIC deployment (section "Security"). Everything is off by default, so local
/// development is unchanged. Set them as environment variables on the host (Security__AccessCode=...).
/// </summary>
public class SecurityOptions
{
    /// <summary>If set, visitors must enter this code once (give it to the jury with the link).</summary>
    public string? AccessCode { get; set; }
    /// <summary>If set, API calls can authenticate with the header X-Api-Key instead of the access code.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Demo user that API-key calls act as.</summary>
    public string ApiUser { get; set; } = "member";
    /// <summary>
    /// Other websites allowed to call /api from the browser (CORS), e.g. "https://my-app.lovable.app,http://localhost:5173".
    /// Empty = only this app. "*" = any origin (fine for a hackathon demo, never with real data).
    /// </summary>
    public string? AllowedOrigins { get; set; }
    /// <summary>Live model calls per user per minute. Above it: recorded run / mock (no error).</summary>
    public int AgentRunsPerMinute { get; set; } = 10;
    /// <summary>Live model calls per day for the whole app, so a public link can't burn all your credits.</summary>
    public int DailyLiveRunBudget { get; set; } = 300;
}

public static class ApiCors
{
    public const string Policy = "api";

    /// <summary>CORS for /api, only when Security:AllowedOrigins is set.</summary>
    public static void AddApiCors(this WebApplicationBuilder builder)
    {
        var origins = (builder.Configuration["Security:AllowedOrigins"] ?? "")
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        builder.Services.AddCors(o => o.AddPolicy(Policy, p =>
        {
            if (origins.Contains("*")) p.AllowAnyOrigin();
            else p.WithOrigins(origins);
            p.AllowAnyHeader().AllowAnyMethod();
        }));
    }
}

/// <summary>Limits LIVE model calls (per user per minute + a daily budget). Agent runs over the limit fall back.</summary>
public class AgentRateLimiter(IOptions<SecurityOptions> options, TimeProvider time)
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _perUser = new();
    private readonly Lock _lock = new();
    private DateOnly _day;
    private int _dailyCount;

    public bool TryAcquire(string userId, out string? reason)
    {
        var o = options.Value;
        var now = time.GetUtcNow();
        lock (_lock)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (today != _day) { _day = today; _dailyCount = 0; }
            if (o.DailyLiveRunBudget > 0 && _dailyCount >= o.DailyLiveRunBudget)
            {
                reason = $"daily AI budget reached ({o.DailyLiveRunBudget} live runs)";
                return false;
            }

            var queue = _perUser.TryGetValue(userId, out var q) ? q : _perUser[userId] = new Queue<DateTimeOffset>();
            while (queue.Count > 0 && now - queue.Peek() > TimeSpan.FromMinutes(1)) queue.Dequeue();
            if (o.AgentRunsPerMinute > 0 && queue.Count >= o.AgentRunsPerMinute)
            {
                reason = $"rate limit reached ({o.AgentRunsPerMinute} live runs per minute)";
                return false;
            }

            queue.Enqueue(now);
            _dailyCount++;
            reason = null;
            return true;
        }
    }
}

/// <summary>
/// Optional access code for public deployments: GET/POST /access, a cookie, and a middleware that
/// sends everyone else to /access. API calls may use X-Api-Key instead.
/// </summary>
public static class AccessGate
{
    private const string CookieName = "app_access";

    public static void UseAccessGate(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var o = context.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value;
            if (string.IsNullOrEmpty(o.AccessCode) || IsExempt(context.Request.Path) || HasAccess(context, o))
            {
                await next();
                return;
            }

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Access code or X-Api-Key required" });
                return;
            }
            context.Response.Redirect("/access?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        });

        app.MapGet("/access", (string? returnUrl, string? error) =>
            Results.Content(Page(returnUrl ?? "/", error is not null), "text/html; charset=utf-8"));

        app.MapPost("/access", async (HttpContext context, IOptions<SecurityOptions> options) =>
        {
            var form = await context.Request.ReadFormAsync();
            var returnUrl = form["returnUrl"].ToString();
            if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/')) returnUrl = "/";
            var code = options.Value.AccessCode;

            if (string.IsNullOrEmpty(code) || form["code"].ToString().Trim() != code)
                return Results.Redirect("/access?error=1&returnUrl=" + Uri.EscapeDataString(returnUrl));

            context.Response.Cookies.Append(CookieName, Hash(code), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps, MaxAge = TimeSpan.FromDays(30),
            });
            return Results.LocalRedirect(returnUrl);
        }).DisableAntiforgery();
    }

    public static bool HasAccess(HttpContext context, SecurityOptions o) =>
        (context.Request.Cookies.TryGetValue(CookieName, out var cookie) && cookie == Hash(o.AccessCode!))
        || IsValidApiKey(context, o);

    public static bool IsValidApiKey(HttpContext context, SecurityOptions o) =>
        !string.IsNullOrEmpty(o.ApiKey) && context.Request.Headers.TryGetValue("X-Api-Key", out var key) && key == o.ApiKey;

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/access") || path.StartsWithSegments("/health")
        || path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content")
        || Path.HasExtension(path.Value) && !path.StartsWithSegments("/api");

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("access:" + code)));

    private static string Page(string returnUrl, bool error) => $$"""
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Access</title>
        <style>
          body{font-family:system-ui,sans-serif;display:grid;place-items:center;min-height:100vh;margin:0;background:#f8fafc;color:#111827}
          form{background:#fff;padding:2rem;border-radius:12px;box-shadow:0 1px 3px rgba(0,0,0,.1);width:min(360px,90vw)}
          h1{font-size:1.25rem;margin:0 0 .25rem} p{color:#6b7280;margin:0 0 1rem;font-size:.9rem}
          input{width:100%;box-sizing:border-box;padding:.6rem .75rem;border:1px solid #d1d5db;border-radius:8px;font-size:1rem}
          button{margin-top:.75rem;width:100%;padding:.6rem;border:0;border-radius:8px;background:{{Config.SiteConfig.PrimaryColor}};color:#fff;font-size:1rem;cursor:pointer}
          .err{color:#dc2626;font-size:.85rem;margin-top:.5rem}
        </style></head>
        <body><form method="post" action="/access">
          <h1>{{System.Net.WebUtility.HtmlEncode(Config.SiteConfig.Name)}}</h1><p>Enter the access code to continue.</p>
          <input name="code" type="password" placeholder="Access code" autofocus required>
          <input type="hidden" name="returnUrl" value="{{System.Net.WebUtility.HtmlEncode(returnUrl)}}">
          <button type="submit">Continue</button>
          {{(error ? "<div class=\"err\">Wrong code, try again.</div>" : "")}}
        </form></body></html>
        """;
}
