using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using HackathonStarter.Services;
using Microsoft.Playwright;

// Drives the video route of docs/PITCH.md (S1-S9) in a visible browser, at the pace of the voice-over, so the screen can be
// filmed in one take. The voice is recorded separately; the edit uses the chapter times printed at the end.
// Usage: dotnet run --project tools/DemoFilm -- [--url http://localhost:5000] [--code ACCESS] [--video DIR] [--fast]
//        [--from S5] [--to S7] [--lang ro|en] [--no-reset] [--headless] [--shots DIR]

var o = Options.Parse(args);
if (o is null) return 2;
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(o.Lang == "en" ? "en-GB" : "ro-RO"); // Lang.T follows it
return await new Film(o).RunAsync();

sealed record Options(string Url, string? Code, string Lang, string? VideoDir, bool Fast, string From, string To, bool Reset, bool Headless, string? ShotsDir)
{
    public static Options? Parse(string[] args)
    {
        string Value(string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        bool Flag(string name) => args.Contains(name);

        if (Flag("--help") || Flag("-h"))
        {
            Console.WriteLine("dotnet run --project tools/DemoFilm -- [--url URL] [--code ACCESS] [--video DIR] [--fast] [--from S#] [--to S#] [--lang ro|en] [--no-reset] [--headless] [--shots DIR]");
            return null;
        }
        var o = new Options(Value("--url", "http://localhost:5000").TrimEnd('/'),
            Value("--code", Environment.GetEnvironmentVariable("DEMO_ACCESS_CODE") ?? "") is { Length: > 0 } c ? c : null,
            Value("--lang", "ro"), Value("--video", "") is { Length: > 0 } v ? v : null, Flag("--fast"),
            Value("--from", "S1").ToUpperInvariant(), Value("--to", "S9").ToUpperInvariant(), !Flag("--no-reset"), Flag("--headless"),
            Value("--shots", "") is { Length: > 0 } d ? d : null);
        if (o.From is "S3" or "S4")
        {
            Console.Error.WriteLine("S3 and S4 continue Maria's case from S2: start at S2 (or at S5 or later).");
            return null;
        }
        return o;
    }
}

sealed class Film(Options o)
{
    // Viewport 1536x864 at scale 1.25 = 1920x1080 pixels, the browser zoom of 125% from the filming checklist.
    private const int Width = 1536, Height = 864;

    private IPage _page = default!;
    private readonly Stopwatch _clock = new();
    private readonly List<string> _chapters = [];
    private string? _caseCode;

    /// <param name="Before">Runs before the chapter starts (off the voice-over, cut in the edit).</param>
    private record Segment(string Id, string Title, double Seconds, Func<Task> Run, Func<Task>? Before = null);

