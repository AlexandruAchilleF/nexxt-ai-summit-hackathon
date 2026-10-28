using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Audit;

/// <summary>
/// One link of the audit hash chain (docs/PLAN.md 12):
/// Hash = SHA256(PrevHash + Seq + At + Actor + CaseId + Kind + PayloadJson); the first entry has PrevHash "GENESIS".
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

/// <summary>Phase 0 placeholder: accepts entries and saves nothing. R3 replaces it with the hash chain.</summary>
public sealed class AuditLog : IAuditLog
{
    public Task<AuditEntry> AppendAsync(string actor, string kind, object? payload = null, int? caseId = null) =>
        Task.FromResult(new AuditEntry { Actor = actor, Kind = kind, CaseId = caseId });

    public Task<List<AuditEntry>> ListAsync(int? caseId = null, int take = 200) => Task.FromResult(new List<AuditEntry>());
    public Task<AuditVerification> VerifyAsync() => Task.FromResult(new AuditVerification(true, 0, null, null, null));
    public Task TamperAsync(long seq) => Task.CompletedTask;
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
