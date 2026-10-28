using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// The startup history (ChangeHistorySeed): every change goes through ChangeService with the real engine, cases and audit
/// chain, as "system:seed" with no signed-in user; the live cases' clients stay untouched; the same start gives the same queue.
/// </summary>
public class SeedHistoryTests
{
    // The demo day (GOLDEN-CASES.md, 1): the startup moment of these tests.
    private static readonly DateTime Now = GoldenCases.ChangeMoment;

    [Fact]
    public async Task The_audit_chain_starts_with_the_history_and_verifies_intact()
    {
        await using var host = await SeededHost.CreateAsync(Now);
        var audit = host.Get<IAuditLog>();

        var entries = await audit.ListAsync(take: 1000);
        var verification = await audit.VerifyAsync();
        Assert.True(verification.Ok, $"broken at #{verification.FirstBrokenSeq}");
        Assert.Equal(entries.Count, verification.Checked);
        // Per change: change.received and one rule.applied per indicium and cleared rule; one case.opened per case; the
        // reminder day of case 16 (reminded, escalated). 38 entries on the engine of 28.10.2026.
        var cases = host.Seeded.Count(s => s.Result.CaseCode is not null);
        Assert.Equal(7, cases);
        Assert.Equal(host.Seeded.Sum(s => 1 + s.Result.Outcome.Indicia.Count + s.Result.Outcome.Cleared.Count) + cases + 2, entries.Count);

        // One change.received per step, all by the seed; the watcher's reminder is the system's, as at runtime.
        Assert.Equal(ChangeHistorySeed.Steps.Count, entries.Count(e => e.Kind == AuditKinds.ChangeReceived));
        Assert.All(entries.Where(e => e.Kind is AuditKinds.ChangeReceived or AuditKinds.RuleApplied or AuditKinds.CaseOpened),
            e => Assert.Equal(ChangeHistorySeed.Actor, e.Actor));
        Assert.Equal([AuditKinds.CaseReminded, AuditKinds.CaseEscalated],
            entries.Where(e => e.Actor == CaseService.SystemActor).OrderBy(e => e.Seq).Select(e => e.Kind));
        Assert.DoesNotContain(entries, e => e.Actor.StartsWith("user:", StringComparison.Ordinal));
        // Times are the events' own, in order, all before the start.
        var bySeq = entries.OrderBy(e => e.Seq).ToList();
        Assert.Equal(bySeq.Select(e => e.At).Order(), bySeq.Select(e => e.At));
        Assert.True(bySeq[^1].At <= Now);

        await using var db = await host.DbAsync();
        Assert.Equal(ChangeHistorySeed.Steps.Count, await db.ChangeEvents.CountAsync());
        Assert.Equal(ChangeHistorySeed.Steps.Count, await db.RuleEvaluations.CountAsync());
    }

    [Fact]
    public async Task The_clients_of_the_live_cases_and_Victor_have_no_change_and_no_case()
    {
        await using var host = await SeededHost.CreateAsync(Now);
        var clients = host.Get<IClientService>();
        await using var db = await host.DbAsync();

        Assert.DoesNotContain(ChangeHistorySeed.Steps, s => ChangeHistorySeed.LiveClients.Contains(s.Request.ClientCode));
        foreach (var code in ChangeHistorySeed.LiveClients)
        {
            Assert.False(await db.ChangeEvents.AnyAsync(e => db.Clients.Any(c => c.Id == e.ClientId && c.Code == code)), code);
            Assert.False(await db.Cases.AnyAsync(c => c.ClientCode == code), code);
        }
        // Cases 4, 13, 5a-5c and 15 start from their cards, exactly as without the history.
        foreach (var id in new[] { "4", "13", "5a", "5b", "5c", "15" })
        {
            var golden = GoldenCases.Get(id);
            var client = await clients.GetAsync(golden.ClientCode);
            Assert.Equivalent(golden.Before, await clients.SnapshotAsync(client!.Id), strict: true);
        }
        // Victor's statement stays as ClientSeed leaves it: not scanned until its DocumentUploaded change (case 10).
        Assert.Equal(ShieldVerdict.NotScanned,
            await db.ClientDocuments.Where(d => db.Clients.Any(c => c.Id == d.ClientId && c.Code == "CL-0010")).Select(d => d.ShieldVerdict).SingleAsync());
    }

    [Fact]
    public async Task Each_golden_step_gets_the_decision_indicia_and_deadline_rule_of_its_card()
    {
        await using var host = await SeededHost.CreateAsync(Now);
        var cases = host.Get<ICaseService>();

        foreach (var seeded in host.Seeded.Where(s => s.Step.GoldenId is not null))
        {
            var golden = GoldenCases.Get(seeded.Step.GoldenId!);
            var outcome = seeded.Result.Outcome;
            Assert.True(golden.ExpectedDecision == outcome.Decision, $"{golden.Id}: {outcome.Decision}, card {golden.ExpectedDecision}");
            // The cleared rules are the golden engine tests' (GoldenCases); here the decision, the indicia and the case.
            Assert.Equal(golden.ExpectedHits.Order(StringComparer.Ordinal), outcome.Indicia.Select(h => $"{h.RuleId}:{h.Jurisdiction}").Order(StringComparer.Ordinal));

            var opensCase = golden.ExpectedDecision is Decision.Clarification or Decision.FatcaAlert;
            Assert.Equal(opensCase, seeded.Result.CaseCode is not null);
            if (!opensCase) continue;
            var item = await cases.GetAsync(seeded.Result.CaseCode!);
            Assert.Equal(golden.ExpectedDeadline!.DueAt is null, item!.DueAt is null);
            if (golden.ExpectedDeadlineRule is { } rule) Assert.Equal(rule, item.DeadlineRuleId);
            Assert.Equal(golden.ExpectedDeadline.Preexisting, item.IsPreexisting);
        }
    }

