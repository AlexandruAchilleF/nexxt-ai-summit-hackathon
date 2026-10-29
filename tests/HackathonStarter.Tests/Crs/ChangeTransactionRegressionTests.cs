using System.Text.Json;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static HackathonStarter.Tests.Crs.R2WorkflowHost;

namespace HackathonStarter.Tests.Crs;

/// <summary>Provider failures must roll back the whole change, not just the failed case statement or audit append.</summary>
public class ChangeTransactionRegressionTests
{
    private const string ClientCode = "CL-0051";
    private const string OtherClientCode = "CL-0052";

    private sealed record WriteFault(string Sql, string Message, Type EntityType);

    private static readonly WriteFault CaseInsert = new("""
        CREATE TRIGGER "FailChangeWrite" BEFORE INSERT ON "Cases"
        BEGIN SELECT RAISE(ABORT, 'synthetic case insert failure'); END;
        """, "synthetic case insert failure", typeof(Case));

    private static readonly WriteFault IndiciumInsert = new("""
        CREATE TRIGGER "FailChangeWrite" BEFORE INSERT ON "Indicia"
        BEGIN SELECT RAISE(ABORT, 'synthetic indicium insert failure'); END;
        """, "synthetic indicium insert failure", typeof(Indicium));

    private static readonly WriteFault CaseMetadataUpdate = new("""
        CREATE TRIGGER "FailChangeWrite"
        BEFORE UPDATE OF "Regime", "DueAt", "RemindAt", "IsPreexisting", "DeadlineRuleId", "DeadlineGuidePage" ON "Cases"
        BEGIN SELECT RAISE(ABORT, 'synthetic case metadata update failure'); END;
        """, "synthetic case metadata update failure", typeof(Case));

