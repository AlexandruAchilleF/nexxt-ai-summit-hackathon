using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace HackathonStarter.Features.Sentinel.Demo;

/// <summary>
/// The demo inbox: the bank-side events of the live scenario, with the data of the golden case
/// (Evaluation/GoldenCases). The change goes through IChangeService, so the rules, the deadline and the audit log run as in
/// production. The client's answer is the one write outside it: IChangeService has no way to add a document yet, so the PDF
/// is stored as a ClientDocument here and then recorded as a DocumentUploaded change (the shield scans it). Editors only.
/// </summary>
public sealed class DemoScenarios(IDbContextFactory<AppDbContext> dbFactory, IChangeService changes, ICaseService cases,
                                  CurrentUser currentUser, ISimClock clock) : IDemoScenarios
{
    // Golden case 4: one AddressChanged event with the new residence and the German phone that replaces the Romanian one.
    private static readonly GoldenCase Maria = GoldenCases.Get("4");

    private static readonly DemoScenario[] Scenarios =
    [
        new("maria", Maria.ClientCode, Maria.Id,
            "Maria moves to Munich", "Maria se mută la München",
            "New residence in Munich and a German phone that replaces the Romanian one, online. New account: 90 days.",
            "Reședință nouă în München și un telefon german care îl înlocuiește pe cel românesc, online. Cont nou: 90 de zile.",
            HasResponse: true),
    ];

    private static GoldenCase Golden(DemoScenario scenario) => GoldenCases.Get(scenario.GoldenCase);

    public IReadOnlyList<DemoScenario> List() => Scenarios;

    public async Task<ChangeResult> ApplyAsync(string scenarioId)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        var scenario = Find(scenarioId);
        var change = Golden(scenario).Change ?? throw new InvalidOperationException($"Golden case {scenario.GoldenCase} has no change.");

        // A second click would record the same move again: refuse it while the new address is already the current one.
        if (change.Address is { } address)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var applied = await db.Addresses.AnyAsync(a => a.IsCurrent && a.Kind == address.Kind && a.Line == address.Line
                && a.Country == address.Country && db.Clients.Any(c => c.Id == a.ClientId && c.Code == change.ClientCode));
            if (applied) throw new InvalidOperationException($"The scenario {scenario.Id} is already applied: {address.Line} is the current address.");
        }
        return await changes.RecordAsync(change);
    }

    public async Task<int> DeliverResponseAsync(string caseCode)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        var c = await cases.GetAsync(caseCode) ?? throw new InvalidOperationException($"No case {caseCode}.");
        if (!c.IsOpen) throw new InvalidOperationException($"Case {c.Code} is {c.Status}: it takes no answer any more.");
        var answer = Scenarios.Where(s => s.ClientCode == c.ClientCode && s.HasResponse).Select(s => Golden(s).Response?.Document).FirstOrDefault()
                     ?? throw new InvalidOperationException($"No demo answer is scripted for client {c.ClientCode}.");

        int documentId;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var existing = await db.ClientDocuments.Where(d => d.ClientId == c.ClientId && d.FileName == answer.FileName).Select(d => (int?)d.Id).FirstOrDefaultAsync();
            if (existing is int id)
            {
                // Delivered already: the same document, no second upload; its change too, unless recording it failed last time.
                var marker = $"\"documentId\":{id}";
                if (await db.ChangeEvents.AnyAsync(e => e.ClientId == c.ClientId && e.Kind == ChangeKind.DocumentUploaded && e.NewValueJson != null && e.NewValueJson.Contains(marker)))
                    return id;
                await RecordUploadAsync(c, id, answer.FileName);
                return id;
            }

            var pdf = SyntheticPdfs.Get(answer.FileName).Bytes;
            using var parsed = PdfDocument.Open(pdf);
            var document = new ClientDocument
            {
                ClientId = c.ClientId, Kind = answer.Kind, FileName = answer.FileName, Content = pdf, Pages = parsed.NumberOfPages, UploadedAt = clock.UtcNow,
            };
            db.ClientDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        await RecordUploadAsync(c, documentId, answer.FileName);
        return documentId;
    }

    private Task<ChangeResult> RecordUploadAsync(Case c, int documentId, string fileName) =>
        changes.RecordAsync(new ChangeRequest(c.ClientCode, ChangeKind.DocumentUploaded, "online", DocumentId: documentId,
            CaseCode: c.Code, Summary: $"Răspuns primit: {fileName}"));

    private static DemoScenario Find(string scenarioId) =>
        Scenarios.FirstOrDefault(s => string.Equals(s.Id, scenarioId?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown demo scenario \"{scenarioId}\". Known: {string.Join(", ", Scenarios.Select(s => s.Id))}.", nameof(scenarioId));
}
