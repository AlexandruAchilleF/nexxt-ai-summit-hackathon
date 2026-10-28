using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using HackathonStarter.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>
/// The agent over the real CRS modules, wired like Program.cs: the demo seed (ClientSeed, the golden clients with their
/// documents), the rule engine, the shield, the case workflow and the hash-chained audit log, on a temp SQLite file. The demo
/// date is a SimClock over a fixed real time, the moment of the golden changes (2026-10-28T07:00Z). Synthetic data only.
/// </summary>
internal static class RealWorld
{
    public static DemoUser Ana => SiteConfig.Users.Single(u => u.Role == Roles.Admin);
    public static DemoUser Mihai => SiteConfig.Users.Single(u => u.Role == Roles.Member);
    public static DemoUser Vera => SiteConfig.Users.Single(u => u.Role == Roles.Viewer);

    /// <param name="history">Also the startup history of main (ChangeHistorySeed): other clients' cases, deadlines and audit entries.</param>
    public static async Task<(TestHost Host, SimClock Clock)> CreateAsync(
        IChatClient? model = null, DemoUser? user = null, IReadOnlyDictionary<string, IChatClient>? profiles = null, bool history = false,
        Action<AiOptions>? ai = null)
    {
        var clock = new SimClock(new ManualTime(GoldenCases.ChangeMoment));
        var host = await TestHost.CreateAsync(model, ai, user: user ?? Ana, profiles: profiles, configure: s =>
        {
            s.AddClients();
            s.AddChanges();
            s.AddRules();
            s.AddDeadlines();
            s.AddCases();
            s.AddShield();
            s.AddTin();
            s.AddAudit();
            s.AddSentinel();
            s.AddScoped<StructuredAi>();
            s.AddSingleton<ISimClock>(clock);
        });
        await using var db = await host.Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        db.Clients.AddRange(ClientSeed.Create(clock.UtcNow));
        await db.SaveChangesAsync();
        if (history) await ChangeHistorySeed.RunAsync(host.Root, clock.UtcNow);
        return (host, clock);
    }

    /// <summary>The change of a golden case, recorded as /changes/new does; an upload gets the id of its seeded document.</summary>
    public static async Task<ChangeResult> RecordGoldenChangeAsync(this TestHost host, string goldenCase)
    {
        var golden = GoldenCases.Get(goldenCase);
        var change = golden.Change!;
        if (change.Kind == ChangeKind.DocumentUploaded)
            change = change with { DocumentId = await host.DocumentIdAsync(golden.ClientCode, golden.Documents[0].FileName) };
        return await host.Get<IChangeService>().RecordAsync(change);
    }

    public static async Task<int> DocumentIdAsync(this TestHost host, string clientCode, string fileName) =>
        (await host.Get<IClientService>().ListDocumentsAsync(clientCode)).Single(d => d.FileName == fileName).Id;

    public static Task<AgentRun> DecideAsync(this TestHost host, AgentRun run, string tool) =>
        host.Get<AgentRunner>().DecideAsync(host.Agent(SentinelTools.AgentName), run.Id,
            run.Steps.Single(s => s is { Kind: StepKind.Approval, State: ApprovalState.Pending } && s.Tool == tool).Id, approved: true);

    public static Task<Case?> CaseAsync(this TestHost host, string code) => host.Get<ICaseService>().GetAsync(code);

    public static Task<AuditVerification> VerifyAuditAsync(this TestHost host) => host.Get<IAuditLog>().VerifyAsync();

    public static async Task<List<string>> AuditKindsAsync(this TestHost host) =>
        [.. (await host.Get<IAuditLog>().ListAsync(take: 500)).OrderBy(e => e.Seq).Select(e => e.Kind)];

    /// <summary>Every check of the run's report: none failed.</summary>
    public static void ChecksPass(this AgentRun run)
    {
        var report = run.Steps.Single(s => s.Kind == StepKind.Check);
        Assert.All(report.Checks!, c => Assert.NotEqual(Features.Checks.CheckOutcome.Fail, c.Outcome));
    }

    public static IEnumerable<string> AllText(this AgentRun run) =>
        run.Steps.SelectMany(s => new[] { s.Text, s.Input, s.Output, s.Summary, s.Error }).OfType<string>();
}

/// <summary>The rule engine's verdicts as plain strings for the asserts, e.g. "CRS-IND-ADDR:DE".</summary>
internal static class Hits
{
    public static string[] Of(IEnumerable<Indicium> indicia) => [.. indicia.Select(i => $"{i.RuleId}:{i.Jurisdiction}")];
    public static string[] Of(IEnumerable<RuleHit> hits) => [.. hits.Select(h => $"{h.RuleId}:{h.Jurisdiction}")];
}
