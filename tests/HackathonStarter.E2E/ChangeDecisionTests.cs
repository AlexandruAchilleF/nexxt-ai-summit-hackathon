using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>
/// The route without the agent: a change on /changes/new, the decision of the code with rule IDs and guide pages, and the
/// case it opens. Uses golden cases no other test touches (CL-0011, CL-0015), since the collection shares one database.
/// </summary>
[Collection("app")]
public class ChangeDecisionTests(AppFixture app)
{
    [Fact]
    public async Task Power_of_attorney_abroad_opens_a_case_with_the_indicium_and_the_deadline()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/changes/new?scenario=11");
        await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();

        var verdict = page.Locator(".crs-verdict");
        await Assertions.Expect(verdict.Locator(".crs-verdict-title")).ToHaveTextAsync("Clarification");
        await Assertions.Expect(verdict.GetByText("Matches golden case 11")).ToBeVisibleAsync();
        await Assertions.Expect(verdict.Locator(".crs-rule.is-hit")).ToContainTextAsync("CRS-IND-POA");

        var link = verdict.Locator("a[href^='/cases/CRS-']");
        var href = await link.GetAttributeAsync("href");
        await app.GoAsync(page, href!);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Case {href!.Split('/')[^1]}" })).ToBeVisibleAsync();
        var indicium = page.Locator(".crs-ind").Filter(new() { HasText = "CRS-IND-POA" });
        await Assertions.Expect(indicium).ToContainTextAsync("Uncovered: starts the deadline");
        await Assertions.Expect(indicium).ToContainTextAsync(new Regex(@"guide p\. \d+"));
        await Assertions.Expect(page.Locator(".crs-clock")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_move_inside_Romania_opens_no_case_and_says_why()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/changes/new?scenario=15");
        await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();

        var verdict = page.Locator(".crs-verdict");
        await Assertions.Expect(verdict.Locator(".crs-verdict-title")).ToHaveTextAsync("No case");
        await Assertions.Expect(verdict.GetByText("Matches golden case 15")).ToBeVisibleAsync();
        await Assertions.Expect(verdict.Locator(".crs-rule").Filter(new() { HasText = "GEN-DOMESTIC" })).ToContainTextAsync("does not apply");
        await Assertions.Expect(verdict.Locator("a[href^='/cases/CRS-']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Viewer_cannot_record_a_change()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/changes/new");

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" })).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByText("Viewers can't record changes.", new() { Exact = false })).ToBeVisibleAsync();
    }
}
