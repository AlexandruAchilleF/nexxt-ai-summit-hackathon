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
        // The seeded change history opens cases first, so Maria's code is read from the link.
        var caseCode = caseUrl!.Split('/')[^1];
        await page.GotoAsync($"{app.BaseUrl}{caseUrl}", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Verify case detail page loaded.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Case {caseCode}" })).ToBeVisibleAsync();

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
        // The seeded change history opens cases first, so Maria's code is read from the link.
        var caseCode = (await page.GetByRole(AriaRole.Link, new() { Name = "Open case" }).GetAttributeAsync("href"))!.Split('/')[^1];

        // Navigate to cases page.
        await app.GoAsync(page, "/cases");

        // Verify the cases page loaded.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cases" })).ToBeVisibleAsync();

        // Verify Maria's case is listed (may be newly created or from a previous test setup).
        var caseRow = page.GetByText(caseCode, new() { Exact = true }).First;
        await Assertions.Expect(caseRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
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

    [Fact]
    public async Task Golden_case_4_register_response_with_self_certification()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");

        // Apply Maria scenario to create the case.
        await app.GoAsync(page, "/changes/new");
        var demoScenarioSection = page.Locator(".app-list");
        var mariaButton = demoScenarioSection.GetByText("Maria", new() { Exact = false }).First;
        await mariaButton.ClickAsync();

        // Wait for decision and verify.
        await Assertions.Expect(page.GetByText("Clarification needed")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to case.
        var openCaseButton = page.GetByRole(AriaRole.Link, new() { Name = "Open case" });
        var caseUrl = await openCaseButton.GetAttributeAsync("href");
        // The seeded change history opens cases first, so Maria's code is read from the link.
        var caseCode = caseUrl!.Split('/')[^1];
        await page.GotoAsync($"{app.BaseUrl}{caseUrl}", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Case should show Maria's case code.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Case {caseCode}" })).ToBeVisibleAsync();

        // First, draft and approve a request to move the case to AwaitingClient status.
        var draftButton = page.GetByRole(AriaRole.Button, new() { Name = "Draft request" });
        await draftButton.ClickAsync();

        var messageField = page.GetByLabel("Message to client");
        await messageField.FillAsync("Please provide documentation.");

        var saveButton = page.GetByRole(AriaRole.Button, new() { Name = "Save request" });
        await saveButton.ClickAsync();

        // Approve the request.
        var approveButton = page.GetByRole(AriaRole.Button, new() { Name = "Approve" });
        await approveButton.ClickAsync();

        // Now the response form should be visible. Register response with new self-certification.
        var registerButton = page.GetByRole(AriaRole.Button, new() { Name = "Register response" });
        await registerButton.ClickAsync();

        // The form should show with response kind already selected as NewSelfCertification.
        // Fill the residence country.
        var residenceSelect = page.GetByRole(AriaRole.Combobox, new() { Name = "Residence country" });
        await residenceSelect.ClickAsync();
        var germanyOption = page.GetByRole(AriaRole.Option, new() { Name = "Germany" });
        await germanyOption.ClickAsync();

        // Fill TIN.
        var tinField = page.GetByLabel("TIN");
        await tinField.FillAsync("86095174223");

        // Fill signed date - click to open date picker and type the date.
        var dateField = page.GetByLabel("Signed date");
        await dateField.ClickAsync();
        // The date picker should open; type the date.
        await page.Keyboard.TypeAsync("01/22/2027");

        // Channel is online by default, fill summary.
        var summaryField = page.GetByLabel("Summary");
        await summaryField.FillAsync("New self-certification for Germany residence.");

        // Register the response.
        var registerResponseButton = page.GetByRole(AriaRole.Button, new() { Name = "Register" }).Last;
        await registerResponseButton.ClickAsync();

        // Wait and verify response is recorded (no error thrown).
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Case {caseCode}" })).ToBeVisibleAsync(new() { Timeout = 5_000 });

        // The response should be visible now.
        await Assertions.Expect(page.GetByText("NewSelfCertification")).ToBeVisibleAsync();

        // Close the case should now be available and work.
        var closeButton = page.GetByRole(AriaRole.Button, new() { Name = "Close case" });
        await closeButton.ClickAsync();

        // Verify case is closed - check the pill with exact match.
        var closedPill = page.GetByText("Closed", new() { Exact = true }).First;
        await Assertions.Expect(closedPill).ToBeVisibleAsync();
    }
}
