using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HackathonStarter.E2E;

/// <summary>
/// The demo route through the case page, without the agent: a move to Germany opens a CRS case with the 90-day deadline,
/// the request goes out, the reminder day escalates it, a new self-certification closes it on SC-VALID, /audit has every step
/// and a viewer can act on none. Filler client CL-2004: no seeded change and no other test touches it (one shared database).
/// </summary>
[Collection("app")]
public class DemoRouteTests(AppFixture app)
{
    private const string ClientCode = "CL-2004";
    private const string GermanTin = "86095174223"; // synthetic, a German TIN format (golden case 4)
    private const string Germany = "Germany (DE)";

    [Fact]
    public async Task A_move_to_Germany_runs_from_the_change_to_a_closed_case_in_the_audit_log()
    {
        var page = await app.NewPageAsync(user: "admin", lang: "en");
        var viewer = await app.NewPageAsync(user: "viewer", lang: "en");
        var offsetAtStart = await DemoOffsetAsync(page);
        var passed = false;
        try
        {
            // 1. The change: code decides at once and opens the case.
            await app.GoAsync(page, "/changes/new");
            await page.GetByRole(AriaRole.Combobox, new() { Name = "Client" }).FillAsync(ClientCode);
            var option = page.GetByRole(AriaRole.Option).Filter(new() { HasText = ClientCode });
            var clientName = (await option.InnerTextAsync()).Split(" · ")[0].Trim();
            await option.ClickAsync();
            await PickGermanyAsync(page, page.Locator(".crs-form"));
            await page.GetByLabel("Street and number").FillAsync("Leopoldstraße 40");
            await page.GetByLabel("City").FillAsync("80802 München");
            await page.GetByRole(AriaRole.Button, new() { Name = "Record and apply the rules" }).ClickAsync();

            var verdict = page.Locator(".crs-verdict");
            await Assertions.Expect(verdict).ToContainTextAsync("Decision of the code");
            await Assertions.Expect(verdict.Locator(".crs-verdict-title")).ToHaveTextAsync("Clarification");
            var rule = verdict.Locator(".crs-rule.is-hit").Filter(new() { HasText = "CRS-IND-ADDR" });
            await Assertions.Expect(rule).ToContainTextAsync(Germany);
            await Assertions.Expect(rule).ToContainTextAsync("guide p. 29");
            var link = verdict.Locator("a[href^='/cases/CRS-']");
            await Assertions.Expect(link).ToHaveCountAsync(1);
            var code = (await link.GetAttributeAsync("href"))!.Split('/')[^1];
            Assert.Matches(@"^CRS-\d{4}-\d{4}$", code);
            await Assertions.Expect(link).ToHaveTextAsync($"Open case {code}");
            await link.ClickAsync();

            // 2. The case: client, indicium with rule and page, the new account's 90-day deadline, status Open.
            await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/cases/{code}$"));
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Case {code}" })).ToBeVisibleAsync();
            var meta = page.Locator(".crs-meta");
            await Assertions.Expect(meta).ToContainTextAsync($"{clientName} · {ClientCode}");
            await Assertions.Expect(meta.Locator($"a[href='/clients/{ClientCode}']")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator(".page-header .app-pill").Filter(new() { HasTextRegex = new Regex("^CRS$") })).ToBeVisibleAsync();
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Open");
            await Assertions.Expect(CurrentStage(page)).ToHaveTextAsync("Opened");

            var indicium = page.Locator(".crs-ind").Filter(new() { HasText = "CRS-IND-ADDR" });
            await Assertions.Expect(indicium).ToContainTextAsync(Germany);
            await Assertions.Expect(indicium).ToContainTextAsync("guide p. 29");
            await Assertions.Expect(indicium).ToContainTextAsync("Uncovered: starts the deadline");

            var clock = page.Locator(".crs-clock");
            await Assertions.Expect(clock).ToContainTextAsync("New account");
            await Assertions.Expect(clock).ToContainTextAsync("DL-NEW-90");
            await Assertions.Expect(clock).ToContainTextAsync("guide p. 20-21, 35-36");
            await Assertions.Expect(clock.GetByRole(AriaRole.Img, new() { Name = "Day 0 of 90" })).ToBeVisibleAsync();
            await Assertions.Expect(clock).ToContainTextAsync(new Regex(@"90\s+days left, until \d{1,2} \w+ \d{4}"));
            var due = Regex.Match(await clock.InnerTextAsync(), @"until (\d{1,2} \w+ \d{4})").Groups[1].Value;

            await ViewerReadsAsync(viewer, code, "Save the draft", "Viewers read the case; the operator and the tax department write the request.");
            await Assertions.Expect(viewer.GetByTestId("agent-analyze")).ToHaveCountAsync(0);

            // 3. The request: saved as a draft, then approved and sent through the checkpoint.
            var next = page.Locator(".crs-next");
            await Assertions.Expect(next).ToContainTextAsync("Write the request to the client");
            await Assertions.Expect(next).ToContainTextAsync($"Due by {due} (DL-NEW-90).");
            await Assertions.Expect(next.GetByRole(AriaRole.Textbox, new() { Name = "Letter", Exact = true }))
                .ToHaveValueAsync(new Regex($"Please send us by {Regex.Escape(LetterDate(due))}"));
            await next.GetByRole(AriaRole.Button, new() { Name = "Save the draft" }).ClickAsync();
            await Assertions.Expect(next).ToContainTextAsync("Approve the request to the client");
            await Assertions.Expect(next.Locator(".crs-letter")).ToContainTextAsync("Please send us by");
            await Assertions.Expect(LetterStatus(page, "Request")).ToHaveTextAsync("Draft");

            await ViewerReadsAsync(viewer, code, "Approve and send", "Only the operator approves what goes to the client.");
            await Assertions.Expect(viewer.GetByRole(AriaRole.Button, new() { Name = "Reject" })).ToBeDisabledAsync();

            await next.GetByRole(AriaRole.Button, new() { Name = "Approve and send" }).ClickAsync();
            var checkpoint = next.Locator(".crs-checkpoint");
            await Assertions.Expect(checkpoint).ToContainTextAsync("Send this request?");
            await Assertions.Expect(Fact(checkpoint, "To")).ToHaveTextAsync(clientName);
            await Assertions.Expect(Fact(checkpoint, "Deadline")).ToHaveTextAsync(due);
            await checkpoint.GetByRole(AriaRole.Button, new() { Name = "Confirm and send" }).ClickAsync();
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Awaiting client");
            await Assertions.Expect(CurrentStage(page)).ToHaveTextAsync("Awaiting client");
            await Assertions.Expect(next).ToContainTextAsync("Register the client's answer");
            await Assertions.Expect(LetterStatus(page, "Request")).ToHaveTextAsync("Sent");

            // 4. Day 85 of 90 from the Demo menu: reminded and escalated, the watcher's reminder waits. The deadline is not passed.
            var offset = await DemoOffsetAsync(page);
            await DemoActionAsync(page, "To the reminder day");
            await Assertions.Expect(page.Locator(".demo-date-offset")).ToHaveTextAsync($"+{offset + 85} d");
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Escalated");
            await Assertions.Expect(CurrentStage(page)).ToHaveTextAsync("Escalated");
            await Assertions.Expect(clock.GetByRole(AriaRole.Img, new() { Name = "Day 85 of 90" })).ToBeVisibleAsync();
            await Assertions.Expect(clock).ToContainTextAsync("5 days left");
            await Assertions.Expect(clock).ToContainTextAsync("Reminder drafted, escalated to the tax department");
            await Assertions.Expect(next).ToContainTextAsync("Approve the reminder to the client");
            await Assertions.Expect(next).ToContainTextAsync("Drafted by the deadline watcher on the reminder day.");
            await Assertions.Expect(next.GetByRole(AriaRole.Button, new() { Name = "Approve and send" })).ToBeEnabledAsync();
            await Assertions.Expect(LetterStatus(page, "Reminder")).ToHaveTextAsync("Draft");

            await ViewerReadsAsync(viewer, code, "Register the answer", "Viewers read the case; the operator and the tax department register answers.");
            await Assertions.Expect(viewer.GetByRole(AriaRole.Button, new() { Name = "Approve and send" })).ToBeDisabledAsync();

            // 5. The answer: the golden sample answer is Maria's only, so the Demo menu has none here and the operator types it.
            await page.Locator("[aria-label='Demo']").ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Menuitem).Filter(new() { HasText = "+1 day" })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Menuitem).Filter(new() { HasText = "Fill in the sample answer" })).ToHaveCountAsync(0);
            await page.Locator("[aria-label='Demo']").ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Menuitem)).ToHaveCountAsync(0);
            await PickGermanyAsync(page, next);
            await next.GetByRole(AriaRole.Textbox, new() { Name = "TIN", Exact = true }).FillAsync(GermanTin);
            await Assertions.Expect(next.Locator(".app-pill").Filter(new() { HasText = "format valid" })).ToBeVisibleAsync();
            await next.GetByRole(AriaRole.Button, new() { Name = "Register the answer" }).ClickAsync();
            await Assertions.Expect(Status(page)).ToHaveTextAsync("Response received");
            await Assertions.Expect(next).ToContainTextAsync("Approve the closing");
            await Assertions.Expect(next.Locator(".app-pill").Filter(new() { HasText = "Compliant" })).ToBeVisibleAsync();
            await Assertions.Expect(next).ToContainTextAsync("reportable for DE");

            await ViewerReadsAsync(viewer, code, "Approve the closing", "Only the operator approves the closing.");

            await next.GetByRole(AriaRole.Button, new() { Name = "Approve the closing" }).ClickAsync();
            var closing = next.Locator(".crs-checkpoint");
            await Assertions.Expect(closing).ToContainTextAsync("Close this case?");
            await Assertions.Expect(Fact(closing, "Answer")).ToHaveTextAsync("New self-certification");
            await closing.GetByRole(AriaRole.Button, new() { Name = "Confirm and close" }).ClickAsync();

            await Assertions.Expect(Status(page)).ToHaveTextAsync("Closed");
            await Assertions.Expect(next.Locator(".crs-verdict-title")).ToHaveTextAsync("Closed");
            await Assertions.Expect(next.Locator(".crs-verdict-stamp .crs-code")).ToHaveTextAsync("SC-VALID");
            await Assertions.Expect(next.Locator(".app-pill").Filter(new() { HasText = Germany })).ToBeVisibleAsync();
            await Assertions.Expect(next).ToContainTextAsync("Proof of confirmation");
            await Assertions.Expect(Fact(next, "Channel")).ToHaveTextAsync("Online · Electronic acceptance");
            await Assertions.Expect(Fact(next, "Signed by")).ToHaveTextAsync(clientName);
            await Assertions.Expect(Fact(next, "Approved by")).ToHaveTextAsync("Ana Popa");
            await Assertions.Expect(Fact(next, "SHA-256")).ToHaveTextAsync(new Regex("^[0-9a-f]{16}…$"));

            // 6. The audit log: the case's steps in order (newest first) and the hash chain intact.
            await app.GoAsync(page, "/audit");
            var rows = page.Locator("tbody tr").Filter(new() { Has = page.Locator($"a[href='/cases/{code}']") });
            await Assertions.Expect(rows.Locator("td[data-label='Kind']")).ToContainTextAsync(
                ["Case closed", "Answer registered", "Escalated", "Reminder due", "Request approved and sent", "Request drafted", "Case opened"]);
            var first = (await rows.Locator(".audit-seq").AllInnerTextsAsync()).Select(long.Parse).Min();
            await page.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
            var result = page.GetByText(new Regex(@"^Chain (intact: \d+ entries checked|broken at entry #\d+)$"));
            await Assertions.Expect(result).ToBeVisibleAsync();
            // Verify stops at the first break, so a tamper before this case would leave its entries unchecked: AuditTests
            // breaks the shared chain on purpose and has to run after this class.
            Assert.True((await result.InnerTextAsync()).StartsWith("Chain intact"),
                $"{await result.InnerTextAsync()}; this case's entries start at #{first}. A test that tampers must run after this one.");

            // 7. The viewer reads the closed case, with nothing left to act on.
            await app.GoAsync(viewer, $"/cases/{code}");
            await Assertions.Expect(Status(viewer)).ToHaveTextAsync("Closed");
            await Assertions.Expect(viewer.Locator(".crs-next .crs-verdict-stamp .crs-code")).ToHaveTextAsync("SC-VALID");
            await Assertions.Expect(viewer.GetByRole(AriaRole.Button, new() { Name = "Approve the closing" })).ToHaveCountAsync(0);
            await Assertions.Expect(viewer.GetByRole(AriaRole.Button, new() { Name = "Approve and send" })).ToHaveCountAsync(0);
            passed = true;
        }
        finally
        {
            // Back to the real date if it started there: CaseAgentTests answers with Maria's golden form, dated 21 Jan 2027.
            try { await RestoreRealDateAsync(page, offsetAtStart); }
            catch (Exception) when (!passed) { /* keep the test's own failure */ }
        }
    }

    private async Task ViewerReadsAsync(IPage viewer, string code, string button, string note)
    {
        await app.GoAsync(viewer, $"/cases/{code}");
        await Assertions.Expect(viewer.GetByRole(AriaRole.Heading, new() { Name = $"Case {code}" })).ToBeVisibleAsync();
        await Assertions.Expect(viewer.Locator(".crs-ind").Filter(new() { HasText = "CRS-IND-ADDR" })).ToBeVisibleAsync();
        await Assertions.Expect(viewer.GetByRole(AriaRole.Button, new() { Name = button })).ToBeDisabledAsync();
        await Assertions.Expect(viewer.GetByText(note)).ToBeVisibleAsync();
    }

    private static ILocator Status(IPage page) => page.Locator(".page-header .crs-swap .app-pill");

    private static ILocator CurrentStage(IPage page) =>
        page.GetByRole(AriaRole.List, new() { Name = "Case stages" }).Locator("[aria-current='step']");

    private static ILocator LetterStatus(IPage page, string kind) =>
        page.Locator(".crs-tl-head").Filter(new() { Has = page.Locator("b", new() { HasTextRegex = new Regex($"^{kind}$") }) }).Locator(".app-pill");

    // The value next to a label in a checkpoint or in the proof (dl.crs-kv).
    private static ILocator Fact(ILocator scope, string label) =>
        scope.Locator("dl.crs-kv dt", new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}$") }).Locator("xpath=following-sibling::dd[1]");

    // "27 Jan 2027" on the page is "27 January 2027" in the English letter (both en-GB).
    private static string LetterDate(string date)
    {
        var en = CultureInfo.GetCultureInfo("en-GB");
        return DateTime.ParseExact(date, "d MMM yyyy", en).ToString("d MMMM yyyy", en);
    }

    private static async Task PickGermanyAsync(IPage page, ILocator scope)
    {
        var country = scope.GetByRole(AriaRole.Combobox, new() { Name = "Country" });
        await country.FillAsync("DE");
        await page.GetByRole(AriaRole.Option, new() { Name = Germany, Exact = true }).ClickAsync();
        await Assertions.Expect(country).ToHaveValueAsync(Germany);
    }

    // The demo date's lead over the real date as the top bar shows it ("+85 d"; nothing at 0).
    private static async Task<int> DemoOffsetAsync(IPage page)
    {
        var chip = page.Locator(".demo-date-offset");
        return await chip.CountAsync() == 0 ? 0 : int.Parse(Regex.Match(await chip.InnerTextAsync(), @"\d+").Value);
    }

    private static async Task DemoActionAsync(IPage page, string label)
    {
        await page.Locator("[aria-label='Demo']").ClickAsync();
        await page.GetByRole(AriaRole.Menuitem).Filter(new() { HasText = label }).ClickAsync();
    }

    private async Task RestoreRealDateAsync(IPage page, int offsetAtStart)
    {
        await app.GoAsync(page, "/dashboard"); // a fresh page: no Demo menu left open by a failure
        if (offsetAtStart != 0 || await DemoOffsetAsync(page) == 0) return;
        await DemoActionAsync(page, "Back to the real date");
        await Assertions.Expect(page.Locator(".demo-date-offset")).ToHaveCountAsync(0);
    }
}