    [Fact]
    public async Task The_queue_has_open_cases_in_different_states_and_the_others_an_explained_no_case()
    {
        await using var host = await SeededHost.CreateAsync(Now);
        var queue = await host.Get<ICaseService>().QueueAsync();

        // In queue order: the deadlines first, the nearest first; the FATCA alerts (no deadline in the guide, p. 38) last.
        Assert.Equal(
        [
            ("CL-0016", CaseStatus.Escalated, (int?)4), ("CL-0011", CaseStatus.Open, 43), ("CL-0017", CaseStatus.Open, 64),
            ("CL-0018", CaseStatus.Open, 78),
            ("CL-0014", CaseStatus.Open, null), ("CL-0006", CaseStatus.Open, null), ("CL-0007", CaseStatus.Open, null),
        ], queue.Select(c => (c.ClientCode, c.Status, c.DueAt is { } due ? (int?)Math.Floor((due - Now).TotalDays) : null)));
        Assert.Equal(Enumerable.Range(1, 7).Select(n => $"CRS-2026-{n:0000}").Order(StringComparer.Ordinal), queue.Select(c => c.Code).Order(StringComparer.Ordinal));

        // No case: the filler changes stay in Romania, cases 3 and 9 are covered by the self-certification; each says why.
        var noCase = host.Seeded.Where(s => s.Result.CaseCode is null).ToList();
        Assert.Equal(7, noCase.Count);
        Assert.All(noCase, s => Assert.False(string.IsNullOrWhiteSpace(s.Result.Outcome.ExplanationRo), s.Step.Label));
        Assert.All(noCase.Where(s => s.Step.GoldenId is null && s.Step.Request.Kind != ChangeKind.DocumentUploaded), s =>
        {
            Assert.Equal(Decision.NoCase, s.Result.Outcome.Decision);
            Assert.Contains(s.Result.Outcome.Cleared, h => h.RuleId == "GEN-DOMESTIC");
        });
        // The passports are scanned by the shield when uploaded: clean, no case.
        Assert.All(noCase.Where(s => s.Step.Request.Kind == ChangeKind.DocumentUploaded), s => Assert.Equal(Decision.NoCase, s.Result.Outcome.Decision));
    }

    [Fact]
    public async Task The_same_start_gives_the_same_history_and_the_same_chain()
    {
        await using var first = await SeededHost.CreateAsync(Now);
        await using var second = await SeededHost.CreateAsync(Now);

        Assert.Equal(await QueueAsync(first), await QueueAsync(second));
        Assert.Equal(await ChainAsync(first), await ChainAsync(second));

        static async Task<List<(string, CaseStatus, DateTime?, DateTime)>> QueueAsync(SeededHost host) =>
            [.. (await host.Get<ICaseService>().QueueAsync(new CaseQueueFilter(OpenOnly: false))).Select(c => (c.Code, c.Status, c.DueAt, c.OpenedAt))];
        static async Task<List<string>> ChainAsync(SeededHost host) =>
            [.. (await host.Get<IAuditLog>().ListAsync(take: 1000)).Select(e => e.Hash)];
    }

    [Fact]
    public async Task A_second_start_on_the_same_database_adds_nothing()
    {
        await using var host = await SeededHost.CreateAsync(Now);
        await using (var db = await host.DbAsync())
            Assert.True(await db.ChangeEvents.AnyAsync()); // AppDbContext runs the history only while this is false

        Assert.Equal(ChangeHistorySeed.Steps.Count, (await host.Get<IChangeService>().ListAsync(take: 500)).Count);
    }

    private sealed class SeededHost : IAsyncDisposable
    {
        private readonly string _file = Path.Combine(Path.GetTempPath(), $"crs-history-{Guid.NewGuid():N}.db");
        private readonly ServiceProvider _root;
        private readonly AsyncServiceScope _scope;

        private SeededHost()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_file}"));
            services.AddScoped<CurrentUser>();
            // As at startup: no circuit, so no user. The history must never ask for one.
            services.AddScoped<AuthenticationStateProvider, NoUserAtStartup>();
            services.AddClients();
            services.AddChanges();
            services.AddRules();
            services.AddDeadlines();
            services.AddCases();
            services.AddShield();
            services.AddTin();
            services.AddAudit();
            _root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _root.CreateAsyncScope();
        }

        public IReadOnlyList<ChangeHistorySeed.SeededChange> Seeded { get; private set; } = [];

        public static async Task<SeededHost> CreateAsync(DateTime now)
        {
            var host = new SeededHost();
            await using (var db = await host.DbAsync())
            {
                await db.Database.EnsureCreatedAsync();
                db.Clients.AddRange(ClientSeed.Create(now));
                await db.SaveChangesAsync();
            }
            host.Seeded = await ChangeHistorySeed.RunAsync(host._root, now);
            return host;
        }

        public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

        public Task<AppDbContext> DbAsync() => _root.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _root.DisposeAsync();
            SqliteConnection.ClearAllPools();
            try { File.Delete(_file); } catch { /* best effort */ }
        }
    }

    private sealed class NoUserAtStartup : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            throw new InvalidOperationException("No user at startup: the history must run as the system.");
    }
}
