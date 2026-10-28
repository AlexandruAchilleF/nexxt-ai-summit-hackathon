using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>Golden case 4 on screen with the agent (mock, no AI key): the two buttons, the approvals and the client's answer from the Demo menu.</summary>
[Collection("app")]
public class CaseAgentTests(AppFixture app)
{
    [Fact]
    public async Task Maria_closes_through_the_agent_panel()
    {
        var page = await app.NewPageAsync();
        await app.GoAsync(page, "/changes/new?scenario=4");
        await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();
        var link = page.Locator("a[href^='/cases/CRS-']").First;
        await Assertions.Expect(link).ToBeVisibleAsync();
        var href = await link.GetAttributeAsync("href");
        await app.GoAsync(page, href!);

        var panel = page.Locator(".case-agent");
        // Run 1: reads the documents and drafts the request; sending waits for the approval.
        await panel.GetByTestId("agent-analyze").ClickAsync();
        await Assertions.Expect(panel.GetByText("2 / 2")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(panel.GetByTestId("agent-verify")).ToBeDisabledAsync();
        await panel.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Awaiting client").First).ToBeVisibleAsync();

        // Day 85 (Demo menu): reminder and escalation. Then the client's new self-certification, signed that day, arrives;
        // run 2 checks it and proposes closing.
        await page.Locator(".demo-date").ClickAsync();
        await page.GetByText("To the reminder day").ClickAsync();
        await Assertions.Expect(page.GetByText("Escalated").First).ToBeVisibleAsync();
        await page.Locator(".demo-date").ClickAsync();
        await page.GetByText("The client answers").ClickAsync();
        await Assertions.Expect(page.GetByText(new Regex("The client's answer is in the file"))).ToBeVisibleAsync();
        await panel.GetByTestId("agent-verify").ClickAsync();
        await Assertions.Expect(panel.GetByText("format valid").First).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await panel.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Approved and executed").Last).ToBeVisibleAsync();
        await Assertions.Expect(panel.GetByTestId("agent-analyze")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Viewer_sees_no_agent_buttons()
    {
        var page = await app.NewPageAsync(user: "viewer");
        await app.GoAsync(page, "/cases");
        var link = page.Locator("a[href^='/cases/CRS-']").First;
        await app.GoAsync(page, (await link.GetAttributeAsync("href"))!);

        await Assertions.Expect(page.Locator(".case-agent")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("agent-analyze")).ToHaveCountAsync(0);
    }
}
