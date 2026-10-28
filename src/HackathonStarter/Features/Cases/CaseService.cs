using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Cases;

public record CaseQueueFilter(CaseStatus? Status = null, bool OpenOnly = true, string? ClientCode = null);

/// <param name="CaseCode">Null for evidence about a client without a case (e.g. the signal of golden case 8).</param>
public record NewEvidence(string ClientCode, string? CaseCode, EvidenceSource Source, string Quote, int? DocumentId = null,
                          int? Page = null, string? LinkedRuleId = null, string RecordedBy = "agent");

public record NewSignal(string ClientCode, string? CaseCode, string Description, int EvidenceId, SignalStrength Strength = SignalStrength.Weak);

/// <param name="SelfCertificationId">A new self-certification is saved first through IChangeService (ChangeKind.SelfCertSubmitted),
/// which owns client data; pass its id here. CaseService never writes client data (no Cases -> Changes dependency).</param>
public record NewResponse(ResponseKind Kind, ExplanationCategory Category, string Channel, string Summary,
                          int? DocumentId = null, bool HasDocumentaryEvidence = false, int? SelfCertificationId = null);

/// <summary>What one pass of the deadline watcher changed.</summary>
public record DeadlineTick(int Reminded, int Escalated, int Expired);

/// <summary>
/// The only place where a case changes state (docs/PLAN.md 9.3). Every method checks the current state (invalid
/// transition = exception), the role (CurrentUser.Require) and writes the audit log. Time from <c>SimClock</c>, never
/// the app-wide TimeProvider. Code computes everything a model must not decide: requested documents, proof fields,
/// whether a response is enough. Dependencies: rules, deadlines, shield, audit, clients; never IChangeService. Owner: R2.
/// </summary>
public interface ICaseService
{
    /// <summary>Open cases first, ordered by deadline.</summary>
    Task<List<Case>> QueueAsync(CaseQueueFilter? filter = null);
    /// <summary>The case with indicia, evidence, signals, requests, responses and proof.</summary>
    Task<Case?> GetAsync(string code);

    /// <summary>
    /// Opens a case for a change with uncovered indicia. If the client already has an open case, the new indicia are added
    /// to it and the deadline stays (design choice: the guide is silent, PLAN 9.2, NEVERIFICAT).
    /// </summary>
    Task<Case> OpenAsync(ChangeEvent change, RuleOutcome outcome, bool isPreexisting);

    /// <summary>Saves a quote; code verifies it against the visible (shielded) text of the document page.</summary>
    Task<Evidence> AddEvidenceAsync(NewEvidence evidence);
    /// <summary>Saves a context signal. Never changes the decision or the case status.</summary>
    Task<ContextSignal> AddSignalAsync(NewSignal signal);
    /// <summary>Evidence and signals of a client, with or without a case (golden case 8 has no case).</summary>
    Task<List<Evidence>> ListEvidenceAsync(string clientCode);
    Task<List<ContextSignal>> ListSignalsAsync(string clientCode);

    /// <summary>The documents the client must send, computed by code from the indicia, regime and account age (e.g. the visa for a student, p. 37).</summary>
    Task<IReadOnlyList<string>> RequiredDocumentsAsync(string caseCode);
    /// <summary>Saves the request written by the model; code attaches <see cref="RequiredDocumentsAsync"/>.</summary>
    Task<ClientRequest> DraftRequestAsync(string caseCode, string language, string body, RequestKind kind = RequestKind.Initial);
    /// <summary>Open → AwaitingClient, or ResponseReceived → AwaitingClient for a follow-up after an insufficient answer. Operator only.</summary>
    Task<ClientRequest> ApproveRequestAsync(string caseCode, int requestId, string channel);
    Task<ClientRequest> RejectRequestAsync(string caseCode, int requestId, string reason);
    /// <summary>AwaitingClient / Reminded / Escalated → ResponseReceived.</summary>
    Task<ClientResponse> RegisterResponseAsync(string caseCode, NewResponse response);
    /// <summary>
    /// ResponseReceived → Closed, only if code finds the response enough for the regime and account age (new CRS:
    /// self-certification or explanation; preexisting CRS: self-certification and documentary evidence; FATCA: new
    /// self-certification). The proof (dates, hash of the document, signer, approver) is built by code from the response.
    /// </summary>
    Task<Case> CloseAsync(string caseCode, ConfirmationChannel channel,
                          ConfirmationEvidenceKind evidenceKind = ConfirmationEvidenceKind.SignedDocument, string? collectionSteps = null);
    Task<Case> EscalateAsync(string caseCode, string reason);

    /// <summary>Reminders, escalations and expiries due at <paramref name="now"/>. Called by DeadlineWatcher and by tests.</summary>
    Task<DeadlineTick> TickAsync(DateTime now);
}

