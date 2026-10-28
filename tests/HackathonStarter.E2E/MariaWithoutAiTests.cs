using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>
/// End-to-end tests for the Maria golden case (case 4): CL-0001 seeded with her documents.
/// Tests verify the core paths work without AI: change application, case detail page, request/response workflow.
/// </summary>
[Collection("app")]
public class MariaWithoutAiTests(AppFixture app)
{
    [Fact]
    public async Task Dashboard_shows_CRS_sections_and_record_change_link()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");
        await app.GoAsync(page, "/dashboard");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Each change to client data becomes a decision" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Latest changes")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Cases by status")).ToBeVisibleAsync();

        var recordLink = page.GetByRole(AriaRole.Link, new() { Name = "Record a change" });
        await Assertions.Expect(recordLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Can_apply_Maria_scenario_and_see_decision()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");
        await app.GoAsync(page, "/changes/new");

        // Apply the Maria scenario.
        var demoScenarioSection = page.Locator(".app-list");
        var mariaButton = demoScenarioSection.GetByText("Maria", new() { Exact = false }).First;
        await mariaButton.ClickAsync();

        // Wait for decision to appear.
        await Assertions.Expect(page.GetByText("Clarification needed")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Verify indicia are shown.
        await Assertions.Expect(page.GetByText("CRS-IND-ADDR", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("CRS-IND-PHONE", new() { Exact = true })).ToBeVisibleAsync();

        // Verify Open case button is present.
        var openCaseButton = page.GetByRole(AriaRole.Link, new() { Name = "Open case" });
        await Assertions.Expect(openCaseButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Case_detail_page_shows_indicia_and_request_section()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");
        await app.GoAsync(page, "/changes/new");

        // Apply Maria scenario.
        var demoScenarioSection = page.Locator(".app-list");
        var mariaButton = demoScenarioSection.GetByText("Maria", new() { Exact = false }).First;
        await mariaButton.ClickAsync();

        await Assertions.Expect(page.GetByText("Clarification needed")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to case detail page.
        var openCaseButton = page.GetByRole(AriaRole.Link, new() { Name = "Open case" });
        var caseUrl = await openCaseButton.GetAttributeAsync("href");
        await page.GotoAsync($"{app.BaseUrl}{caseUrl}", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Verify case detail page loaded.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Case CRS-2026-0001" })).ToBeVisibleAsync();

        // Verify deadline information is shown.
        await Assertions.Expect(page.GetByText("days to the deadline")).ToBeVisibleAsync();

        // Verify indicia section.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Indicia found" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("CRS-IND-ADDR", new() { Exact = true })).ToBeVisibleAsync();

        // Verify Request section exists with Draft button.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Request" })).ToBeVisibleAsync();
        var draftButton = page.GetByRole(AriaRole.Button, new() { Name = "Draft request" });
        await Assertions.Expect(draftButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Viewer_cannot_record_change_on_dashboard()
    {
        var page = await app.NewPageAsync(user: "viewer", lang: "en");
        await app.GoAsync(page, "/dashboard");

        // Viewer should not see the Record a change link.
        var recordLink = page.GetByRole(AriaRole.Link, new() { Name = "Record a change" });
        await Assertions.Expect(recordLink).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Cases_page_lists_open_and_closed_cases()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");

        // First create a case by applying Maria scenario.
        await app.GoAsync(page, "/changes/new");
        var demoScenarioSection = page.Locator(".app-list");
        var mariaButton = demoScenarioSection.GetByText("Maria", new() { Exact = false }).First;
        await mariaButton.ClickAsync();

        await Assertions.Expect(page.GetByText("Clarification needed")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to cases page.
        await app.GoAsync(page, "/cases");

        // Verify the cases page loaded.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cases" })).ToBeVisibleAsync();

        // Verify case CRS-2026-0001 is listed (may be newly created or from a previous test setup).
        await Assertions.Expect(page.GetByText("CRS-2026-0001")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Admin_can_draft_request_on_case_page()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");
        await app.GoAsync(page, "/changes/new");

        // Apply Maria scenario.
        var demoScenarioSection = page.Locator(".app-list");
        var mariaButton = demoScenarioSection.GetByText("Maria", new() { Exact = false }).First;
        await mariaButton.ClickAsync();

        await Assertions.Expect(page.GetByText("Clarification needed")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to case.
        var openCaseButton = page.GetByRole(AriaRole.Link, new() { Name = "Open case" });
        var caseUrl = await openCaseButton.GetAttributeAsync("href");
        await page.GotoAsync($"{app.BaseUrl}{caseUrl}", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Admin should see Draft request button.
        var draftButton = page.GetByRole(AriaRole.Button, new() { Name = "Draft request" });
        await Assertions.Expect(draftButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Mobile_width_390px_renders_correctly()
    {
        var page = await app.NewPageAsync(user: "admin", width: 390, height: 844, lang: "en");
        await app.GoAsync(page, "/dashboard");

        // Mobile nav should be visible.
        var nav = page.Locator(".mobile-nav");
        await Assertions.Expect(nav).ToBeVisibleAsync();

        // Dashboard should be accessible.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Each change to client data becomes a decision" })).ToBeVisibleAsync();
    }
}
