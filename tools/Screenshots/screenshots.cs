// The README screenshots, regenerated from a running app (English UI, 1536x864 at 1.25 = 1920x1080).
// Start the app on the mock, reset the demo data (Settings), then:
//   dotnet run tools/Screenshots/screenshots.cs -- [url] [outDir] [lang] [only,these,steps]
// The steps change data (Maria's case, the traps, the shield scan, the audit tamper): run them on a local app, not on Render.
#:package Microsoft.Playwright@1.63.0
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
using Microsoft.Playwright;

var url = args.Length > 0 ? args[0] : "http://localhost:5000";
var outDir = args.Length > 1 ? args[1] : "docs/screenshots";
var lang = args.Length > 2 ? args[2] : "en";
var only = args.Length > 3 ? args[3].Split(',') : null;
Directory.CreateDirectory(outDir);

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1536, Height = 864 }, DeviceScaleFactor = 1.25f });
await context.AddCookiesAsync([new Cookie { Name = "app_lang", Value = lang, Url = url }]);
// No toasts in the stills.
await context.AddInitScriptAsync("document.addEventListener('DOMContentLoaded',()=>{const s=document.createElement('style');s.textContent='.mud-snackbar-provider,#blazor-error-ui{display:none!important}';document.head.appendChild(s);});");
var page = await context.NewPageAsync();
page.SetDefaultTimeout(30_000);
await page.GotoAsync($"{url}/switch-user/admin?returnUrl=/dashboard", new() { WaitUntil = WaitUntilState.NetworkIdle });

async Task Go(string path) { await page.GotoAsync(url + path, new() { WaitUntil = WaitUntilState.NetworkIdle }); await page.WaitForTimeoutAsync(1200); }
async Task Shot(string name) { await page.AddStyleTagAsync(new() { Content = ".mud-snackbar,.mud-snackbar-provider,.mud-snackbar-location-top-right{display:none!important}" }); await page.Mouse.MoveAsync(1, 1); await page.WaitForTimeoutAsync(500); await page.ScreenshotAsync(new() { Path = Path.Combine(outDir, name + ".png") }); Console.WriteLine(name); }
async Task Scroll(ILocator l, string block = "start") { await l.EvaluateAsync("(e,b)=>e.scrollIntoView({block:b})", block); await page.WaitForTimeoutAsync(500); }
bool Want(string n) => only is null || only.Contains(n);


var dateMenu = page.Locator(".demo-date");
async Task DemoMenu(string item) { await dateMenu.ClickAsync(); await page.GetByText(item).First.ClickAsync(); await page.WaitForTimeoutAsync(1200); }

// Seeded state first; then the steps that change data, the audit tamper last.
if (Want("landing")) { await Go("/"); await Shot("landing"); }
if (Want("dashboard")) { await Go("/dashboard"); await Shot("dashboard"); }
if (Want("architecture")) { await Go("/architecture"); await Shot("architecture"); }
if (Want("mobile")) {
    var m = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 }, DeviceScaleFactor = 2, IsMobile = true, HasTouch = true });
    await m.AddCookiesAsync([new Cookie { Name = "app_lang", Value = lang, Url = url }]);
    var mp = await m.NewPageAsync();
    await mp.GotoAsync($"{url}/switch-user/admin?returnUrl=/dashboard", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await mp.WaitForTimeoutAsync(1500);
    await mp.ScreenshotAsync(new() { Path = Path.Combine(outDir, "mobile-dashboard.png") });
    Console.WriteLine("mobile-dashboard");
}
if (Want("metrics")) { await Go("/metrics"); await Shot("metrics"); }
if (Want("maria")) {
    await Go("/changes/new?scenario=4");
    await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();
    var link = page.Locator(".crs-verdict a[href^='/cases/CRS-']").First;
    await link.WaitForAsync();
    await Shot("change-maria");
    var code = (await link.GetAttributeAsync("href"))!.Split('/')[^1];
    Console.WriteLine($"case {code}");
    await Go($"/cases/{code}");
    var panel = page.Locator(".case-agent");
    await panel.GetByTestId("agent-analyze").ClickAsync();
    var approve = panel.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true });
    await approve.WaitForAsync(new() { Timeout = 120_000 });
    await Scroll(panel); await Shot("agent-approval");
    await approve.ClickAsync();
    await page.GetByText("Awaiting client").First.WaitForAsync();
    await page.WaitForTimeoutAsync(800);
    await Scroll(page.Locator(".crs-grid.two").First); await Shot("case-evidence");
    await DemoMenu("To the reminder day");
    await page.GetByText("Escalated").First.WaitForAsync();
    await Go($"/cases/{code}");
    await DemoMenu("The client answers");
    await panel.GetByTestId("agent-verify").ClickAsync();
    await panel.GetByText("format valid").First.WaitForAsync(new() { Timeout = 120_000 });
    await panel.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
    await page.WaitForTimeoutAsync(2500);
    await Go($"/cases/{code}"); await Shot("case-closed");
    await DemoMenu("Back to the real date");
}
if (Want("change")) {
    foreach (var id in new[] { "5a", "5c" }) {
        await Go($"/changes/new?scenario={id}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();
        await page.Locator(".crs-verdict .crs-verdict-title").WaitForAsync();
        await Shot($"trap-{id}");
    }
}
if (Want("shield")) {
    await Go("/clients/CL-0010");
    var scan = page.GetByRole(AriaRole.Button, new() { Name = "Record the upload and scan" });
    if (await scan.CountAsync() > 0) await scan.ClickAsync();
    var flag = page.GetByText("Red flag from the shield").First;
    await flag.ClickAsync(); await page.WaitForTimeoutAsync(600); await Scroll(flag, "center");
    await Shot("shield");
}
if (Want("audit")) {
    await Go("/audit?tamper=1");
    var verify = page.GetByRole(AriaRole.Button, new() { Name = "Verify", Exact = true });
    await verify.ClickAsync(); await page.WaitForTimeoutAsync(1200);
    var rows = page.Locator("tbody tr");
    var row = await rows.Filter(new() { HasText = "Case closed" }).CountAsync() > 0 ? rows.Filter(new() { HasText = "Case closed" }).First : rows.Nth(1);
    await row.GetByRole(AriaRole.Button, new() { Name = "Tamper" }).ClickAsync();
    await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Tamper" }).ClickAsync();
    await page.WaitForTimeoutAsync(800);
    await verify.ClickAsync(); await page.WaitForTimeoutAsync(1500);
    await Shot("audit");
}
