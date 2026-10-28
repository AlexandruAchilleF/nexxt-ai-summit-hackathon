using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs;

public sealed class AuditLogTests : IAsyncLifetime
{
    private static readonly DateTime Now = new DateTime(2026, 10, 28, 7, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567);
    private static readonly DateTime NowMs = new(2026, 10, 28, 7, 0, 0, 123, DateTimeKind.Utc);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "audit-tests-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<IServiceScope> _scopes = [];
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        _services = new ServiceCollection()
            .AddLogging()
            .AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(_dir, "audit.db")}"))
            .AddSingleton<ISimClock>(new FixedClock(Now))
            .AddScoped<CurrentUser>()
            .AddAudit()
            .BuildServiceProvider();
        await using var db = await NewContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var scope in _scopes) scope.Dispose();
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>A fresh scope (own DbContexts) as the given demo user, admin by default.</summary>
    private IAuditLog Audit(DemoUser? user = null)
    {
        var scope = _services.CreateScope();
        _scopes.Add(scope);
        scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(user ?? SiteConfig.Users[0]);
        return scope.ServiceProvider.GetRequiredService<IAuditLog>();
    }

    private Task<AppDbContext> NewContextAsync() =>
        _services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    [Fact]
    public void Hash_is_sha256_of_prev_hash_newline_canonical()
    {
        var e = new AuditEntry { Seq = 1, At = NowMs, Actor = "system", CaseId = null, Kind = "change.received", PayloadJson = "{}" };

        Assert.Equal("[1,\"2026-10-28T07:00:00.123Z\",\"system\",null,\"change.received\",\"{}\"]", AuditChain.Canonical(e));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("GENESIS\n" + AuditChain.Canonical(e)))),
            AuditChain.ComputeHash("GENESIS", e));
    }

    [Fact]
    public void Canonical_is_unambiguous()
    {
        var a = new AuditEntry { Seq = 1, At = NowMs, Actor = "1", Kind = "23", PayloadJson = "{}" };
        var b = new AuditEntry { Seq = 1, At = NowMs, Actor = "12", Kind = "3", PayloadJson = "{}" };

        Assert.NotEqual(AuditChain.ComputeHash(AuditChain.Genesis, a), AuditChain.ComputeHash(AuditChain.Genesis, b));
    }

    [Fact]
    public async Task Appends_are_chained_from_genesis_with_continuous_seq()
    {
        var log = Audit();
        var entries = new List<AuditEntry>();
        for (var i = 0; i < 3; i++) entries.Add(await log.AppendAsync(AuditActors.System, AuditKinds.ChangeReceived));

        Assert.Equal([1L, 2L, 3L], entries.Select(e => e.Seq));
        Assert.Equal(AuditChain.Genesis, entries[0].PrevHash);
        Assert.Equal(entries[0].Hash, entries[1].PrevHash);
        var verification = await Audit().VerifyAsync();
        Assert.True(verification.Ok);
        Assert.Equal(3, verification.Checked);
    }

    [Fact]
    public async Task At_is_truncated_to_milliseconds_and_survives_a_db_round_trip()
    {
        var entry = await Audit().AppendAsync(AuditActors.Agent, AuditKinds.TinChecked, new { country = "DE" }, caseId: 7);

        Assert.Equal(NowMs, entry.At);
        Assert.Equal(NowMs, Assert.Single(await Audit().ListAsync()).At);
        Assert.True((await Audit().VerifyAsync()).Ok);
    }

    [Fact]
    public async Task Payload_is_masked_before_it_is_saved()
    {
        var log = Audit();
        await log.AppendAsync(AuditActors.Agent, AuditKinds.SignalRecorded,
            new { iban = "RO49AAAA1B31007593840000", cnp = "2910608400319", note = "ok" });
        await log.AppendAsync(AuditActors.Agent, AuditKinds.SignalRecorded, new { cnp = 2910608400319L });

        var saved = await Audit().ListAsync();
        var strings = saved[1].PayloadJson;
        Assert.Contains("[IBAN-1]", strings);
        Assert.Contains("[CNP-1]", strings);
        Assert.DoesNotContain("RO49AAAA1B31007593840000", strings);
        Assert.DoesNotContain("2910608400319", strings);
        JsonDocument.Parse(strings).Dispose();
        Assert.Contains("[CNP-1]", saved[0].PayloadJson);
        JsonDocument.Parse(saved[0].PayloadJson).Dispose();
    }

    [Fact]
    public async Task Payload_stays_readable()
    {
        var entry = await Audit().AppendAsync(AuditActors.Agent, AuditKinds.EvidenceRecorded, new { quote = "Telefon: +49 89, Brașov" });

        Assert.Contains("+49 89, Brașov", entry.PayloadJson);
    }

    [Fact]
    public async Task Tamper_is_detected_at_exactly_the_modified_entry()
    {
        var log = Audit();
        for (var i = 0; i < 5; i++) await log.AppendAsync(AuditActors.System, AuditKinds.CaseReminded, new { step = i }, caseId: 1);

        await log.TamperAsync(3);

        var result = await Audit().VerifyAsync();
        Assert.False(result.Ok);
        Assert.Equal(3L, result.FirstBrokenSeq);
        Assert.NotEqual(result.Expected, result.Actual);
        Assert.Contains("edited", (await Audit().ListAsync()).Single(e => e.Seq == 3).PayloadJson);
    }

    [Fact]
    public async Task Tamper_requires_admin()
    {
        await Audit().AppendAsync(AuditActors.System, AuditKinds.CaseOpened);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Audit(SiteConfig.Users[1]).TamperAsync(1));
        Assert.True((await Audit().VerifyAsync()).Ok);
    }

    [Fact]
    public async Task Tamper_of_a_missing_entry_throws() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Audit().TamperAsync(99));

    [Fact]
    public async Task Deleted_entry_is_detected()
    {
        var log = Audit();
        for (var i = 0; i < 4; i++) await log.AppendAsync(AuditActors.System, AuditKinds.CaseReminded);
        await using (var db = await NewContextAsync())
            await db.AuditEntries.Where(e => e.Seq == 2).ExecuteDeleteAsync();

        var result = await Audit().VerifyAsync();

        Assert.Equal(3L, result.FirstBrokenSeq);
        Assert.Equal(("2", "3"), (result.Expected, result.Actual)); // the Seq that should follow, the one found
    }

    [Fact]
    public async Task Concurrent_appends_keep_seq_continuous()
    {
        var logs = Enumerable.Range(0, 20).Select(_ => Audit()).ToList();

        await Task.WhenAll(logs.Select((log, i) =>
            Task.Run(() => log.AppendAsync(AuditActors.Agent, AuditKinds.EvidenceRecorded, new { i }))));

        var seqs = (await Audit().ListAsync()).Select(e => e.Seq).Order();
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), seqs);
        Assert.True((await Audit().VerifyAsync()).Ok);
    }

    [Fact]
    public async Task List_by_case_returns_newest_first()
    {
        var log = Audit();
        await log.AppendAsync(AuditActors.System, AuditKinds.CaseOpened, caseId: 7);
        await log.AppendAsync(AuditActors.System, AuditKinds.CaseOpened, caseId: 8);
        await log.AppendAsync(AuditActors.System, AuditKinds.CaseClosed, caseId: 7);

        var entries = await log.ListAsync(7);

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].Seq > entries[1].Seq);
    }

    private sealed class FixedClock(DateTime now) : ISimClock
    {
        public DateTime UtcNow => now;
        public TimeSpan Offset => TimeSpan.Zero;
        public event Action? Changed { add { } remove { } }
        public void Advance(TimeSpan by) => throw new NotSupportedException();
        public void Reset() => throw new NotSupportedException();
    }
}
