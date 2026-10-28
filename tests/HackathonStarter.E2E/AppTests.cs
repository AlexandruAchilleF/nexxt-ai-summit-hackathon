using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>End-to-end: the real app in a real browser, like a user (and a judge) would use it.</summary>
[Collection("app")]
public class AppTests(AppFixture app)
{
    private static ILocator Snackbar(IPage page, string text) => page.Locator(".mud-snackbar").Filter(new() { HasText = text });
    private static ILocator ChatBox(IPage page) => page.GetByPlaceholder("Ask about items, a loan, or attach a document…");

    private static async Task AskAsync(IPage page, string question)
    {
        await ChatBox(page).FillAsync(question);
        await page.WaitForTimeoutAsync(150);
        await ChatBox(page).PressAsync("Enter");
    }

    [Theory]
    [InlineData("/scenarios", "Scenarios")]
    [InlineData("/runs", "Agent runs")]
    [InlineData("/exports", "Exports")]
    public async Task Product_pages_load_with_the_footer(string path, string heading)
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, path);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First).ToBeVisibleAsync(); // the app bar repeats the title
        await Assertions.Expect(page.Locator("footer.app-footer").GetByRole(AriaRole.Link, new() { Name = "Data and privacy" })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData("/privacy", "Data and privacy")]
    [InlineData("/privacy-policy", "Privacy policy")]
    [InlineData("/terms", "Terms and conditions")]
    [InlineData("/contact", "Contact")]
    public async Task Public_pages_load_outside_the_app(string path, string heading)
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, path);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("footer.lp-footer").GetByRole(AriaRole.Link, new() { Name = "Data and privacy" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".app-topbar")).ToHaveCountAsync(0); // no app shell
    }

    [Fact]
    public async Task Romanian_is_the_default_and_the_switch_changes_the_language()
    {
        var page = await app.NewPageAsync(lang: null);
        await app.GoAsync(page, "/");
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "ro");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Cum funcționează" })).ToBeVisibleAsync();

        await app.GoAsync(page, "/runs");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Rulări", Exact = true })).ToBeVisibleAsync();

        await page.Locator(".lang-switch").ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Agent runs", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/runs$"));
    }

    [Fact]
    public async Task A_scenario_runs_turn_by_turn_and_ends_with_checks()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/scenarios");

        await page.GetByRole(AriaRole.Button, new() { Name = "Vreau să investesc 10.000 EUR. Ce acțiuni să cumpăr?" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Checks:", new() { Exact = false }).First).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Assertions.Expect(page.GetByText("Customer", new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Items_crud_with_validation()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/items");

        await page.GetByRole(AriaRole.Button, new() { Name = "New item" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Create item" }).ClickAsync();
        await Assertions.Expect(page.GetByText("The Title field is required.")).ToBeVisibleAsync();

        await page.GetByLabel("Title").FillAsync("E2E item");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create item" }).ClickAsync();
        await Assertions.Expect(Snackbar(page, "Item created")).ToBeVisibleAsync();

        await page.GetByPlaceholder("Search items…").FillAsync("E2E item");
        await Assertions.Expect(page.Locator(".mud-table-body tr")).ToHaveCountAsync(1);
        await page.WaitForTimeoutAsync(800); // let the debounced search finish re-rendering the grid
        await page.Locator(".mud-table-body tr").First.GetByText("E2E item").ClickAsync();
        // Blazor navigates client-side (no full page load): assert on the URL, with retries.
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/items/\d+$"), new() { Timeout = 15_000 });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "E2E item" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync($"{app.BaseUrl}/items", new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Assistant_uses_tools_and_asks_for_approval()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/assistant");

        await page.GetByRole(AriaRole.Button, new() { Name = "Give me an overview of our items" }).ClickAsync();
        await Assertions.Expect(page.GetByText("getStats")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(new System.Text.RegularExpressions.Regex("You have \\d+ items"))).ToBeVisibleAsync();

        await AskAsync(page, "Monthly payment for a 50,000 EUR loan at 7.5% over 5 years?");
        await Assertions.Expect(page.GetByText("1.001,90 EUR").First).ToBeVisibleAsync();

        await AskAsync(page, "Arhivează elementul „Write demo script”");
        await Assertions.Expect(page.GetByText("Approval needed")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Approved by Ana Admin").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Assistant_reads_an_uploaded_pdf()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/assistant");

        await page.SetInputFilesAsync("input[type=file]", Path.Combine(AppContext.BaseDirectory, "Fixtures", "loan-application.pdf"));
        await Assertions.Expect(page.Locator(".mud-chip", new() { HasText = "loan-application.pdf" })).ToBeVisibleAsync();
        await AskAsync(page, "Summarize the attached document");
        await Assertions.Expect(page.GetByText(new System.Text.RegularExpressions.Regex("I read loan-application.pdf"))).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Monthly income: 6,500 RON", new() { Exact = false }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Assistant_accepts_a_photo()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/assistant");

        await page.SetInputFilesAsync("input[type=file]", Path.Combine(AppContext.BaseDirectory, "Fixtures", "receipt-photo.png"));
        await Assertions.Expect(page.Locator(".mud-chip", new() { HasText = "receipt-photo.jpg" })).ToBeVisibleAsync(); // resized to JPEG in the browser
        await AskAsync(page, "What does this receipt say?");
        await Assertions.Expect(page.GetByText(new System.Text.RegularExpressions.Regex("I received the image"))).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Works_as_an_installable_phone_app()
    {
        var page = await app.NewPageAsync(width: 390, height: 844);
        await app.GoAsync(page, "/dashboard");

        var nav = page.Locator(".mobile-nav");
        await Assertions.Expect(nav).ToBeVisibleAsync();
        await nav.GetByText("Cases", new() { Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/cases$"));

        await app.GoAsync(page, "/assistant"); // the Assistant left the bottom bar; it stays reachable by URL
        await page.WaitForTimeoutAsync(500);
        await AskAsync(page, "Give me an overview of our items");
        await Assertions.Expect(page.GetByText(new System.Text.RegularExpressions.Regex("You have \\d+ items"))).ToBeVisibleAsync();
        await Assertions.Expect(ChatBox(page)).ToHaveValueAsync(""); // cleared after sending

        Assert.True(await page.EvaluateAsync<bool>("async () => !!(await navigator.serviceWorker.ready)"));
        var manifest = await app.Http.GetFromJsonAsync<JsonElement>($"{app.BaseUrl}/manifest.webmanifest");
        Assert.Equal("standalone", manifest.GetProperty("display").GetString());
        Assert.True(manifest.GetProperty("icons").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Conversation_survives_a_page_reload()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/assistant");

        await AskAsync(page, "Give me an overview of our items");
        // The id is added with history.replaceState (no navigation event), so poll the URL instead of WaitForURL.
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"\?c="));
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle }); // mid-run

        // After a reload the circuit is rebuilt from scratch; give the first (cold) run more time.
        var slow = new LocatorAssertionsToBeVisibleOptions { Timeout = 20_000 };
        await Assertions.Expect(page.GetByText("Give me an overview of our items")).ToBeVisibleAsync(slow);
        await Assertions.Expect(page.GetByText(new System.Text.RegularExpressions.Regex("You have \\d+ items"))).ToBeVisibleAsync(slow);
    }

    [Fact]
    public async Task Arena_debate_ends_with_a_verdict()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/arena");

        await page.GetByRole(AriaRole.Button, new() { Name = "Should banks let AI agents approve small loans automatically?" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Judge's verdict")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByText("Skeptic").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Viewer_cannot_edit_or_approve()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/items");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "New item" })).ToHaveCountAsync(0);

        await app.GoAsync(page, "/assistant");
        await AskAsync(page, "Archive the 'Old prototype' item");
        await Assertions.Expect(page.GetByText("Approval needed")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
        await Assertions.Expect(Snackbar(page, "can't do this")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Calculators_show_the_correct_loan_payment()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/calculators");
        await Assertions.Expect(page.GetByText("1.001,90 EUR")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Api_streams_agent_events_and_returns_the_run()
    {
        var body = new StringContent("""{"messages":[{"role":"user","content":"Give me an overview of our items"}]}""", Encoding.UTF8, "application/json");
        using var response = await app.Http.PostAsync($"{app.BaseUrl}/api/agents/assistant", body);
        response.EnsureSuccessStatusCode();

        var lines = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement).ToList();
        Assert.Equal("run", lines[0].GetProperty("type").GetString());
        Assert.Contains(lines, l => l.GetProperty("type").GetString() == "step");
        var done = lines[^1];
        Assert.Equal("done", done.GetProperty("type").GetString());
        Assert.Equal("Completed", done.GetProperty("run").GetProperty("status").GetString());

        Assert.StartsWith("You have", done.GetProperty("answer").GetString());

        var runId = done.GetProperty("run").GetProperty("id").GetString();
        var run = await app.Http.GetFromJsonAsync<JsonElement>($"{app.BaseUrl}/api/runs/{runId}");
        Assert.Equal(runId, run.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Api_without_streaming_returns_the_answer_and_pending_approvals()
    {
        var body = new StringContent("""{"messages":[{"role":"user","content":"Archive the 'Integrate payments' item"}]}""", Encoding.UTF8, "application/json");
        using var response = await app.Http.PostAsync($"{app.BaseUrl}/api/agents/assistant?stream=false", body);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("AwaitingApproval", result.GetProperty("status").GetString());
        var approval = result.GetProperty("pendingApprovals")[0];
        Assert.Equal("updateItem", approval.GetProperty("tool").GetString());

        var decided = await app.Http.PostAsJsonAsync(
            $"{app.BaseUrl}/api/runs/{result.GetProperty("runId").GetString()}/approvals/{approval.GetProperty("id").GetString()}", new { approved = false });
        Assert.Equal("Completed", (await decided.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}