    [Theory]
    [InlineData(ChangeKind.AddressChanged)]
    [InlineData(ChangeKind.PersonalDataChanged)]
    [InlineData(ChangeKind.SelfCertSubmitted)]
    public async Task Case_insert_failure_rolls_back_client_values_self_certification_history_and_every_workflow_write(ChangeKind kind)
    {
        await using var database = await TransactionDatabase.CreateAsync();
        await using var host = await CreateAsync(database.Configure);
        var client = await SeedHistoryAsync(host);
        var request = kind switch
        {
            ChangeKind.AddressChanged => MoveWithPhone(),
            ChangeKind.PersonalDataChanged => new ChangeRequest(ClientCode, kind, "online", BirthCountry: "US", Citizenships: ["RO", "US"]),
            ChangeKind.SelfCertSubmitted => new ChangeRequest(ClientCode, kind, "online",
                SelfCertification: new SelfCertificationSnapshot(host.Clock.UtcNow, [new("RO", client.Cnp), new("US", null)], DeclaresUsPerson: true)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        string[] rules = kind switch
        {
            ChangeKind.AddressChanged => ["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"],
            ChangeKind.PersonalDataChanged => ["FATCA-IND-CIT:US", "FATCA-IND-POB:US"],
            _ => ["FATCA-TIN:US"],
        };

        var retry = await RejectAndRetryAsync(host, request, CaseInsert, AuditKinds.CaseOpened, rules);

        var item = Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(Assert.IsType<string>(retry.CaseCode)));
        Assert.Equal(CaseStatus.Open, item.Status);
        Assert.Equal(retry.Change.Id, item.TriggerChangeId);
        Assert.Equal(rules.Order(StringComparer.Ordinal), item.Indicia.Select(i => $"{i.RuleId}:{i.Jurisdiction}").Order(StringComparer.Ordinal));
        Assert.Equal(kind == ChangeKind.AddressChanged ? Regime.Crs : Regime.Fatca, item.Regime);
        Assert.Equal(kind == ChangeKind.AddressChanged ? retry.Change.ReceivedAt.AddDays(90) : (DateTime?)null, item.DueAt);
        Assert.Equal(kind == ChangeKind.AddressChanged ? retry.Change.ReceivedAt.AddDays(85) : (DateTime?)null, item.RemindAt);

        await using var db = await host.DbAsync();
        if (kind == ChangeKind.AddressChanged)
        {
            var address = await db.Addresses.SingleAsync(a => a.ClientId == client.Id && a.IsCurrent);
            var phone = await db.Phones.SingleAsync(p => p.ClientId == client.Id && p.IsCurrent);
            Assert.Equal(("DE", "Synthetic Street 51", "Berlin"), (address.Country, address.Line, address.City));
            Assert.Equal(("DE", "+49305550151"), (phone.Country, phone.Number));
            Assert.False((await db.Addresses.SingleAsync(a => a.Id == client.Addresses[0].Id)).IsCurrent);
            Assert.False((await db.Phones.SingleAsync(p => p.Id == client.Phones[0].Id)).IsCurrent);
        }
        else if (kind == ChangeKind.PersonalDataChanged)
        {
            var saved = await db.Clients.SingleAsync(c => c.Id == client.Id);
            Assert.Equal("US", saved.BirthCountry);
            Assert.Equal(["RO", "US"], saved.Citizenships);
        }
        else
        {
            var forms = await db.SelfCertifications.Where(s => s.ClientId == client.Id).OrderBy(s => s.Id).ToListAsync();
            Assert.Equal([SelfCertStatus.Superseded, SelfCertStatus.Superseded, SelfCertStatus.Valid], forms.Select(s => s.Status));
            var current = forms[^1];
            Assert.Equal(retry.SelfCertificationId, current.Id);
            Assert.True(current.DeclaresUsPerson);
            Assert.Equal([("RO", client.Cnp), ("US", (string?)null)], current.Residences.Select(r => (r.Country, r.Tin)));
        }
    }

    [Fact]
    public async Task Indicium_insert_failure_on_extension_preserves_the_original_case_deadline_and_client_then_allows_retry()
    {
        await using var database = await TransactionDatabase.CreateAsync();
        await using var host = await CreateAsync(database.Configure);
        await SeedHistoryAsync(host);
        var original = await RequestSentAsync(host, await host.MoveAsync(ClientCode, "ES"));
        Assert.Equal(Regime.Crs, original.Regime);
        Assert.NotNull(original.DueAt);
        Assert.Equal(("CRS-IND-ADDR", "ES"), (Assert.Single(original.Indicia).RuleId, Assert.Single(original.Indicia).Jurisdiction));
        host.AdvanceDays(9);

        var retry = await RejectAndRetryAsync(host, MoveWithPhone(), IndiciumInsert, AuditKinds.CaseIndiciumAdded,
            ["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"]);

        Assert.Equal(original.Code, retry.CaseCode);
        var extended = Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(original.Code));
        Assert.Equal((original.Status, original.Regime, original.IsPreexisting, original.OpenedAt, original.TriggerChangeId),
            (extended.Status, extended.Regime, extended.IsPreexisting, extended.OpenedAt, extended.TriggerChangeId));
        Assert.Equal((original.DueAt, original.RemindAt, original.DeadlineRuleId, original.DeadlineGuidePage),
            (extended.DueAt, extended.RemindAt, extended.DeadlineRuleId, extended.DeadlineGuidePage));
        AssertOriginalIndicia(original, extended);
        Assert.Equal(["CRS-IND-ADDR:DE", "CRS-IND-PHONE:DE"], extended.Indicia.Where(i => i.ChangeEventId == retry.Change.Id)
            .Select(i => $"{i.RuleId}:{i.Jurisdiction}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Case_metadata_update_failure_keeps_fatca_deadline_free_until_the_crs_change_is_retried()
    {
        await using var database = await TransactionDatabase.CreateAsync();
        await using var host = await CreateAsync(database.Configure);
        await SeedHistoryAsync(host);
        var original = await RequestSentAsync(host, await host.As(Member).Changes.RecordAsync(
            new ChangeRequest(ClientCode, ChangeKind.PhoneAdded, "online", Phone: new PhoneSnapshot("+13125550151", "US"))));
        Assert.Equal((Regime.Fatca, "FATCA-SC"), (original.Regime, original.DeadlineRuleId));
        Assert.Null(original.DueAt);
        Assert.Null(original.RemindAt);
        Assert.Equal("FATCA-IND-PHONE", Assert.Single(original.Indicia).RuleId);
        host.AdvanceDays(7);
        var request = new ChangeRequest(ClientCode, ChangeKind.AddressChanged, "online",
            Address: new AddressSnapshot(AddressKind.Residence, "Synthetic Street 51", "Berlin", "DE"));

        var retry = await RejectAndRetryAsync(host, request, CaseMetadataUpdate, AuditKinds.CaseIndiciumAdded, ["CRS-IND-ADDR:DE"]);

        Assert.Equal(original.Code, retry.CaseCode);
        var extended = Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(original.Code));
        Assert.Equal((original.Status, original.OpenedAt, original.TriggerChangeId), (extended.Status, extended.OpenedAt, extended.TriggerChangeId));
        Assert.Equal((Regime.Both, "DL-NEW-90", false), (extended.Regime, extended.DeadlineRuleId, extended.IsPreexisting));
        Assert.Equal(retry.Change.ReceivedAt.AddDays(90), extended.DueAt);
        Assert.Equal(retry.Change.ReceivedAt.AddDays(85), extended.RemindAt);
        AssertOriginalIndicia(original, extended);
        var added = Assert.Single(extended.Indicia, i => i.ChangeEventId == retry.Change.Id);
        Assert.Equal(("CRS-IND-ADDR", "DE"), (added.RuleId, added.Jurisdiction));
    }

    private static ChangeRequest MoveWithPhone() => new(ClientCode, ChangeKind.AddressChanged, "online",
        Address: new AddressSnapshot(AddressKind.Residence, "Synthetic Street 51", "Berlin", "DE"),
        Phone: new PhoneSnapshot("+49305550151", "DE"));

    private static async Task<Client> SeedHistoryAsync(R2WorkflowHost host)
    {
        var client = await host.SeedClientAsync(ClientCode, new DateOnly(2019, 3, 14));
        Assert.Null((await host.MoveAsync(ClientCode, "RO")).CaseCode);
        var renewed = await host.As(Member).Changes.RecordAsync(new ChangeRequest(ClientCode, ChangeKind.SelfCertSubmitted, "branch",
            SelfCertification: new SelfCertificationSnapshot(host.Clock.UtcNow, [new("RO", client.Cnp)], DeclaresUsPerson: false)));
        Assert.Null(renewed.CaseCode);
        await using (var db = await host.DbAsync())
        {
            var statuses = await db.SelfCertifications.Where(s => s.ClientId == client.Id).OrderBy(s => s.Id).Select(s => s.Status).ToListAsync();
            Assert.Equal([SelfCertStatus.Superseded, SelfCertStatus.Valid], statuses);
        }
        await host.SeedClientAsync(OtherClientCode, new DateOnly(2020, 5, 12));
        await RequestSentAsync(host, await host.MoveAsync(OtherClientCode, "ES"));
        host.AdvanceDays(2);
        return client;
    }

    private static async Task<Case> RequestSentAsync(R2WorkflowHost host, ChangeResult change)
    {
        var code = Assert.IsType<string>(change.CaseCode);
        var draft = await host.As(Member).Cases.DraftRequestAsync(code, "en", "Please send a self-certification for this synthetic case.");
        await host.As(Admin).Cases.ApproveRequestAsync(code, draft.Id, "email");
        return Assert.IsType<Case>(await host.As(Member).Cases.GetAsync(code));
    }

    private static void AssertOriginalIndicia(Case original, Case extended)
    {
        foreach (var indicium in original.Indicia)
            Assert.Equal(JsonSerializer.Serialize(indicium), JsonSerializer.Serialize(Assert.Single(extended.Indicia, i => i.Id == indicium.Id)));
    }

    private static async Task<ChangeResult> RejectAndRetryAsync(R2WorkflowHost host, ChangeRequest request, WriteFault fault,
        string caseAuditKind, string[] addedRules)
    {
        var before = await SnapshotAsync(host);
        Dictionary<string, string[]> afterFailure;
        await ExecuteSqlAsync(host, fault.Sql);
        try
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => host.As(Member).Changes.RecordAsync(request));
            var provider = Assert.IsType<SqliteException>(error.InnerException);
            Assert.Equal(19, provider.SqliteErrorCode);
            Assert.Equal(1811, provider.SqliteExtendedErrorCode); // SQLITE_CONSTRAINT_TRIGGER, not a lock or validation error.
            Assert.Equal($"SQLite Error 19: '{fault.Message}'.", provider.Message);
            Assert.Contains(error.Entries, e => e.Entity.GetType() == fault.EntityType);
            afterFailure = await SnapshotAsync(host);
        }
        finally { await ExecuteSqlAsync(host, "DROP TRIGGER \"FailChangeWrite\";"); }

        // Retry in a new scope before the rollback assertion, so even a RED baseline exercises release of the writer gates.
        var retry = await host.As(Member).Changes.RecordAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
        var afterRetry = await SnapshotAsync(host);
        Assert.Equal(before["AuditEntries"], afterRetry["AuditEntries"].Take(before["AuditEntries"].Length));
        await AssertAuditAsync(host, before["AuditEntries"].Length, retry, caseAuditKind, addedRules);

        var changed = before.Keys.Where(table => !before[table].SequenceEqual(afterFailure[table])).ToArray();
        Assert.True(changed.Length == 0, $"Rejected request must preserve every row and column of the pre-request snapshot. Changed tables: {string.Join(", ", changed)}.");
        foreach (var table in new[] { "ChangeEvents", "RuleEvaluations" })
        {
            Assert.Equal(before[table], afterRetry[table].Take(before[table].Length));
            Assert.Equal(before[table].Length + 1, afterRetry[table].Length);
        }
        Assert.Equal(before["Cases"].Length + (caseAuditKind == AuditKinds.CaseOpened ? 1 : 0), afterRetry["Cases"].Length);
        Assert.Equal(before["Indicia"].Length + addedRules.Length, afterRetry["Indicia"].Length);
        Assert.Equal(before["Addresses"].Length + (request.Address is null ? 0 : 1), afterRetry["Addresses"].Length);
        Assert.Equal(before["Phones"].Length + (request.Phone is null ? 0 : 1), afterRetry["Phones"].Length);
        Assert.Equal(before["SelfCertifications"].Length + (request.SelfCertification is null ? 0 : 1), afterRetry["SelfCertifications"].Length);
        await using var db = await host.DbAsync();
        var evaluation = await db.RuleEvaluations.SingleAsync(e => e.ChangeEventId == retry.Change.Id);
        Assert.Equal(retry.Change.EvaluationId, evaluation.Id);
        Assert.Equal((retry.Outcome.Decision, retry.Outcome.EngineVersion), (evaluation.Decision, evaluation.EngineVersion));
        return retry;
    }

