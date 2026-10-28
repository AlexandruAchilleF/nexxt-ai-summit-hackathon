using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>The /audit page: verify the hash chain, tamper with an entry as admin, see the chain break exactly there.</summary>
[Collection("app")]
public class AuditTests(AppFixture app)
{
    [Fact]
    public async Task Tampering_an_entry_breaks_the_chain_exactly_there()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/audit");

        await page.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Chain intact")).ToBeVisibleAsync();

        var row = page.Locator("tbody tr").Nth(1);
        var seq = (await row.Locator(".audit-seq").InnerTextAsync()).Trim();
        await row.GetByRole(AriaRole.Button, new() { Name = "Tamper" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Tamper" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
        await Assertions.Expect(page.GetByText($"Chain broken at entry #{seq}")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Viewer_does_not_see_tamper()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/audit");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Audit log" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Tamper" })).ToHaveCountAsync(0);
    }
}
