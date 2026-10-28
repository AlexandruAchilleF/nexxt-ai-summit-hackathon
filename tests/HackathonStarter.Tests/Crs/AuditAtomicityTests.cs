using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.R2WorkflowHost;

namespace HackathonStarter.Tests.Crs;

public class AuditAtomicityTests
{
    public static IEnumerable<object[]> Failures()
    {
        foreach (var scenario in new[] { "open", "extend", "same", "no-case", "multiple" })
        foreach (var position in Enumerable.Range(1, scenario == "same" ? 2 : scenario == "no-case" ? 1 : scenario == "multiple" ? 4 : 3))
        foreach (var after in new[] { false, true })
            yield return [scenario, position, after];
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Failed_append_rolls_back_every_write_and_retry_is_complete(string scenario, int position, bool after)
    {
        var fault = new AppendFault();
        await using var host = await CreateAsync(s => s.AddScoped<IAuditLog>(sp =>
            new FailingAudit(ActivatorUtilities.CreateInstance<AuditLog>(sp), fault)));
        await host.SeedClientAsync("CL-0016", new DateOnly(2019, 1, 1));
        if (scenario is "extend" or "same") await host.MoveAsync("CL-0016", "ES");
        if (scenario == "multiple")
            host.Engine.OnEvaluate = (_, _, _) => ScriptedRuleEngine.Outcome([
                ScriptedRuleEngine.Hit("CRS-IND-ADDR", "ES"), ScriptedRuleEngine.Hit("CRS-IND-ADDR", "DE")]);
        var country = scenario == "same" ? "ES" : scenario == "no-case" ? "RO" : "DE";
        await using var before = await host.DbAsync();
        var changes = await before.ChangeEvents.CountAsync();
        var entries = await before.AuditEntries.CountAsync();
        var indicia = await before.Indicia.CountAsync();
        var addresses = await before.Addresses.CountAsync();
        fault.Remaining = position;
        fault.After = after;

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.MoveAsync("CL-0016", country));

        await using (var db = await host.DbAsync())
        {
            Assert.Equal(changes, await db.ChangeEvents.CountAsync());
            Assert.Equal(changes, await db.RuleEvaluations.CountAsync());
            Assert.Equal(entries, await db.AuditEntries.CountAsync());
            Assert.Equal(indicia, await db.Indicia.CountAsync());
            Assert.Equal(addresses, await db.Addresses.CountAsync());
            Assert.Equal(changes == 0 ? 0 : 1, await db.Cases.CountAsync());
        }

        var result = await host.MoveAsync("CL-0016", country);
        await using var saved = await host.DbAsync();
        Assert.Equal(changes + 1, await saved.ChangeEvents.CountAsync());
        Assert.Equal(changes + 1, await saved.RuleEvaluations.CountAsync());
        var audit = await saved.AuditEntries.OrderBy(e => e.Seq).ToListAsync();
        Assert.Equal(Enumerable.Range(1, audit.Count).Select(i => (long)i), audit.Select(e => e.Seq));
        Assert.True(AuditChain.Verify(audit).Ok);
        var expected = scenario == "same" ? new[] { AuditKinds.ChangeReceived, AuditKinds.RuleApplied }
            : scenario == "no-case" ? [AuditKinds.ChangeReceived]
            : new[] { AuditKinds.ChangeReceived, AuditKinds.RuleApplied,
                scenario is "open" or "multiple" ? AuditKinds.CaseOpened : AuditKinds.CaseIndiciumAdded };
        if (scenario == "multiple")
            expected = [AuditKinds.ChangeReceived, AuditKinds.RuleApplied, AuditKinds.RuleApplied, AuditKinds.CaseOpened];
        var added = audit.Skip(entries).ToList();
        Assert.Equal(expected, added.Select(e => e.Kind));
        Assert.All(added, e => Assert.Contains($"\"changeId\":{result.Change.Id}", e.PayloadJson));
        Assert.Equal(scenario == "no-case" ? 0 : 1, await saved.Cases.CountAsync());
        Assert.Equal(scenario is "extend" or "multiple" ? 2 : scenario == "no-case" ? 0 : 1, await saved.Indicia.CountAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Direct_case_open_or_extension_rolls_back_and_can_be_retried(bool extend, bool after)
    {
        var fault = new AppendFault();
        await using var host = await CreateAsync(s => s.AddScoped<IAuditLog>(sp =>
            new FailingAudit(ActivatorUtilities.CreateInstance<AuditLog>(sp), fault)));
        await host.SeedClientAsync("CL-0016", new DateOnly(2019, 1, 1));
        var change = await host.MoveAsync("CL-0016", extend ? "ES" : "RO");
        var outcome = ScriptedRuleEngine.Outcome([ScriptedRuleEngine.Hit("CRS-IND-ADDR", "DE")]);
        fault.Remaining = 1;
        fault.After = after;
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.As(Member).Cases.OpenAsync(change.Change, outcome));
        await using (var db = await host.DbAsync())
        {
            Assert.Equal(extend ? 1 : 0, await db.Cases.CountAsync());
            Assert.Equal(extend ? 1 : 0, await db.Indicia.CountAsync());
            Assert.Equal(extend ? 3 : 1, await db.AuditEntries.CountAsync());
        }
        await host.As(Member).Cases.OpenAsync(change.Change, outcome);
        await using var saved = await host.DbAsync();
        Assert.Single(await saved.Cases.ToListAsync());
        Assert.Equal(extend ? 2 : 1, await saved.Indicia.CountAsync());
        var entries = await saved.AuditEntries.OrderBy(e => e.Seq).ToListAsync();
        Assert.Equal(extend ? 4 : 2, entries.Count);
        Assert.True(AuditChain.Verify(entries).Ok);
    }

    [Fact]
    public async Task Concurrent_scopes_and_independent_appends_preserve_complete_ordered_histories()
    {
        await using var host = await CreateAsync(s => s.AddScoped<IAuditLog, AuditLog>());
        await host.SeedClientAsync("CL-0016", new DateOnly(2019, 1, 1));
        var sessions = Enumerable.Range(0, 8).Select(_ => host.As(Member)).ToArray();
        var scopes = Enumerable.Range(0, 8).Select(_ => host.Root.CreateScope()).ToArray();
        try
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writers = sessions.Select((session, i) => Task.Run(async () =>
            {
                await start.Task;
                await session.Changes.RecordAsync(new ChangeRequest("CL-0016", ChangeKind.AddressChanged, "online",
                    Address: new AddressSnapshot(AddressKind.Residence, "Synthetic", "City", i % 2 == 0 ? "ES" : "DE")));
            })).Concat(scopes.Select(scope => Task.Run(async () =>
            {
                await start.Task;
                await scope.ServiceProvider.GetRequiredService<IAuditLog>()
                    .AppendAsync("system", AuditKinds.TinChecked, new { country = "DE" });
            }))).ToArray();
            start.SetResult();
            await Task.WhenAll(writers);
            await using var db = await host.DbAsync();
            var entries = await db.AuditEntries.OrderBy(e => e.Seq).ToListAsync();
            Assert.Equal(Enumerable.Range(1, 26).Select(i => (long)i), entries.Select(e => e.Seq));
            Assert.True((await scopes[0].ServiceProvider.GetRequiredService<IAuditLog>().VerifyAsync()).Ok);
            Assert.Equal(8, await db.ChangeEvents.CountAsync());
            Assert.Equal(8, await db.RuleEvaluations.CountAsync());
            Assert.Single(await db.Cases.ToListAsync());
            Assert.Equal(2, await db.Indicia.CountAsync());
            Assert.Equal(8, entries.Count(e => e.Kind == AuditKinds.TinChecked));
            foreach (var change in await db.ChangeEvents.ToListAsync())
            {
                var history = entries.Where(e => e.PayloadJson.Contains($"\"changeId\":{change.Id},")).ToList();
                Assert.Equal(AuditKinds.ChangeReceived, history[0].Kind);
                Assert.Equal(AuditKinds.RuleApplied, history[1].Kind);
                Assert.InRange(history.Count, 2, 3);
                Assert.Equal(history[0].Seq + 1, history[1].Seq);
            }
        }
        finally { foreach (var scope in scopes) scope.Dispose(); }
    }

    private sealed class AppendFault
    {
        public int Remaining;
        public bool After;
    }

    private sealed class FailingAudit(IAuditLog inner, AppendFault fault) : IAuditLog
    {
        public async Task<AuditEntry> AppendAsync(string actor, string kind, object? payload = null, int? caseId = null)
        {
            var fail = --fault.Remaining == 0;
            if (fail && !fault.After) throw new InvalidOperationException("Injected before append");
            var entry = await inner.AppendAsync(actor, kind, payload, caseId);
            if (fail) throw new InvalidOperationException("Injected after append");
            return entry;
        }
        public Task<List<AuditEntry>> ListAsync(int? caseId = null, int take = 200) => inner.ListAsync(caseId, take);
        public Task<AuditVerification> VerifyAsync() => inner.VerifyAsync();
        public Task TamperAsync(long seq) => inner.TamperAsync(seq);
    }
}