/// <summary>Phase 0 placeholder: queries return nothing, transitions throw. Owner: R2.</summary>
public class CaseService : ICaseService
{
    private static NotImplementedException Todo(string method) => new($"CaseService.{method} arrives with R2 (phase 1).");

    public Task<List<Case>> QueueAsync(CaseQueueFilter? filter = null) => Task.FromResult(new List<Case>());
    public Task<Case?> GetAsync(string code) => Task.FromResult<Case?>(null);
    public Task<Case> OpenAsync(ChangeEvent change, RuleOutcome outcome, bool isPreexisting) => throw Todo(nameof(OpenAsync));
    public Task<Evidence> AddEvidenceAsync(NewEvidence evidence) => throw Todo(nameof(AddEvidenceAsync));
    public Task<ContextSignal> AddSignalAsync(NewSignal signal) => throw Todo(nameof(AddSignalAsync));
    public Task<List<Evidence>> ListEvidenceAsync(string clientCode) => Task.FromResult(new List<Evidence>());
    public Task<List<ContextSignal>> ListSignalsAsync(string clientCode) => Task.FromResult(new List<ContextSignal>());
    public Task<IReadOnlyList<string>> RequiredDocumentsAsync(string caseCode) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<ClientRequest> DraftRequestAsync(string caseCode, string language, string body, RequestKind kind = RequestKind.Initial) => throw Todo(nameof(DraftRequestAsync));
    public Task<ClientRequest> ApproveRequestAsync(string caseCode, int requestId, string channel) => throw Todo(nameof(ApproveRequestAsync));
    public Task<ClientRequest> RejectRequestAsync(string caseCode, int requestId, string reason) => throw Todo(nameof(RejectRequestAsync));
    public Task<ClientResponse> RegisterResponseAsync(string caseCode, NewResponse response) => throw Todo(nameof(RegisterResponseAsync));
    public Task<Case> CloseAsync(string caseCode, ConfirmationChannel channel, ConfirmationEvidenceKind evidenceKind = ConfirmationEvidenceKind.SignedDocument, string? collectionSteps = null) => throw Todo(nameof(CloseAsync));
    public Task<Case> EscalateAsync(string caseCode, string reason) => throw Todo(nameof(EscalateAsync));
    public Task<DeadlineTick> TickAsync(DateTime now) => Task.FromResult(new DeadlineTick(0, 0, 0));
}

public static class CasesSetup
{
    public static IServiceCollection AddCases(this IServiceCollection services) =>
        services.AddScoped<ICaseService, CaseService>();
}

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> e)
    {
        e.HasIndex(c => c.Code).IsUnique();
        e.HasIndex(c => new { c.Status, c.DueAt });
        e.HasIndex(c => c.ClientId);
        e.HasIndex(c => c.ClientCode);
        e.Property(c => c.Status).HasConversion<string>();
        e.Property(c => c.Regime).HasConversion<string>();
        e.Ignore(c => c.IsOpen);
        e.HasMany(c => c.Indicia).WithOne().HasForeignKey(i => i.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Evidence).WithOne().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Signals).WithOne().HasForeignKey(s => s.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Requests).WithOne().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Responses).WithOne().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(c => c.Proof).WithOne().HasForeignKey<ConfirmationProof>(p => p.CaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IndiciumConfiguration : IEntityTypeConfiguration<Indicium>
{
    public void Configure(EntityTypeBuilder<Indicium> e) => e.Property(i => i.Regime).HasConversion<string>();
}

public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> e)
    {
        e.Property(x => x.Source).HasConversion<string>();
        e.HasIndex(x => x.ClientId);
    }
}

public class ContextSignalConfiguration : IEntityTypeConfiguration<ContextSignal>
{
    public void Configure(EntityTypeBuilder<ContextSignal> e)
    {
        e.Property(s => s.Strength).HasConversion<string>();
        e.HasIndex(s => s.ClientId);
    }
}

public class ClientRequestConfiguration : IEntityTypeConfiguration<ClientRequest>
{
    public void Configure(EntityTypeBuilder<ClientRequest> e)
    {
        e.Property(r => r.Kind).HasConversion<string>();
        e.Property(r => r.Status).HasConversion<string>();
    }
}

public class ClientResponseConfiguration : IEntityTypeConfiguration<ClientResponse>
{
    public void Configure(EntityTypeBuilder<ClientResponse> e)
    {
        e.Property(r => r.Kind).HasConversion<string>();
        e.Property(r => r.Category).HasConversion<string>();
    }
}

public class ConfirmationProofConfiguration : IEntityTypeConfiguration<ConfirmationProof>
{
    public void Configure(EntityTypeBuilder<ConfirmationProof> e)
    {
        e.Property(p => p.Channel).HasConversion<string>();
        e.Property(p => p.EvidenceKind).HasConversion<string>();
        e.Property(p => p.SignerCapacity).HasConversion<string>();
    }
}
