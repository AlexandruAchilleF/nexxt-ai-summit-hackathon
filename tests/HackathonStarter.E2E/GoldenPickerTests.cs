using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>Golden case 12 through the picker and direct link, on desktop and phone.</summary>
[Collection("app")]
public class GoldenPickerTests(AppFixture app)
{
    [Theory]
    [InlineData(390, false)]
    [InlineData(1360, false)]
    [InlineData(390, true)]
    [InlineData(1360, true)]
    public async Task Golden_case_12_opens_a_case_that_matches_the_card(int width, bool directLink)
    {
        var page = await app.NewPageAsync(width: width, height: 844);
        await app.GoAsync(page, directLink ? "/changes/new?scenario=12" : "/changes/new?golden=1");
        if (!directLink)
            await page.Locator(".crs-chip", new() { HasText = "In-care-of address in Italy" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Matches golden case 12")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".crs-rule.is-hit .crs-code", new() { HasText = "CRS-IND-CO" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".crs-rule.is-hit .crs-code", new() { HasText = "CRS-IND-MAIL" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("a[href^='/cases/CRS-']").First).ToBeVisibleAsync();
        Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > window.innerWidth"), $"no horizontal scroll at {width}px");
    }
}
