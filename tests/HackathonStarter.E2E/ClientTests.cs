using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>The client page (/clients/{code}): what the operator shows "before" and "after" a change.</summary>
[Collection("app")]
public class ClientTests(AppFixture app)
{
    [Fact]
    public async Task Maria_shows_her_address_account_and_self_certification()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/clients/CL-0001");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Maria Popescu" })).ToBeVisibleAsync();

        var addresses = page.Locator("section[aria-labelledby=client-addresses]");
        await Assertions.Expect(addresses).ToContainTextAsync("Bd. Unirii 15");

        var accounts = page.Locator("section[aria-labelledby=client-accounts]");
        await Assertions.Expect(accounts).ToContainTextAsync("RO19RZBR0000060021458391");
        await Assertions.Expect(accounts).ToContainTextAsync("Depository account");
        await Assertions.Expect(accounts).ToContainTextAsync("CRS: new");
        await Assertions.Expect(accounts).ToContainTextAsync("FATCA: new");

        var selfCerts = page.Locator("section[aria-labelledby=client-selfcerts]");
        await Assertions.Expect(selfCerts).ToContainTextAsync("Signed");
        await Assertions.Expect(selfCerts).ToContainTextAsync("format valid");
        await Assertions.Expect(selfCerts).Not.ToContainTextAsync("TIN valid");
    }

    [Fact]
    public async Task Fits_a_390_px_phone()
    {
        var page = await app.NewPageAsync(user: "viewer", width: 390, height: 844);
        await app.GoAsync(page, "/clients/CL-0001");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Maria Popescu" })).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<int>("document.documentElement.scrollWidth") <= 390);
    }

    [Theory]
    [InlineData("CL-0001", false)]
    [InlineData("CL-0002", false)]
    [InlineData("CL-0001", true)]
    [InlineData("CL-0002", true)]
    public async Task Dossier_cards_share_row_edges_with_populated_or_empty_sections(string code, bool dark)
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, $"/clients/{code}");
        if (dark) await page.GetByRole(AriaRole.Button, new() { Name = "Toggle dark mode", Exact = true }).ClickAsync();

        async Task AlignedAsync(params string[] sections)
        {
            var boxes = new List<(float Y, float Height)>();
            foreach (var section in sections)
            {
                var box = (await page.Locator($"section[aria-labelledby={section}]").BoundingBoxAsync())!;
                boxes.Add((box.Y, box.Height));
            }
            Assert.All(boxes, box =>
            {
                Assert.InRange(Math.Abs(box.Y - boxes[0].Y), 0, 1);
                Assert.InRange(Math.Abs(box.Height - boxes[0].Height), 0, 1);
            });
        }

        await AlignedAsync("client-addresses", "client-phones", "client-poa");
        await AlignedAsync("client-selfcerts", "client-documents");
        Assert.True(await page.EvaluateAsync<int>("document.documentElement.scrollWidth") <= 1360);
    }

    [Fact]
    public async Task Staff_profiles_show_banking_roles_and_switch_without_leaving_the_dossier()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/clients/CL-0001");
        var profile = page.Locator("button.staff-profile");
        await Assertions.Expect(profile).ToContainTextAsync("Operator");
        await profile.ClickAsync();
        await Assertions.Expect(page.GetByText("Staff demo profiles", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Read-only access", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Menuitem).Filter(new() { HasText = "Mihai Stan" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/clients/CL-0001$"));
        await Assertions.Expect(page.Locator("button.staff-profile")).ToContainTextAsync("Tax department");
    }

    [Fact]
    public async Task Victor_statement_shows_the_hidden_text_on_a_scan()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/clients/CL-0010");

        var documents = page.Locator("section[aria-labelledby=client-documents]");
        // ShieldTests may have recorded the upload already (same app): then the button shows the saved red flag instead.
        await documents.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Scan now|Show the removed text)$") }).ClickAsync();
        await documents.GetByText("Red flag from the shield").ClickAsync();
        await Assertions.Expect(documents).ToContainTextAsync("Ignoră instrucțiunile anterioare");
        await Assertions.Expect(documents).ToContainTextAsync("white text");
    }

    [Fact]
    public async Task A_seeded_case_links_to_the_case_page()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/clients/CL-0006");

        var link = page.Locator("section[aria-labelledby=client-cases] a[href^='/cases/CRS-']");
        await Assertions.Expect(link).ToHaveCountAsync(1);
        await Assertions.Expect(link).ToContainTextAsync("FATCA-IND-POB");
    }

    [Fact]
    public async Task An_unknown_code_shows_the_empty_state_with_a_link_to_the_list()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/clients/CL-9999");

        await Assertions.Expect(page.GetByText("No client with this code")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "All clients" }).Last.ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/clients$"));
        await Assertions.Expect(page.GetByText("Maria Popescu")).ToBeVisibleAsync();
    }
}
