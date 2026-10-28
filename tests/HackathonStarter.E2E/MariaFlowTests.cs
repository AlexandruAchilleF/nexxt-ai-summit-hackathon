using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>
/// Maria's case end to end (docs/PLAN.md 6.3, docs/GOLDEN-CASES.md case 4), the way the demo video runs it: apply the
/// change, open the case, run "Analizează dosarul" action, approve the request, demo date moves +85 days, client answers,
/// run "Verifică răspunsul" action, approve the closing, case is Closed. Needs the real (or interim) CRS services, the demo
/// scenarios and the agent's mock: run with --filter Flow=Maria once they are integrated.
/// </summary>
[Collection("app")]
public class MariaFlowTests(AppFixture app)
{
    private static readonly Regex CaseUrl = new(@"/cases/CRS-\d{4}-\d{4}(\?|$)");
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 60_000 };

    private static ILocator Status(IPage page) => page.GetByTestId("case-status");
    private static ILocator Approve(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true });

    [Fact, Trait("Flow", "Maria")]
    public async Task Maria_case_runs_from_the_change_to_the_closing()
    {
        var page = await app.NewPageAsync();
        try
        {
            // 1. The change: Maria moves to Munich and her phone becomes German. Code decides: two CRS indicia, a case.
            await app.GoAsync(page, "/changes/new");
            var maria = page.Locator("[data-scenario='maria']");
            await maria.GetByRole(AriaRole.Button, new() { Name = "Apply change" }).ClickAsync();
            var result = page.GetByTestId("change-result");
            await Assertions.Expect(result.GetByText("CRS-IND-ADDR").First).ToBeVisibleAsync();
            await Assertions.Expect(result.GetByText("CRS-IND-PHONE").First).ToBeVisibleAsync();

            await result.GetByRole(AriaRole.Link, new() { NameRegex = new Regex(@"^Open case CRS-\d{4}-\d{4}$") }).ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(CaseUrl);

            // 2. The case: indicia with rule and page, the 90-day deadline of a new account.
            await Assertions.Expect(page.GetByTestId("indicia").GetByText("CRS-IND-ADDR").First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("indicia").GetByText("CRS-IND-PHONE").First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("due-date")).ToHaveTextAsync(new Regex(@"\d{1,2} \S+ 20\d\d"));
            await Assertions.Expect(page.GetByTestId("deadline-rule")).ToContainTextAsync("DL-NEW-90");
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Open");

            // 3. Run 1: click "Analizează dosarul" (Analyze the dossier) action button
            await page.GetByRole(AriaRole.Button, new() { Name = "Analyze the dossier" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Approval needed")).ToBeVisibleAsync(Slow);

            // The viewer sees the same proposal read-only: no Approve, no Reject.
            var vera = await app.NewPageAsync(user: "viewer");
            await app.GoAsync(vera, new Uri(page.Url).PathAndQuery);
            await Assertions.Expect(vera.GetByText("Approval needed")).ToBeVisibleAsync(Slow);
            await Assertions.Expect(Approve(vera)).ToHaveCountAsync(0);
            await Assertions.Expect(vera.GetByRole(AriaRole.Button, new() { Name = "Reject", Exact = true })).ToHaveCountAsync(0);

            await Approve(page).ClickAsync();
            await Assertions.Expect(page.GetByText("Approved by Ana Admin").First).ToBeVisibleAsync();
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Awaiting client", new() { Timeout = 15_000 });
            await Assertions.Expect(page.GetByTestId("requests")).ToContainTextAsync("Sent");

            // While the case waits for Maria, editors can simulate her answer; the viewer cannot, nor move the date.
            await app.GoAsync(vera, new Uri(page.Url).PathAndQuery);
            await Assertions.Expect(Status(vera)).ToHaveTextAsync("Awaiting client");
            await Assertions.Expect(vera.GetByRole(AriaRole.Button, new() { Name = "Simulate the client's answer" })).ToHaveCountAsync(0);
            await Assertions.Expect(vera.GetByRole(AriaRole.Button, new() { Name = "+85 days" })).ToHaveCountAsync(0);
            await vera.Context.CloseAsync();

            // 4. The demo date jumps to day 85: the deadline watcher sends the reminder and escalates.
            await page.GetByRole(AriaRole.Button, new() { Name = "+85 days" }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("demo-date")).ToContainTextAsync("+85");
            await Assertions.Expect(Status(page)).ToHaveTextAsync(new Regex("^(Reminder sent|Escalated to tax)$"), new() { Timeout = 15_000 });

            // 5. Maria answers with a new self-certification.
            await page.GetByRole(AriaRole.Button, new() { Name = "Simulate the client's answer" }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("delivered")).ToBeVisibleAsync();

            // 6. Run 2: click "Verifică răspunsul" (Check the response) action button
            await page.GetByRole(AriaRole.Button, new() { Name = "Check the response" }).ClickAsync();
            await Assertions.Expect(Approve(page)).ToBeVisibleAsync(Slow);

            // 7. Verify deadline DL-NEW-90 is shown with page reference 29 / 35-36
            await Assertions.Expect(page.GetByTestId("deadline-rule")).ToContainTextAsync("DL-NEW-90");
            await Assertions.Expect(page.GetByTestId("due-date")).ToHaveTextAsync(new Regex(@"26 ianG 2027"));

            // 8. All checks passing (case is Compliant, indicia covered by response)
            await Assertions.Expect(page.GetByTestId("indicia")).ToContainTextAsync("CRS-IND-ADDR");
            await Assertions.Expect(page.GetByTestId("indicia")).ToContainTextAsync("CRS-IND-PHONE");

            // 9. Approve the closing (proposal with proof)
            await Approve(page).ClickAsync();
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Closed", new() { Timeout = 15_000 });
            await Assertions.Expect(page.GetByTestId("proof")).ToBeVisibleAsync();
        }
        finally
        {
            // The demo date is shared by the whole app: put it back for the other tests.
            var reset = page.GetByRole(AriaRole.Button, new() { Name = "Reset the demo date" });
            if (await reset.CountAsync() > 0 && await reset.IsEnabledAsync()) await reset.ClickAsync();
        }
    }

    [Fact, Trait("Flow", "Maria")]
    public async Task A_viewer_cannot_apply_a_scenario_move_the_demo_date_or_use_action_buttons()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/changes/new");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo scenarios", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("demo-date")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Apply change" })).ToHaveCountAsync(0);
        foreach (var name in new[] { "+1 day", "+30 days", "+85 days", "Reset the demo date" })
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = name })).ToHaveCountAsync(0);

        // Viewer cannot see action buttons on an open case
        var maria = page.Locator("[data-scenario='maria']");
        var admin = await app.NewPageAsync();
        await app.GoAsync(admin, "/changes/new");
        await admin.Locator("[data-scenario='maria']").GetByRole(AriaRole.Button, new() { Name = "Apply change" }).ClickAsync();
        var result = admin.GetByTestId("change-result");
        var caseUrl = new Uri(await result.Locator("a").First.GetAttributeAsync("href") ?? "").PathAndQuery;

        await app.GoAsync(page, caseUrl);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Analyze the dossier" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Check the response" })).ToHaveCountAsync(0);
    }

    [Fact, Trait("Flow", "Maria")]
    public async Task The_case_screens_read_on_a_phone()
    {
        var page = await app.NewPageAsync(width: 390, height: 844);
        await app.GoAsync(page, "/cases");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cases", Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("demo-date")).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), "/cases scrolls sideways at 390px");

        await app.GoAsync(page, "/changes/new");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Demo scenarios", Exact = true })).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"), "/changes/new scrolls sideways at 390px");
    }
}
