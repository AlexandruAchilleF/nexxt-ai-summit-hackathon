using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HackathonStarter.Data;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace HackathonStarter.E2E;

/// <summary>Rendered controls and sizing only. The isolated visual fixture does not simulate legal decisions or a workflow.</summary>
[Collection("app")]
public class ResponseDocumentLayoutTests(AppFixture app, ITestOutputHelper output)
{
    private const string ShortName = "SPECIMEN-layout-evidence.txt";
    private static readonly string LongName = "SPECIMEN-" + new string('x', 70) + "-evidence.txt";

    [Theory]
    [InlineData("en")]
    [InlineData("ro")]
    public async Task Document_selection_stays_within_390px_in_every_rendered_state(string language)
    {
        var seed = await SeedAsync();
        IPage? page = null;
        try
        {
            page = await app.NewPageAsync(user: "admin", width: 390, height: 844, lang: language);
            await app.GoAsync(page, $"/cases/{seed.CaseCode}");
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", language);
            var form = page.Locator(".crs-next");
            await Assertions.Expect(form).ToBeVisibleAsync(new() { Timeout = 20_000 });
            var document = form.Locator(".crs-response-document").Filter(new()
            {
                Has = page.Locator("label").Filter(new() { HasTextRegex = new Regex("^Document$") }),
            });
            await Assertions.Expect(document).ToHaveCountAsync(1);
            var selected = document.Locator(".mud-select-input > .mud-input-slot:visible");
            await AssertSelectionAsync(selected, "");
            await FitsViewportAsync(page, language + "/empty");

            var evidence = form.GetByRole(AriaRole.Checkbox, new()
            {
                Name = language == "ro" ? "Document justificativ nou atașat (cont preexistent, p. 29)"
                    : "New documentary evidence attached (preexisting account, p. 29)", Exact = true,
            });
            await Assertions.Expect(evidence).Not.ToBeCheckedAsync();
            await evidence.CheckAsync();
            await Assertions.Expect(evidence).ToBeCheckedAsync();
            await Assertions.Expect(form.GetByText(language == "ro"
                ? "Selectează un document care aparține acestui client înainte de înregistrarea dovezii documentare."
                : "Select a document belonging to this client before registering documentary evidence.", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(form.GetByRole(AriaRole.Button, new()
            {
                Name = language == "ro" ? "Înregistrează răspunsul" : "Register the answer", Exact = true,
            })).ToBeDisabledAsync();
            await AssertSelectionAsync(selected, ""); // Checking the assertion must not select a document automatically.
            await FitsViewportAsync(page, language + "/checked-without-document");

            var previous = "";
            foreach (var filename in new[] { ShortName, LongName })
            {
                await selected.ClickAsync();
                // MudBlazor portals the options outside .crs-next; the originating control stays scoped to the form.
                var option = page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = filename });
                await Assertions.Expect(option).ToBeVisibleAsync();
                await AssertSelectionAsync(selected, previous);
                await FitsViewportAsync(page, language + "/open/" + filename);
                await option.ClickAsync();
                await Assertions.Expect(page.Locator(".mud-popover-open")).ToHaveCountAsync(0);
                await AssertSelectionAsync(selected, filename);
                await FitsViewportAsync(page, language + "/selected/" + filename);
                var screenshot = $"/tmp/opencode/response-document-layout-{language}-{(filename == ShortName ? "short" : "long")}-{seed.CaseCode}.png";
                await page.ScreenshotAsync(new() { Path = screenshot });
                output.WriteLine("SCREENSHOT " + screenshot);
                previous = filename;
            }

            await using var db = OpenDatabase();
            Assert.Equal(0, await db.ClientResponses.CountAsync(r => r.CaseId == seed.CaseId));
        }
        catch
        {
            if (page is not null)
            {
                try
                {
                    var path = $"/tmp/opencode/response-document-layout-{language}-390-{Guid.NewGuid():N}.png";
                    await page.ScreenshotAsync(new() { Path = path });
                    output.WriteLine("Failure screenshot: " + path);
                }
                catch (Exception capture) { output.WriteLine("Screenshot failed: " + capture.Message); }
            }
            throw;
        }
        finally
        {
            try { if (page is not null) await page.Context.CloseAsync(); }
            finally { await RemoveSeedAsync(seed); }
        }
    }

    private static async Task AssertSelectionAsync(ILocator selected, string filename)
    {
        await Assertions.Expect(selected).ToHaveCountAsync(1);
        await Assertions.Expect(selected).ToBeVisibleAsync();
        var expected = new Regex(filename.Length == 0 ? @"^\s*$" : Regex.Escape(filename));
        // Both MudSelect's input and its child-content display must retain the full filename, not remove text to fit.
        if (await selected.EvaluateAsync<bool>("el => el instanceof HTMLInputElement"))
            await Assertions.Expect(selected).ToHaveValueAsync(expected);
        else
            await Assertions.Expect(selected).ToHaveTextAsync(expected, new() { UseInnerText = true });
    }

