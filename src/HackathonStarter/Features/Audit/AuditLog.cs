using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Audit;

/// <summary>
/// One link of the audit hash chain (docs/PLAN.md 12):
/// Hash = SHA256(PrevHash + "\n" + [Seq, At, Actor, CaseId, Kind, PayloadJson] as a JSON array, see <see cref="AuditChain"/>);
/// the first entry has PrevHash "GENESIS". At is UTC, truncated to milliseconds before saving.
/// PayloadJson is masked (PiiVault.ForStorage) before it is saved.
/// </summary>
public class AuditEntry
{
    public long Seq { get; set; }
    public DateTime At { get; set; }
    public string Actor { get; set; } = "";             // "system", "agent", "user:admin"
    public int? CaseId { get; set; }
    public string Kind { get; set; } = "";              // see AuditKinds
    public string PayloadJson { get; set; } = "{}";
    public string PrevHash { get; set; } = "";
    public string Hash { get; set; } = "";
}

public static class AuditActors
{
    public const string System = "system";
    public const string Agent = "agent";
    public static string User(string id) => $"user:{id}";
}

/// <summary>Entry kinds, so every module writes the same names.</summary>
public static class AuditKinds
{
    public const string ChangeReceived = "change.received";
    public const string RuleApplied = "rule.applied";
    public const string CaseOpened = "case.opened";
    public const string CaseIndiciumAdded = "case.indicium-added";
    public const string EvidenceRecorded = "evidence.recorded";
    public const string SignalRecorded = "signal.recorded";
    public const string RequestDrafted = "request.drafted";
    public const string RequestApproved = "request.approved";
    public const string RequestRejected = "request.rejected";
    public const string ResponseRegistered = "response.registered";
    public const string CaseReminded = "case.reminded";
    public const string CaseEscalated = "case.escalated";
    public const string CaseExpired = "case.expired";
    public const string CaseClosed = "case.closed";
    public const string TinChecked = "tin.checked";
    public const string ShieldAlert = "shield.alert";
    public const string ClockChanged = "clock.changed";
}

/// <param name="FirstBrokenSeq">The first entry whose hash no longer matches; every later entry is suspect too.</param>
public record AuditVerification(bool Ok, int Checked, long? FirstBrokenSeq, string? Expected, string? Actual);

/// <summary>
/// Append-only hash chain: one writer (<see cref="AppendAsync"/>), serialized with a static lock so Seq stays continuous
/// across scopes. Scoped, because Tamper checks the current user's role. Owner: R3.
/// </summary>
public interface IAuditLog
{
    /// <param name="payload">Any object; serialized to JSON and masked before saving.</param>
    Task<AuditEntry> AppendAsync(string actor, string kind, object? payload = null, int? caseId = null);
    Task<List<AuditEntry>> ListAsync(int? caseId = null, int take = 200);
    Task<AuditVerification> VerifyAsync();
    /// <summary>Demo only, admin only: edits a saved payload without recomputing the hash, so VerifyAsync shows the break.</summary>
    Task TamperAsync(long seq);
}

/// <summary>
/// The guide asks for a record that can credibly prove each confirmation "for audit purposes" and a system ensuring "the
/// information received is the information sent" (ANAF guide p. 34-35). One write point, serialized, so Seq stays continuous.
/// </summary>
public sealed class AuditLog(IDbContextFactory<AppDbContext> dbFactory, ISimClock clock, CurrentUser user) : IAuditLog
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    // Readable in the UI: "+49" and "Brașov" stay as they are instead of \u002B and \u0219.
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<AuditEntry> AppendAsync(string actor, string kind, object? payload = null, int? caseId = null)
    {
        var payloadJson = MaskedJson(payload);
        await WriteLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var last = await db.AuditEntries.AsNoTracking().OrderByDescending(e => e.Seq).FirstOrDefaultAsync();
            var now = clock.UtcNow;
            var entry = new AuditEntry
            {
                Seq = (last?.Seq ?? 0) + 1,
                At = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc),
                Actor = actor,
                CaseId = caseId,
                Kind = kind,
                PayloadJson = payloadJson,
                PrevHash = last?.Hash ?? AuditChain.Genesis,
            };
            entry.Hash = AuditChain.ComputeHash(entry.PrevHash, entry);
            db.AuditEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<List<AuditEntry>> ListAsync(int? caseId = null, int take = 200)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AuditEntries.AsNoTracking()
            .Where(e => caseId == null || e.CaseId == caseId)
            .OrderByDescending(e => e.Seq)
            .Take(take)
            .ToListAsync();
    }

    public async Task<AuditVerification> VerifyAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return AuditChain.Verify(await db.AuditEntries.AsNoTracking().OrderBy(e => e.Seq).ToListAsync());
    }

    public async Task TamperAsync(long seq)
    {
        await user.Require(Roles.Admin);
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.AuditEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Seq == seq)
            ?? throw new KeyNotFoundException($"Audit entry #{seq} does not exist.");

        var payload = JsonNode.Parse(entry.PayloadJson) as JsonObject ?? [];
        payload["edited"] = "outside the application";
        var edited = payload.ToJsonString(Readable);
        await db.AuditEntries.Where(e => e.Seq == seq).ExecuteUpdateAsync(s => s.SetProperty(e => e.PayloadJson, edited));
    }

    /// <summary>The payload as JSON, every string and number masked with one storage vault for the whole entry.</summary>
    private static string MaskedJson(object? payload) =>
        payload is null ? "{}" : Mask(JsonSerializer.SerializeToNode(payload, JsonSerializerOptions.Web), PiiVault.ForStorage())?.ToJsonString(Readable) ?? "{}";

    private static JsonNode? Mask(JsonNode? node, PiiVault vault)
    {
        switch (node)
        {
            case JsonObject o:
                var masked = new JsonObject();
                foreach (var (key, value) in o) masked[key] = Mask(value, vault);
                return masked;
            case JsonArray a:
                return new JsonArray(a.Select(x => Mask(x, vault)).ToArray());
            case JsonValue v when v.GetValueKind() is JsonValueKind.String or JsonValueKind.Number:
                var raw = v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : v.ToJsonString();
                var text = vault.Mask(raw);
                // A number that contained personal data (a CNP) becomes a masked string.
                return text == raw ? v.DeepClone() : JsonValue.Create(text);
            default:
                return node?.DeepClone();
        }
    }
}

public static class AuditSetup
{
    public static IServiceCollection AddAudit(this IServiceCollection services) =>
        services.AddScoped<IAuditLog, AuditLog>();
}

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> e)
    {
        e.HasKey(a => a.Seq);
        e.Property(a => a.Seq).ValueGeneratedNever();
        e.HasIndex(a => a.CaseId);
    }
}
