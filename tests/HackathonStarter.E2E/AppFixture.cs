using System.Diagnostics;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>
/// Starts the real app (Development, fresh SQLite DB, no AI key → mock/replay agents) and a headless browser.
/// Shared by all tests in the collection. Browsers: `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    private Process? _app;
    private string _dataDir = "";
    public string BaseUrl { get; } = $"http://127.0.0.1:{Random.Shared.Next(5300, 5900)}";
    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;
    public HttpClient Http { get; } = new();

    public async Task InitializeAsync()
    {
        var root = FindRepoRoot();
        var projectDir = Path.Combine(root, "src", "HackathonStarter");
        var dll = new[] { "Debug", "Release" }
            .Select(c => Path.Combine(projectDir, "bin", c, "net10.0", "HackathonStarter.dll"))
            .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            ?? throw new InvalidOperationException("Build the app first (dotnet build).");

        _dataDir = Path.Combine(Path.GetTempPath(), "hs-e2e-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dataDir);

        var start = new ProcessStartInfo("dotnet", $"\"{dll}\" --urls {BaseUrl}")
        {
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ConnectionStrings__Default"] = $"Data Source={Path.Combine(_dataDir, "e2e.db")}";
        start.Environment["Database__RecreateOnStartup"] = "true";
        start.Environment["AI__Provider"] = "None";
        _app = Process.Start(start)!;
        _app.OutputDataReceived += (_, _) => { };
        _app.ErrorDataReceived += (_, _) => { };
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();

        for (var i = 0; ; i++)
        {
            try
            {
                if ((await Http.GetAsync($"{BaseUrl}/health")).IsSuccessStatusCode) break;
            }
            catch (HttpRequestException) when (i < 60) { }
            if (i >= 60) throw new TimeoutException("The app did not start in 30s");
            await Task.Delay(500);
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync();
    }

    /// <summary>
    /// A fresh browser context (own cookies) on a page, signed in as the given demo user. Tests assert English text,
    /// so the language cookie is "en" unless a test passes null (the app's default, Romanian).
    /// </summary>
    public async Task<IPage> NewPageAsync(string user = "admin", int width = 1360, int height = 900, string? lang = "en")
    {
        var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        if (lang is not null) await context.AddCookiesAsync([new Cookie { Name = "app_lang", Value = lang, Url = BaseUrl }]);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(20_000);
        await page.GotoAsync($"{BaseUrl}/switch-user/{user}?returnUrl=/dashboard");
        return page;
    }

    /// <summary>Navigates and waits until Blazor is interactive.</summary>
    public async Task GoAsync(IPage page, string path)
    {
        await page.GotoAsync(BaseUrl + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(500);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        if (_app is { HasExited: false }) { _app.Kill(entireProcessTree: true); await _app.WaitForExitAsync(); }
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* best effort */ }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}

[CollectionDefinition("app")]
public class AppCollection : ICollectionFixture<AppFixture>;