    private static async Task AssertAuditAsync(R2WorkflowHost host, int oldCount, ChangeResult result, string caseAuditKind, string[] addedRules)
    {
        await using var db = await host.DbAsync();
        var entries = await db.AuditEntries.AsNoTracking().OrderBy(e => e.Seq).ToListAsync();
        var item = await db.Cases.SingleAsync(c => c.Code == result.CaseCode);
        Assert.Equal(Enumerable.Range(1, entries.Count).Select(i => (long)i), entries.Select(e => e.Seq));
        await using var scope = host.Root.CreateAsyncScope();
        var audit = Assert.IsType<AuditLog>(scope.ServiceProvider.GetRequiredService<IAuditLog>());
        Assert.Equal(new AuditVerification(true, entries.Count, null, null, null), await audit.VerifyAsync());
        Assert.True(AuditChain.Verify(entries).Ok);

        var rules = result.Outcome.Indicia.Select(hit => (Hit: hit, Applied: true))
            .Concat(result.Outcome.Cleared.Select(hit => (Hit: hit, Applied: false))).ToArray();
        var added = entries.Skip(oldCount).ToList();
        Assert.Equal(new[] { AuditKinds.ChangeReceived }.Concat(Enumerable.Repeat(AuditKinds.RuleApplied, rules.Length)).Append(caseAuditKind),
            added.Select(e => e.Kind));
        Assert.All(added, entry =>
        {
            Assert.Equal("user:" + Member.Id, entry.Actor);
            Assert.Equal(item.Id, entry.CaseId);
            using var payload = JsonDocument.Parse(entry.PayloadJson);
            Assert.Equal(result.Change.Id, payload.RootElement.GetProperty("changeId").GetInt32());
        });
        using var received = JsonDocument.Parse(added[0].PayloadJson);
        Assert.Equal(ClientCode, received.RootElement.GetProperty("client").GetString());
        Assert.Equal(result.Change.Kind.ToString(), received.RootElement.GetProperty("kind").GetString());
        Assert.Equal(result.Outcome.Decision.ToString(), received.RootElement.GetProperty("decision").GetString());
        Assert.Equal(result.Outcome.EngineVersion, received.RootElement.GetProperty("engineVersion").GetString());
        var selfCert = received.RootElement.GetProperty("selfCertificationId");
        Assert.Equal(result.SelfCertificationId, selfCert.ValueKind == JsonValueKind.Null ? (int?)null : selfCert.GetInt32());
        for (var i = 0; i < rules.Length; i++)
        {
            var (hit, applied) = rules[i];
            using var payload = JsonDocument.Parse(added[i + 1].PayloadJson);
            var value = payload.RootElement;
            Assert.Equal((hit.RuleId, hit.Jurisdiction, hit.Regime.ToString(), hit.GuidePage, applied),
                (value.GetProperty("rule").GetString(), value.GetProperty("jurisdiction").GetString(), value.GetProperty("regime").GetString(),
                    value.GetProperty("page").GetString(), value.GetProperty("applied").GetBoolean()));
            Assert.Equal(applied ? hit.CoveredBy.ToString() : null, value.GetProperty("coveredBy").GetString());
        }
        using var last = JsonDocument.Parse(added[^1].PayloadJson);
        Assert.Equal(item.Code, last.RootElement.GetProperty("code").GetString());
        Assert.Equal(item.Regime.ToString(), last.RootElement.GetProperty("regime").GetString());
        Assert.Equal(addedRules.Order(StringComparer.Ordinal), last.RootElement.GetProperty(caseAuditKind == AuditKinds.CaseOpened ? "rules" : "added")
            .EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
    }

    private static readonly string[] Tables =
    [
        "Clients", "Addresses", "Phones", "Accounts", "StandingOrders", "PowersOfAttorney", "SelfCertifications", "ClientDocuments",
        "AcceptedExplanations", "ChangeEvents", "RuleEvaluations", "Cases", "Indicia", "Evidence", "ContextSignals",
        "ClientRequests", "ClientResponses", "ConfirmationProofs", "AuditEntries",
    ];

    private static async Task<Dictionary<string, string[]>> SnapshotAsync(R2WorkflowHost host)
    {
        await using var db = await host.DbAsync();
        await db.Database.OpenConnectionAsync();
        var snapshot = new Dictionary<string, string[]>(StringComparer.Ordinal);
        // Every stored column, including historical flags, self-certification JSON and old audit links; no tracked entities.
        foreach (var table in Tables)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table}\" ORDER BY 1"; // Fixed test-only table names, never request input.
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<string>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++) row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                rows.Add(JsonSerializer.Serialize(row));
            }
            snapshot.Add(table, [.. rows]);
        }
        return snapshot;
    }

    private static async Task ExecuteSqlAsync(R2WorkflowHost host, string sql)
    {
        await using var db = await host.DbAsync();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>An isolated temporary in-memory SQLite database, with a separate connection for every context, as in production.</summary>
    private sealed class TransactionDatabase : IDbContextFactory<AppDbContext>, IAsyncDisposable
    {
        private readonly SqliteConnection _keeper = new($"Data Source=change-transaction-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False");

        public static async Task<TransactionDatabase> CreateAsync()
        {
            var database = new TransactionDatabase();
            await database._keeper.OpenAsync();
            return database;
        }

        public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_keeper.ConnectionString).Options);

        public void Configure(IServiceCollection services)
        {
            services.AddSingleton<IDbContextFactory<AppDbContext>>(this);
            services.AddSingleton<ICrsRuleEngine>(sp => sp.GetRequiredService<CrsRuleEngine>());
            services.AddScoped<IAuditLog, AuditLog>();
        }

        public ValueTask DisposeAsync() => _keeper.DisposeAsync();
    }
}
