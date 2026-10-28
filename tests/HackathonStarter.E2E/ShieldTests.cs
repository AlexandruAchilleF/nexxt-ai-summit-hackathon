using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>Golden case 10 on screen: Victor's statement is on file unscanned; recording the upload raises the shield's red flag.</summary>
[Collection("app")]
public class ShieldTests(AppFixture app)
{
    [Fact]
    public async Task Scanning_Victors_statement_shows_the_red_flag()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/clients/CL-0010");

        await page.GetByRole(AriaRole.Button, new() { Name = "Record the upload and scan" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Hidden text removed")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Record the upload and scan" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Viewer_does_not_see_the_scan_button()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/clients/CL-0010");

        await Assertions.Expect(page.GetByText("Not scanned").First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Record the upload and scan" })).ToHaveCountAsync(0);
    }
}
