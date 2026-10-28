using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Synthetic;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Clients;

/// <summary>
/// The demo's change history, recorded once at startup (after ClientSeed, while no change exists) through ChangeService
/// as <see cref="Actor"/>: the rule engine decides each change, CaseService opens the cases, and the audit chain starts
/// here, so /cases, the dashboard and /audit show real data. The golden cases the video does not enter live, plus three
/// ordinary filler changes. Never the clients of the live cases 4, 13, 5a-5c and 15 (CL-0001, CL-0013, CL-0005, CL-0015)
/// nor Victor (CL-0010, case 10, the shield: his statement stays as ClientSeed leaves it). Not case 12 either: its card ends the
/// Cluj residence with the c/o letter (variant A), but AddressChanged replaces only the addresses of its own kind, so through
/// ChangeService the residence stays current and the engine says NoCase.
///
/// Each change is received a fixed time before the start (the simulated now), and the deadline watcher's reminders and
/// expiries run at their own moments in between, as in a bank that has been running: the same start gives the same queue,
/// the same deadlines left and the same case codes. Cases that need a person (request approved, answer, closing) are not
/// seeded: only code acts here. Owner: R5.
/// </summary>
public static class ChangeHistorySeed
{
    public const string Actor = "system:seed";

    /// <param name="GoldenId">The golden case whose change and decision this is; null for a filler change or a case's document.</param>
    /// <param name="Ago">How long before the start the bank received the change.</param>
    /// <param name="DocumentFileName">DocumentUploaded: the client's seeded document (its id comes from the database).</param>
    public sealed record Step(string Label, string? GoldenId, TimeSpan Ago, ChangeRequest Request, string? DocumentFileName = null);

    public sealed record SeededChange(Step Step, ChangeResult Result);

    /// <summary>The clients whose cases the video enters live, plus Victor: the history never touches them.</summary>
    public static readonly IReadOnlySet<string> LiveClients = new HashSet<string> { "CL-0001", "CL-0005", "CL-0010", "CL-0013", "CL-0015" };

    /// <summary>In the order they are received (oldest first).</summary>
    public static IReadOnlyList<Step> Steps { get; } = Build();

    /// <summary>AppDbContext's call: the history only while no change exists (a new database); otherwise nothing.</summary>
    public static async Task<IReadOnlyList<SeededChange>> RunIfEmptyAsync(IServiceProvider services, DateTime now)
    {
        await using (var db = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
            if (await db.ChangeEvents.AnyAsync()) return [];
        return await RunAsync(services, now);
    }

    /// <summary>Records <see cref="Steps"/>; <paramref name="now"/> is the simulated now at startup (ISimClock).</summary>
    public static async Task<IReadOnlyList<SeededChange>> RunAsync(IServiceProvider services, DateTime now)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        // The real services on a clock set to each event's moment: ReceivedAt, deadlines and audit times are the event's.
        var clock = new SeedClock(now - Steps[0].Ago);
        var audit = ActivatorUtilities.CreateInstance<AuditLog>(sp, clock);
        var cases = ActivatorUtilities.CreateInstance<CaseService>(sp, clock, audit);
        var changes = ActivatorUtilities.CreateInstance<ChangeService>(sp, clock, cases, audit);
        var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var ticks = new SortedSet<DateTime>(); // the reminder and deadline moments of the cases opened so far
        var seeded = new List<SeededChange>();
        foreach (var step in Steps)
        {
            var at = now - step.Ago;
            await TickUntilAsync(cases, clock, ticks, at);
            clock.UtcNow = at;
            var request = step.DocumentFileName is { } file
                ? step.Request with { DocumentId = await DocumentIdAsync(factory, step.Request.ClientCode, file) }
                : step.Request;
            var result = await changes.RecordAsSystemAsync(request, Actor);
            if (result.CaseCode is { } code && await cases.GetAsync(code) is { } opened)
                ticks.UnionWith(new[] { opened.RemindAt, opened.DueAt }.OfType<DateTime>());
            seeded.Add(new(step, result));
        }
        await TickUntilAsync(cases, clock, ticks, now);
        return seeded;
    }

