using System.Net;
using HackathonStarter.Config;

namespace HackathonStarter.Services;

/// <summary>
/// Installable app (PWA): the manifest and the offline page are generated from <see cref="SiteConfig"/>,
/// so renaming the app or changing its color needs no other edit. Icons live in wwwroot/icons,
/// the service worker in wwwroot/service-worker.js. Turn it all off with SiteConfig.Pwa.Enabled = false.
/// </summary>
public static class PwaEndpoints
{
    public static void MapPwa(this WebApplication app)
    {
        if (!SiteConfig.Pwa.Enabled) return;

        app.MapGet("/manifest.webmanifest", () => Results.Json(new
        {
            name = SiteConfig.Name,
            short_name = SiteConfig.Pwa.ShortName,
            description = SiteConfig.Description,
            start_url = SiteConfig.Pwa.StartUrl,
            scope = "/",
            display = "standalone",
            theme_color = SiteConfig.PrimaryColor,
            background_color = SiteConfig.Pwa.BackgroundColor,
            icons = new object[]
            {
                new { src = "/icons/icon-192.png", sizes = "192x192", type = "image/png", purpose = "any" },
                new { src = "/icons/icon-512.png", sizes = "512x512", type = "image/png", purpose = "any" },
                new { src = "/icons/icon-maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" },
            },
        }, contentType: "application/manifest+json")).ExcludeFromDescription();

        // Shown by the service worker when the device is offline (Blazor Server needs a connection).
        app.MapGet("/offline.html", () => Results.Content($$"""
            <!DOCTYPE html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="theme-color" content="{{SiteConfig.PrimaryColor}}"><title>{{WebUtility.HtmlEncode(SiteConfig.Name)}} · offline</title>
            <style>
              body{font-family:system-ui,sans-serif;display:grid;place-items:center;min-height:100vh;margin:0;background:#f8fafc;color:#111827;text-align:center;padding:1rem}
              img{width:72px;height:72px} h1{font-size:1.25rem;margin:1rem 0 .25rem} p{color:#6b7280;margin:0 0 1.25rem}
              button{padding:.6rem 1.25rem;border:0;border-radius:8px;background:{{SiteConfig.PrimaryColor}};color:#fff;font-size:1rem}
            </style></head>
            <body><div>
              <img src="/icons/icon-192.png" alt="">
              <h1>You're offline</h1><p>{{WebUtility.HtmlEncode(SiteConfig.Name)}} needs an internet connection. Your conversations are saved.</p>
              <button onclick="location.reload()">Try again</button>
            </div><script>addEventListener('online', () => location.reload());</script></body></html>
            """, "text/html; charset=utf-8")).ExcludeFromDescription();
    }
}
