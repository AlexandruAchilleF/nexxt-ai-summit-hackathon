using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

[Collection("app")]
public class LandingTests(AppFixture app)
{
    [Theory]
    [InlineData("ro", 390)]
    [InlineData("en", 390)]
    [InlineData("ro", 1440)]
    [InlineData("en", 1440)]
    public async Task Public_landing_uses_TRACE_light_theme_and_the_real_workflow(string language, int width)
    {
        var page = await app.NewPageAsync(lang: language, width: width);
        await app.GoAsync(page, "/");
        var english = language == "en";
        var cta = english ? "Open the application" : "Deschide aplicația";
        var how = english ? "How it works" : "Cum funcționează";
        var synthetic = english ? "Demonstration with synthetic customer data." : "Demonstrație cu date de client sintetice.";

        await Assertions.Expect(page).ToHaveTitleAsync("TRACE");
        await Assertions.Expect(page.Locator(".lp-nav .brand-logo")).ToContainTextAsync("TRACE");
        await Assertions.Expect(page.Locator(".lp-nav .brand-logo")).ToContainTextAsync("Tax Residency Anomaly Correlation Engine");
        await Assertions.Expect(page.Locator(".lp")).Not.ToContainTextAsync("CRS Sentinel");
        await Assertions.Expect(page.Locator(".lp-demo-note")).ToHaveTextAsync(synthetic);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = cta, Exact = true })).ToHaveCountAsync(3);
        await Assertions.Expect(page.Locator(".lp-hero-actions a")).ToHaveAttributeAsync("href", "/dashboard");
        await Assertions.Expect(page.Locator(".lp-preview")).ToContainTextAsync("CRS-IND-ADDR");
        await Assertions.Expect(page.Locator(".lp-preview")).ToContainTextAsync("CRS-IND-PHONE");
        await Assertions.Expect(page.Locator(".lp-preview")).Not.ToContainTextAsync("1 / 12");
        await Assertions.Expect(page.Locator(".lp-preview button, .lp-preview input")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".lp-audit")).ToContainTextAsync(english ? "hash-chained log" : "lanț de hash-uri");
        await Assertions.Expect(page.Locator(".lp")).ToHaveCSSAsync("background-color", "rgb(243, 246, 250)");
        Assert.Contains("IBM Plex Sans", await page.EvaluateAsync<string>("getComputedStyle(document.body).fontFamily"));
        Assert.True(await page.EvaluateAsync<int>("document.documentElement.scrollWidth") <= width);

        await page.GetByRole(AriaRole.Link, new() { Name = how, Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/#how$"));
        await Assertions.Expect(page.Locator("#how")).ToBeInViewportAsync();
        await page.Locator(".lp-nav a.lp-btn").ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/dashboard$"));
        await Assertions.Expect(page.Locator(".app-topbar")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Landing_language_switch_and_privacy_footer_keep_the_public_identity()
    {
        var page = await app.NewPageAsync(lang: "ro", width: 390);
        await app.GoAsync(page, "/");
        await page.Locator(".lang-switch").ClickAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await Assertions.Expect(page.Locator(".lp-hero-actions a")).ToHaveTextAsync("Open the application");
        await Assertions.Expect(page).ToHaveURLAsync(app.BaseUrl + "/");

        await page.Locator("footer.lp-footer").GetByRole(AriaRole.Link, new() { Name = "Data and privacy", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/privacy$"));
        await Assertions.Expect(page.Locator(".lp")).ToHaveCSSAsync("background-color", "rgb(243, 246, 250)");
        await Assertions.Expect(page.Locator(".lp-nav .brand-logo")).ToContainTextAsync("TRACE");
        await Assertions.Expect(page.Locator(".app-topbar")).ToHaveCountAsync(0);
    }
}