    public async Task<int> RunAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        Console.WriteLine($"App: {o.Url} (a sleeping Render instance can take a minute)");
        try { (await http.GetAsync(o.Url + "/health")).EnsureSuccessStatusCode(); }
        catch (Exception ex) { Console.Error.WriteLine($"The app does not answer on {o.Url}/health: {ex.Message}"); return 1; }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = o.Headless });

        var state = await PrepareAsync(browser);
        if (state is null) return 1;

        var options = new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = Width, Height = Height }, DeviceScaleFactor = 1.25f, StorageState = state,
        };
        if (o.VideoDir is not null)
        {
            Directory.CreateDirectory(o.VideoDir);
            options.RecordVideoDir = o.VideoDir;
            options.RecordVideoSize = new() { Width = 1920, Height = 1080 };
        }
        var context = await browser.NewContextAsync(options);
        await context.AddInitScriptAsync(CursorScript);
        _page = await context.NewPageAsync();
        _page.SetDefaultTimeout(30_000);

        var segments = Segments().SkipWhile(s => s.Id != o.From).TakeWhile(s => string.CompareOrdinal(s.Id, o.To) <= 0).ToList();
        _clock.Start();
        var failed = false;
        foreach (var s in segments)
        {
            var start = _clock.Elapsed;
            try
            {
                if (s.Before is not null) { await s.Before(); start = _clock.Elapsed; }
                _chapters.Add($"{s.Id}  {start:m\\:ss}  {s.Title}");
                Console.WriteLine($"{start:m\\:ss}  {s.Id}  {s.Title}");
                await s.Run();
                if (o.ShotsDir is not null) // the end state of each segment, to check a take without watching it
                {
                    Directory.CreateDirectory(o.ShotsDir);
                    await _page.ScreenshotAsync(new() { Path = Path.Combine(o.ShotsDir, $"{s.Id}.png") });
                }
            }
            catch (Exception ex)
            {
                var shot = Path.Combine(o.VideoDir ?? ".", $"film-error-{s.Id}.png");
                await _page.ScreenshotAsync(new() { Path = shot });
                Console.Error.WriteLine($"{s.Id} failed: {ex.Message.Split('\n')[0]}\nScreenshot: {shot}. Reset the demo data and run again (from S2 for Maria).");
                failed = true;
                break;
            }
            // The voice-over of each segment has its own length: the screen waits for it.
            var left = TimeSpan.FromSeconds(s.Seconds) - (_clock.Elapsed - start);
            if (!o.Fast && left > TimeSpan.Zero) await _page.WaitForTimeoutAsync((float)left.TotalMilliseconds);
        }
        _chapters.Add($"END {_clock.Elapsed:m\\:ss}");

        var video = _page.Video;
        await context.CloseAsync();
        var chaptersFile = Path.Combine(o.VideoDir ?? o.ShotsDir ?? ".", "film-chapters.txt");
        await File.WriteAllLinesAsync(chaptersFile, _chapters);
        Console.WriteLine($"Chapters: {chaptersFile}");
        if (video is not null) Console.WriteLine($"Video: {await video.PathAsync()}");
        return failed ? 1 : 0;
    }

    /// <summary>Access code, demo user (Ana, admin), language, demo reset: done off camera, then handed to the filmed context.</summary>
    private async Task<string?> PrepareAsync(IBrowser browser)
    {
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = Width, Height = Height } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);

        var gate = await page.APIRequest.GetAsync(o.Url + "/", new() { MaxRedirects = 0 });
        if (gate.Headers.TryGetValue("location", out var location) && location.Contains("/access"))
        {
            if (o.Code is null) { Console.Error.WriteLine("The app asks for an access code: pass --code or set DEMO_ACCESS_CODE."); return null; }
            var form = page.APIRequest.CreateFormData();
            form.Set("code", o.Code);
            form.Set("returnUrl", "/");
            var answer = await page.APIRequest.PostAsync(o.Url + "/access", new() { Form = form, MaxRedirects = 0 });
            if (answer.Headers.TryGetValue("location", out var next) && next.Contains("/access"))
            {
                Console.Error.WriteLine("The access code was refused.");
                return null;
            }
        }
        await context.AddCookiesAsync([new Cookie { Name = Lang.Cookie, Value = o.Lang, Url = o.Url }]);
        await page.GotoAsync($"{o.Url}/switch-user/admin?returnUrl=/settings", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(800);

        if (o.Reset)
        {
            Console.WriteLine("Resetting the demo data (seed, audit log, simulated date at offset 0)");
            await page.GetByRole(AriaRole.Button, new() { Name = Lang.T("Reset"), Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = Lang.T("Reset"), Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
            await page.WaitForTimeoutAsync(2_000);
        }
        var state = await context.StorageStateAsync();
        await context.CloseAsync();
        return state;
    }

    // The windows of docs/PITCH.md section 1 (S1 0:00-0:32, S2 0:32-0:49, ...).
    private IEnumerable<Segment> Segments() =>
    [
        new("S1", "Problema: /dashboard", 32, async () =>
        {
            await GoAsync("/dashboard");
            await BeatAsync(4);
            await ScrollAsync(350);
            await BeatAsync(6);
            await ScrollAsync(-350);
        }),
        new("S2", "Maria: schimbarea si cazul", 17, async () =>
        {
            await GoAsync("/changes/new?scenario=4");
            await BeatAsync(2);
            await ClickAsync(_page.GetByRole(AriaRole.Button, new() { Name = Lang.T("Record and apply the rules") }));
            var verdict = _page.Locator(".crs-verdict");
            await Assertions.Expect(verdict.Locator(".crs-verdict-title")).ToBeVisibleAsync();
            await BeatAsync(4);
            var link = verdict.Locator("a[href^='/cases/CRS-']").First;
            _caseCode = (await link.GetAttributeAsync("href"))!.Split('/')[^1];
            Console.WriteLine($"      Maria's case: {_caseCode}");
            await ClickAsync(link);
            await Assertions.Expect(_page.Locator(".crs-clock")).ToBeVisibleAsync();
        }),
        new("S3", "Maria: dovezi, semnal, cererea aprobata", 16, async () =>
        {
            var panel = _page.Locator(".case-agent");
            await ClickAsync(panel.GetByTestId("agent-analyze"));
            var approve = panel.GetByRole(AriaRole.Button, new() { Name = Lang.T("Approve"), Exact = true });
            await Assertions.Expect(approve).ToBeVisibleAsync(new() { Timeout = 180_000 });
            await RefuseReplayAsync(panel);
            await BeatAsync(2);
            await ClickAsync(approve);
            await Assertions.Expect(_page.GetByText(Lang.T("Awaiting client")).First).ToBeVisibleAsync();
            await BeatAsync(1);
            await ScrollToAsync(CaseHeading()); // the status: awaiting the client
            await BeatAsync(2);
            await ScrollToAsync(_page.Locator(".crs-grid.two").First); // the indicia with the verified quote, and the signal
            await BeatAsync(2.5);
        }),
        new("S4", "Maria: ziua 85, raspunsul, inchiderea", 19, async () =>
        {
            var panel = _page.Locator(".case-agent");
            await ClickAsync(_page.Locator(".demo-date"));
            await ClickAsync(_page.GetByText(Lang.T("To the reminder day")));
            await Assertions.Expect(_page.GetByText(Lang.T("Escalated")).First).ToBeVisibleAsync();
            await BeatAsync(2);
            await ClickAsync(_page.Locator(".demo-date"));
            await ClickAsync(_page.GetByText(Lang.T("The client answers")));
            await Assertions.Expect(_page.GetByText(Prefix("The client's answer is in the file. Use \"{0}\".")).First).ToBeVisibleAsync();
            await ClickAsync(panel.GetByTestId("agent-verify"));
            await Assertions.Expect(panel.GetByText(Lang.T("format valid")).First).ToBeVisibleAsync(new() { Timeout = 180_000 });
            await RefuseReplayAsync(panel);
            await BeatAsync(2);
            await ClickAsync(panel.GetByRole(AriaRole.Button, new() { Name = Lang.T("Approve"), Exact = true }));
            await Assertions.Expect(_page.GetByText(Lang.T("Approved and executed")).Last).ToBeVisibleAsync();
            await BeatAsync(1);
            await ScrollToAsync(CaseHeading());
        }),
        new("S5", "Trei capcane: 13, 5a, 5c, 15", 22, async () =>
        {
            foreach (var id in new[] { "13", "5a", "5c", "15" })
            {
                await GoAsync($"/changes/new?scenario={id}");
                await BeatAsync(0.5);
                await ClickAsync(_page.GetByRole(AriaRole.Button, new() { Name = Lang.T("Record and apply the rules") }));
                await Assertions.Expect(_page.Locator(".crs-verdict .crs-verdict-title")).ToBeVisibleAsync();
                await BeatAsync(2.5);
            }
        }, Before: RealDateAsync),
        new("S6", "Scutul: Victor, CL-0010", 12, async () =>
        {
            await GoAsync("/clients/CL-0010");
            await BeatAsync(1.5);
            await ClickAsync(_page.GetByRole(AriaRole.Button, new() { Name = Lang.T("Record the upload and scan") }));
            var flag = _page.GetByText(Prefix("Red flag from the shield: {0} hidden fragment(s) removed")).First;
            await Assertions.Expect(flag).ToBeVisibleAsync();
            await BeatAsync(2);
            await ClickAsync(flag); // opens the fragment the shield removed
            await ScrollToAsync(flag, "center");
            await BeatAsync(4);
        }),
        new("S7", "Arhitectura si jurnalul", 22, async () =>
        {
            await GoAsync("/architecture");
            await BeatAsync(8);
            await GoAsync("/audit?tamper=1");
            var verify = _page.GetByRole(AriaRole.Button, new() { Name = Lang.T("Verify"), Exact = true });
            await ClickAsync(verify);
            await Assertions.Expect(_page.GetByText(Prefix("Chain intact: {0} entries checked")).First).ToBeVisibleAsync();
            await BeatAsync(2);
            // An entry of Maria's case when S2 ran in this take, otherwise the second row (as in AuditTests).
            var rows = _page.Locator("tbody tr");
            var row = _caseCode is not null && await rows.Filter(new() { HasText = _caseCode }).CountAsync() > 0
                ? rows.Filter(new() { HasText = _caseCode }).First
                : rows.Nth(1);
            var seq = (await row.Locator(".audit-seq").InnerTextAsync()).Trim();
            await ClickAsync(row.GetByRole(AriaRole.Button, new() { Name = Lang.T("Tamper") }));
            await ClickAsync(_page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = Lang.T("Tamper") }));
            await ClickAsync(verify);
            await Assertions.Expect(_page.GetByText(Lang.T("Chain broken at entry #{0}", seq))).ToBeVisibleAsync();
            await BeatAsync(3);
        }),
        new("S8", "Metricile: /metrics", 26, async () =>
        {
            await GoAsync("/metrics");
            await BeatAsync(5);
            await ScrollToAsync(_page.Locator(".metrics-table").First);
            await BeatAsync(6);
            await ScrollAsync(500);
        }),
        new("S9", "Fraza de final", 14, async () =>
        {
            await GoAsync("/");
            await BeatAsync(4);
        }),
    ];

    /// <summary>The filming rule: S3 and S4 change data, so a replayed run (tools not run) stops the take.</summary>
    private static async Task RefuseReplayAsync(ILocator panel)
    {
        if (await panel.GetByText(Lang.T("Replay"), new() { Exact = true }).CountAsync() > 0)
            throw new InvalidOperationException("The run is a Replay: tools did not run. Film on Live (Bedrock) or Mock, never Replay (docs/PITCH.md).");
    }

    /// <summary>After Maria, S5-S9 run on the real date again (the Demo menu, as an operator would).</summary>
    private async Task RealDateAsync()
    {
        if (await _page.Locator(".demo-date.is-ahead").CountAsync() == 0) return;
        await ClickAsync(_page.Locator(".demo-date"));
        await ClickAsync(_page.GetByText(Lang.T("Back to the real date")));
        await Assertions.Expect(_page.Locator(".demo-date.is-ahead")).ToHaveCountAsync(0);
    }

    private ILocator CaseHeading() =>
        _page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex(Regex.Escape(_caseCode ?? "CRS-")) }).First;

    private async Task ScrollToAsync(ILocator target, string block = "start")
    {
        await target.EvaluateAsync("(e, block) => e.scrollIntoView({ behavior: 'smooth', block })", block);
        await _page.WaitForTimeoutAsync(o.Fast ? 300 : 900);
    }

    private async Task GoAsync(string path)
    {
        await _page.GotoAsync(o.Url + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await _page.WaitForTimeoutAsync(600); // Blazor becomes interactive
    }

    /// <summary>Moves the visible cursor to the element, then clicks: the viewer sees where the click happens.</summary>
    private async Task ClickAsync(ILocator target)
    {
        await target.ScrollIntoViewIfNeededAsync();
        if (await target.BoundingBoxAsync() is { } box)
        {
            await _page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2, new() { Steps = o.Fast ? 5 : 30 });
            await BeatAsync(0.35);
        }
        await target.ClickAsync();
        await BeatAsync(0.4);
    }

    private async Task ScrollAsync(int pixels)
    {
        await _page.Mouse.MoveAsync(Width / 2f, Height / 2f, new() { Steps = 10 });
        for (var i = 0; i < 10; i++)
        {
            await _page.Mouse.WheelAsync(0, pixels / 10f);
            await _page.WaitForTimeoutAsync(o.Fast ? 10 : 60);
        }
    }

    private Task BeatAsync(double seconds) => _page.WaitForTimeoutAsync((float)(seconds * (o.Fast ? 300 : 1000)));

    /// <summary>The text before the first placeholder, in the UI language: "Chain intact: {0} ..." gives "Lanț întreg: ".</summary>
    private static string Prefix(string key) => Lang.T(key).Split('{')[0];

    private const string CursorScript = """
        (() => {
          if (window.__filmCursor) return;
          window.__filmCursor = true;
          const add = () => {
            // No elastic bounce at the end of a scroll (macOS): it shifts the whole page on camera. The margin keeps a
            // section title out from under the sticky app bar.
            const style = document.createElement('style');
            style.textContent = '* { overscroll-behavior: none !important; scroll-margin-top: 88px; }';
            document.documentElement.appendChild(style);
            const c = document.createElement('div');
            c.style.cssText = 'position:fixed;left:-40px;top:-40px;width:22px;height:22px;margin:-11px 0 0 -11px;border-radius:50%;' +
              'background:rgba(255,196,0,.35);border:2px solid rgba(230,150,0,.95);pointer-events:none;z-index:2147483647;transition:transform .12s';
            document.documentElement.appendChild(c);
            addEventListener('mousemove', e => { c.style.left = e.clientX + 'px'; c.style.top = e.clientY + 'px'; }, true);
            addEventListener('mousedown', () => c.style.transform = 'scale(.65)', true);
            addEventListener('mouseup', () => c.style.transform = '', true);
          };
          document.documentElement ? add() : addEventListener('DOMContentLoaded', add);
        })();
        """;
}