    /// <summary>What DeadlineWatcher would have done up to <paramref name="until"/>, each tick at its own moment.</summary>
    private static async Task TickUntilAsync(CaseService cases, SeedClock clock, SortedSet<DateTime> ticks, DateTime until)
    {
        while (ticks.Count > 0 && ticks.Min <= until)
        {
            var moment = ticks.Min;
            ticks.Remove(moment);
            clock.UtcNow = moment;
            await cases.TickAsync(moment);
        }
    }

    private static async Task<int> DocumentIdAsync(IDbContextFactory<AppDbContext> factory, string clientCode, string fileName)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ClientDocuments.Where(d => d.FileName == fileName && db.Clients.Any(c => c.Id == d.ClientId && c.Code == clientCode))
            .Select(d => d.Id).SingleAsync();
    }

    private static List<Step> Build()
    {
        var filler = new SyntheticData(ClientSeed.FillerSeed + 7);
        List<Step> steps =
        [
            // The card's 10 March: the same distance before the start as before the default change moment.
            Golden("17b", GoldenCases.ChangeMoment - GoldenCases.Get("17b").ReceivedAt!.Value),
            // Reminder day passed yesterday: escalated, 4 days left.
            Golden("16", TimeSpan.FromDays(86)),
            Golden("11", TimeSpan.FromDays(47)),
            new("Filler: moves within Romania", null, TimeSpan.FromDays(40), new("CL-2001", ChangeKind.AddressChanged, "branch",
                Address: new(AddressKind.Residence, "Calea Dorobanților 48, ap. 9", "400117 Cluj-Napoca", "RO"))),
            Golden("9", TimeSpan.FromDays(33)),
            new("Filler: a new Romanian phone replaces the old one", null, TimeSpan.FromDays(19),
                new("CL-2002", ChangeKind.PhoneChanged, "call center", Phone: new(filler.PhoneE164("RO"), "RO"))),
            Golden("18", TimeSpan.FromDays(12)),
            new("Filler: standing order to an account in Romania", null, TimeSpan.FromDays(8),
                new("CL-2003", ChangeKind.StandingOrderCreated, "online", StandingOrder: new(filler.Iban("BTRL"), "RO", true))),
            Golden("14", TimeSpan.FromDays(5)),
            // Case 3's form is signed on 26.10.2026: received the day before the start, never before it is signed.
            Golden("3", TimeSpan.FromDays(1)),
            // Cases 6 and 7, at the start (ClientSeed dates the PDFs then): the passport is uploaded and the shield scans it, no
            // case. The card's change is that upload; the second change records the US data a person reads in the passport
            // (the confirmation the card's After assumes, no quote yet), so the engine decides as on the card.
            Document("6", TimeSpan.Zero),
            new("Golden 6: place of birth from the passport", "6", TimeSpan.Zero,
                new("CL-0006", ChangeKind.PersonalDataChanged, "online", BirthCountry: "US",
                    Summary: "Locul nașterii din pașaport: Chicago, Illinois, S.U.A.")),
            Document("7", TimeSpan.Zero),
            new("Golden 7: US citizenship from the second passport", "7", TimeSpan.Zero,
                new("CL-0007", ChangeKind.PersonalDataChanged, "online", Citizenships: ["RO", "US"],
                    Summary: "Cetățenie SUA din al doilea pașaport (cetățenii: RO, US)")),
        ];
        return steps;
    }

    private static Step Golden(string id, TimeSpan ago)
    {
        var golden = GoldenCases.Get(id);
        return new($"Golden {id}: {golden.Title}", id, ago, golden.Change!);
    }

    private static Step Document(string id, TimeSpan ago)
    {
        var golden = GoldenCases.Get(id);
        return new($"Golden {id}: document uploaded", null, ago, golden.Change!, golden.Documents[0].FileName);
    }

    /// <summary>A clock that stands where the seed puts it; it never moves by itself.</summary>
    private sealed class SeedClock(DateTime start) : ISimClock
    {
        public DateTime UtcNow { get; set; } = start;
        public TimeSpan Offset => TimeSpan.Zero;
        public event Action? Changed { add { } remove { } }
        public void Advance(TimeSpan by) => throw new NotSupportedException("The seed clock is set, not advanced.");
        public void Reset() => throw new NotSupportedException("The seed clock is set, not reset.");
    }
}