    private async Task FitsViewportAsync(IPage page, string state)
    {
        const string measure = """
            () => JSON.stringify({viewport: innerWidth, html: document.documentElement.scrollWidth,
                body: document.body.scrollWidth, bodyOverflowX: getComputedStyle(document.body).overflowX})
            """;
        try
        {
            foreach (var root in new[] { "html", "body" })
                await Assertions.Expect(page.Locator(root)).Not.ToHaveCSSAsync("overflow-x", new Regex("^(hidden|clip)$"));
            await using var ready = await page.WaitForFunctionAsync("""
                () => window.innerWidth === 390
                    && document.documentElement.scrollWidth <= window.innerWidth + 1
                    && document.body.scrollWidth <= window.innerWidth + 1
                """, null, new() { Timeout = 5_000 });
            output.WriteLine($"LAYOUT {state} {await page.EvaluateAsync<string>(measure)}");
        }
        catch (Exception error)
        {
            var measured = await page.EvaluateAsync<string>(measure);
            throw new InvalidOperationException($"{state}: {measured}", error);
        }
    }

    private async Task<Seeded> SeedAsync()
    {
        await using var db = OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var token = Guid.NewGuid().ToString("N")[..12];
        var at = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
        var client = new Client
        {
            Code = "CL-" + token, FirstName = "SPECIMEN", LastName = "Layout", BirthDate = new(1990, 1, 1),
            Documents = [.. new[] { ShortName, LongName }.Select(filename => new ClientDocument
            {
                FileName = filename, Kind = DocumentKind.UtilityBill, MediaType = "text/plain", Pages = 1,
                Content = Encoding.UTF8.GetBytes("SPECIMEN: synthetic layout evidence only."),
                ShieldVerdict = ShieldVerdict.Clean, UploadedAt = at,
            })],
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        var rule = RuleCatalog.All.Single(r => r.Id == RuleIds.TinFormat);
        var item = new Case
        {
            Code = "LAY-" + token, ClientId = client.Id, ClientCode = client.Code, OpenedAt = at,
            Status = CaseStatus.AwaitingClient, Regime = Regime.Crs, IsPreexisting = true,
            AssignedTo = CaseService.DefaultAssignee,
            // Check-only metadata and no deadline keep this visual fixture outside the global clock watcher's work.
            DueAt = null, RemindAt = null, DeadlineRuleId = rule.Id, DeadlineGuidePage = rule.GuidePage,
            Indicia = [new() { RuleId = rule.Id, Regime = Regime.Crs, Jurisdiction = "DE", GuidePage = rule.GuidePage, AddedAt = at }],
        };
        db.Cases.Add(item);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(client.Id, client.Code, item.Id, item.Code);
    }

    private async Task RemoveSeedAsync(Seeded seed)
    {
        await using var db = OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var others = (await db.Clients.CountAsync(c => c.Id != seed.ClientId),
            await db.Cases.CountAsync(c => c.Id != seed.CaseId), await db.AuditEntries.CountAsync());
        var cases = await db.Cases.Where(c => c.Id == seed.CaseId && c.Code == seed.CaseCode && c.ClientId == seed.ClientId).ExecuteDeleteAsync();
        var clients = await db.Clients.Where(c => c.Id == seed.ClientId && c.Code == seed.ClientCode).ExecuteDeleteAsync();
        var remaining = (await db.Clients.CountAsync(), await db.Cases.CountAsync(), await db.AuditEntries.CountAsync());
        var children = (await db.ClientDocuments.CountAsync(d => d.ClientId == seed.ClientId),
            await db.Indicia.CountAsync(i => i.CaseId == seed.CaseId), await db.ClientResponses.CountAsync(r => r.CaseId == seed.CaseId));
        // Configured cascades remove only this case's children and this client's documents; never touch audit or clock.
        await transaction.CommitAsync();
        Assert.Equal(1, cases);
        Assert.Equal(1, clients);
        Assert.Equal(others, remaining);
        Assert.Equal((0, 0, 0), children);
        output.WriteLine($"CLEANUP {seed.CaseCode}: casesRemoved={cases}, clientsRemoved={clients}, documents/indicia/responses={children}, other clients/cases/audit unchanged={remaining}");
    }

    private AppDbContext OpenDatabase()
    {
        // AppFixture owns a disposable SQLite file. Do not fall back to the repository's app.db or create a database.
        var directory = (string?)(typeof(AppFixture).GetField("_dataDir", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(AppFixture), "_dataDir")).GetValue(app);
        if (string.IsNullOrWhiteSpace(directory) || !Path.GetFileName(directory).StartsWith("hs-e2e-", StringComparison.Ordinal))
            throw new InvalidOperationException("The isolated AppFixture data directory is unavailable.");
        var path = Path.Combine(directory, "e2e.db");
        if (!File.Exists(path)) throw new FileNotFoundException("AppFixture must initialize its disposable database first.", path);
        return new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Mode=ReadWrite;Foreign Keys=True;Pooling=False").Options);
    }

    private sealed record Seeded(int ClientId, string ClientCode, int CaseId, string CaseCode);
}
