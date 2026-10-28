using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Sentinel.Demo;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Sentinel.Interim;

/// <summary>
/// The demo inbox (docs/adr/0001): the bank-side events of the live scenario, with the exact data of the golden case.
/// Everything goes through IChangeService, so the rules, the deadline and the audit log run as in production. Editors only.
/// </summary>
public sealed class DemoScenarios(IDbContextFactory<AppDbContext> dbFactory, IChangeService changes, ICaseService cases,
                                  CurrentUser currentUser, ISimClock clock) : IDemoScenarios
{
    private sealed record Script(DemoScenario Scenario, ChangeRequest Change, string? ResponseFile, DocumentKind ResponseKind);

    // Golden case 4: one AddressChanged event with the new residence and the German phone that replaces the Romanian one.
    private static readonly Script[] Scripts =
    [
        new(new DemoScenario("maria", MariaSeed.ClientCode, "4",
                "Maria moves to Munich", "Maria se mută la München",
                "New residence in Munich and a German phone that replaces the Romanian one, online. New account: 90 days.",
                "Reședință nouă în München și un telefon german care îl înlocuiește pe cel românesc, online. Cont nou: 90 de zile.",
                HasResponse: true),
            new ChangeRequest(MariaSeed.ClientCode, ChangeKind.AddressChanged, "online",
                Address: new AddressSnapshot(AddressKind.Residence, "Leopoldstraße 12", "80802 München", "DE"),
                Phone: new PhoneSnapshot("+498945217730", "DE"),
                Summary: "Adresă nouă în München și telefon DE, care înlocuiește telefonul RO"),
            SyntheticPdfs.SelfCertMaria, DocumentKind.SelfCertForm),
    ];

    public IReadOnlyList<DemoScenario> List() => [.. Scripts.Select(s => s.Scenario)];

    public async Task<ChangeResult> ApplyAsync(string scenarioId)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        var script = Find(scenarioId);

        // A second click would record the same move again: refuse it while the new address is already the current one.
        if (script.Change.Address is { } address)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var applied = await db.Addresses.AnyAsync(a => a.IsCurrent && a.Kind == address.Kind && a.Line == address.Line
                && a.Country == address.Country && db.Clients.Any(c => c.Id == a.ClientId && c.Code == script.Change.ClientCode));
            if (applied) throw new InvalidOperationException($"The scenario {script.Scenario.Id} is already applied: {address.Line} is the current address.");
        }
        return await changes.RecordAsync(script.Change);
    }

    public async Task<int> DeliverResponseAsync(string caseCode)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        var c = await cases.GetAsync(caseCode) ?? throw new InvalidOperationException($"No case {caseCode}.");
        if (!c.IsOpen) throw new InvalidOperationException($"Case {c.Code} is {c.Status}: it takes no answer any more.");
        var script = Scripts.FirstOrDefault(s => s.Scenario.ClientCode == c.ClientCode && s.ResponseFile is not null)
                     ?? throw new InvalidOperationException($"No demo answer is scripted for client {c.ClientCode}.");
        var fileName = script.ResponseFile!;

        int documentId;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var existing = await db.ClientDocuments.Where(d => d.ClientId == c.ClientId && d.FileName == fileName).Select(d => (int?)d.Id).FirstOrDefaultAsync();
            if (existing is int id) return id; // delivered already: the same document, no second upload

            var pdf = SyntheticPdfs.Get(fileName).Bytes;
            var document = new ClientDocument
            {
                ClientId = c.ClientId, Kind = script.ResponseKind, FileName = fileName, Content = pdf,
                Pages = MariaSeed.PageCount(pdf), UploadedAt = clock.UtcNow,
            };
            db.ClientDocuments.Add(document);
            await db.SaveChangesAsync();
            documentId = document.Id;
        }

        await changes.RecordAsync(new ChangeRequest(c.ClientCode, ChangeKind.DocumentUploaded, "online", DocumentId: documentId,
            CaseCode: c.Code, Summary: $"Răspuns primit: {fileName}"));
        return documentId;
    }

    private static Script Find(string scenarioId) =>
        Scripts.FirstOrDefault(s => string.Equals(s.Scenario.Id, scenarioId?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown demo scenario \"{scenarioId}\". Known: {string.Join(", ", Scripts.Select(s => s.Scenario.Id))}.", nameof(scenarioId));
}
