using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>The /metrics page: the three parts of the evaluation report, each with its source, and the golden cases row by row.</summary>
[Collection("app")]
public class MetricsTests(AppFixture app)
{
    [Fact]
    public async Task Shows_the_three_parts_and_the_golden_cases_row_by_row()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/metrics");

        foreach (var part in new[] { "1. Specification coverage", "2. Where there is uncertainty", "3. Efficiency" })
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = part })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Run again" })).ToHaveCountAsync(3);

        var maria = page.Locator(".metrics-table tbody tr").Filter(new() { HasText = "Maria: Munich" });
        await Assertions.Expect(maria).ToHaveCountAsync(1);
        await Assertions.Expect(maria).ToContainTextAsync("CRS-IND-ADDR:DE");

        var runAgain = page.GetByRole(AriaRole.Button, new() { Name = "Run again" }).First;
        await runAgain.ClickAsync();
        await Assertions.Expect(runAgain).ToBeEnabledAsync();
        await Assertions.Expect(maria).ToContainTextAsync("CRS-IND-ADDR:DE");
    }
}
