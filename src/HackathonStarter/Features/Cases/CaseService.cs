using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Cases;

public record CaseQueueFilter(CaseStatus? Status = null, bool OpenOnly = true, string? ClientCode = null);

/// <param name="CaseCode">Null for evidence about a client without a case (e.g. the signal of golden case 8).</param>
public record NewEvidence(string ClientCode, string? CaseCode, EvidenceSource Source, string Quote, int? DocumentId = null,
                          int? Page = null, string? LinkedRuleId = null, string RecordedBy = "agent");

public record NewSignal(string ClientCode, string? CaseCode, string Description, int EvidenceId, SignalStrength Strength = SignalStrength.Weak);

/// <param name="SelfCertificationId">A new self-certification is saved first through IChangeService (ChangeKind.SelfCertSubmitted),
/// which owns client data; pass its id here. CaseService writes no client data except the AcceptedExplanation on close
/// (see that class); there is no Cases -> Changes dependency.</param>
public record NewResponse(ResponseKind Kind, ExplanationCategory Category, string Channel, string Summary,
                          int? DocumentId = null, bool HasDocumentaryEvidence = false, int? SelfCertificationId = null);

/// <summary>What one pass of the deadline watcher changed.</summary>
public record DeadlineTick(int Reminded, int Escalated, int Expired);

/// <summary>
/// The only place where a case changes state (docs/PLAN.md 9.3). Every method checks the current state (invalid
/// transition = exception), the role (CurrentUser.Require) and writes the audit log. Time from <c>ISimClock</c>, never
/// the app-wide TimeProvider. Code computes everything a model must not decide: requested documents, proof fields,
/// whether a response is enough. Dependencies: rules, deadlines, shield, audit, clients; never IChangeService. Owner: R2.
/// </summary>
public interface ICaseService
{
    /// <summary>Open cases first, ordered by deadline; cases without a deadline (FATCA, self-certification checks) last,
    /// ordered explicitly (SQLite and PostgreSQL sort NULLs differently).</summary>
    Task<List<Case>> QueueAsync(CaseQueueFilter? filter = null);
    /// <summary>The case with indicia, evidence, signals, requests, responses and proof.</summary>
    Task<Case?> GetAsync(string code);

    /// <summary>
    /// Opens a case for a change with uncovered indicia; preexisting/new and the deadline are computed by code from the
    /// client's accounts (Preexisting.For, IDeadlineCalculator; several accounts: the earliest deadline, GOLDEN-CASES I4,
    /// NEVERIFICAT). Only the indicia of a change of circumstances (*-IND-*) give a deadline: CRS ones the CRS deadline,
    /// FATCA ones none (p. 38), self-certification checks (SC-*) none. If the client already has an open case, the new
    /// indicia are added and the deadline stays (design choice: the guide is silent, PLAN 9.2, NEVERIFICAT).
    /// </summary>
    Task<Case> OpenAsync(ChangeEvent change, RuleOutcome outcome);

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


/// <summary>
/// The case workflow (docs/PLAN.md 9.3, docs/roluri/R2-spec-faza1.md). A static writer gate keeps transitions and the
/// deadline tick from interleaving; every write saves to the database first, then appends to the audit log (codes only).
/// Owner: R2.
/// </summary>
public class CaseService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICrsRuleEngine engine,
    IDeadlineCalculator deadlines,
    IShieldedDocuments documents,
    IEvidenceVerifier verifier,
    IAuditLog audit,
    ISimClock clock,
    CurrentUser currentUser) : ICaseService
{
    public const string SystemActor = "system";
    public const string DefaultAssignee = "operator";
    /// <summary>The watcher's escalation reason (a code text: the reminder day is our design, 5 days before DueAt, PLAN 9.2).</summary>
    public const string ReminderEscalationReason = "Reminder day (day 85 on a new account, 5 days before the deadline): no answer, escalated automatically";

    /// <summary>The reasonable explanations listed by the guide (p. 37). None / Other: a person decides, never this code.</summary>
    public static readonly IReadOnlySet<ExplanationCategory> AcceptedCategories = new HashSet<ExplanationCategory>
    {
        ExplanationCategory.Student, ExplanationCategory.TeacherOrTrainee, ExplanationCategory.Diplomat, ExplanationCategory.EuOfficial,
    };

    private static readonly SemaphoreSlim Writer = new(1, 1);

    // ---- Queries (any role, the viewer included) ----

    public async Task<List<Case>> QueueAsync(CaseQueueFilter? filter = null)
    {
        filter ??= new CaseQueueFilter();
        await using var db = await dbFactory.CreateDbContextAsync();
        IQueryable<Case> query = db.Cases.AsNoTracking().AsSplitQuery().Include(c => c.Indicia.OrderBy(i => i.Id));
        // An explicit status wins over OpenOnly (Status = Closed lists the closed cases).
        if (filter.Status is { } status) query = query.Where(c => c.Status == status);
        else if (filter.OpenOnly) query = query.Where(c => c.Status != CaseStatus.Closed && c.Status != CaseStatus.Expired);
        if (!string.IsNullOrWhiteSpace(filter.ClientCode))
        {
            var clientCode = filter.ClientCode.Trim();
            query = query.Where(c => c.ClientCode == clientCode);
        }
        // NULL ordering differs between SQLite and PostgreSQL: cases without a deadline (FATCA, SC checks) go last explicitly.
        return await query
            .OrderBy(c => c.Status == CaseStatus.Closed || c.Status == CaseStatus.Expired)
            .ThenBy(c => c.DueAt == null)
            .ThenBy(c => c.DueAt)
            .ThenBy(c => c.Id)
            .ToListAsync();
    }

    public async Task<Case?> GetAsync(string code)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Full(db.Cases.AsNoTracking()).FirstOrDefaultAsync(c => c.Code == code);
    }

    public async Task<List<Evidence>> ListEvidenceAsync(string clientCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Evidence.AsNoTracking()
            .Where(e => db.Clients.Any(c => c.Id == e.ClientId && c.Code == clientCode))
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    public async Task<List<ContextSignal>> ListSignalsAsync(string clientCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ContextSignals.AsNoTracking()
            .Where(s => db.Clients.Any(c => c.Id == s.ClientId && c.Code == clientCode))
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> RequiredDocumentsAsync(string caseCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.Cases.AsNoTracking().Include(c => c.Indicia).FirstOrDefaultAsync(c => c.Code == caseCode)
            ?? throw CaseNotFound(caseCode);
        return Documents(item);
    }

    // ---- Opening and growing a case ----

    public async Task<Case> OpenAsync(ChangeEvent change, RuleOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(outcome);
        var user = await RequireEditor();
        var uncovered = outcome.Uncovered.GroupBy(h => (h.RuleId, h.Jurisdiction)).Select(g => g.First()).ToList();
        if (uncovered.Count == 0)
            throw new InvalidOperationException($"Decision {outcome.Decision} has no uncovered indicium: no case to open.");
        var regime = Combine(uncovered.Select(h => h.Regime));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var client = await db.Clients.AsNoTracking().Include(c => c.Accounts).FirstOrDefaultAsync(c => c.Id == change.ClientId)
                ?? throw new KeyNotFoundException($"Client {change.ClientId} not found.");
            var now = clock.UtcNow;

            var existing = await db.Cases.Include(c => c.Indicia)
                .Where(c => c.ClientId == client.Id && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Expired)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();
            if (existing is not null) return await ExtendAsync(db, existing, change, uncovered, regime, client.Accounts, now, user);

            var item = new Case
            {
                Code = await NextCodeAsync(db, now.Year),
                ClientId = client.Id,
                ClientCode = client.Code,
                TriggerChangeId = change.Id,
                Regime = regime,
                Status = CaseStatus.Open,
                OpenedAt = now,
                AssignedTo = DefaultAssignee,
                Indicia = [.. uncovered.Select(h => ToIndicium(h, change.Id, now))],
            };
            ApplyDeadline(item, OpeningDeadline(uncovered, regime, client.Accounts, change.ReceivedAt, now));
            db.Cases.Add(item);
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.CaseOpened, new
            {
                code = item.Code, client = item.ClientCode, changeId = change.Id, regime = item.Regime.ToString(),
                rules = item.Indicia.Select(i => $"{i.RuleId}:{i.Jurisdiction}").ToArray(),
                deadlineRule = item.DeadlineRuleId, preexisting = item.IsPreexisting, dueAt = item.DueAt, remindAt = item.RemindAt,
            }, item.Id);
            return item;
        });
    }

    private async Task<Case> ExtendAsync(AppDbContext db, Case item, ChangeEvent change, List<RuleHit> uncovered, Regime regime,
                                         List<Account> accounts, DateTime now, DemoUser user)
    {
        var added = uncovered.Where(h => !item.Indicia.Any(i => i.RuleId == h.RuleId && i.Jurisdiction == h.Jurisdiction)).ToList();
        if (added.Count == 0) return item;

        var hadCrs = HasCrs(item.Regime);
        item.Indicia.AddRange(added.Select(h => ToIndicium(h, change.Id, now)));
        item.Regime = item.Regime == regime ? regime : Regime.Both;
        // The deadline stays (design choice, NEVERIFICAT: the guide is silent on several changes in one case, PLAN 9.2).
        // A case without one (FATCA only, p. 38, or self-certification checks only) gets the CRS deadline from the change
        // that brings its first CRS indicium (p. 29, 35-36); a self-certification check never starts one.
        var deadlineStarted = false;
        if (item.DueAt is null && added.Any(h => IsCrsIndicium(h.RuleId)))
        {
            ApplyDeadline(item, Deadline(accounts, change.ReceivedAt, Regime.Crs, now));
            deadlineStarted = true;
        }
        // A FATCA case that gains a CRS rule but no deadline: preexisting on the CRS cutoff, as when a case opens (OpeningDeadline).
        else if (!hadCrs && HasCrs(item.Regime))
            item.IsPreexisting = Deadline(accounts, change.ReceivedAt, Regime.Crs, now).Preexisting;
        await db.SaveChangesAsync();

        await audit.AppendAsync(Actor(user), AuditKinds.CaseIndiciumAdded, new
        {
            code = item.Code, changeId = change.Id, regime = item.Regime.ToString(),
            added = added.Select(h => $"{h.RuleId}:{h.Jurisdiction}").ToArray(), deadlineStarted, dueAt = item.DueAt,
        }, item.Id);
        return item;
    }

    /// <summary>
    /// The deadline of a new case, set only by its uncovered indicia of a change of circumstances (*-IND-*, the lists of
    /// p. 29 and p. 31): a CRS one gives the CRS deadline (a Both case too); FATCA ones alone give none (FATCA-SC, p. 38).
    /// Self-certification checks alone (SC-*, e.g. a TIN in the wrong format) give none either: the 90 days follow a change
    /// of circumstances (p. 29, 35-36), and the guide gives no deadline for checking a declaration (GOLDEN-CASES I1, as for
    /// FATCA; NEVERIFICAT). IsPreexisting as for any case: on the CRS cutoff when the case has a CRS rule, else on FATCA's.
    /// </summary>
    private DeadlineResult OpeningDeadline(IReadOnlyList<RuleHit> uncovered, Regime regime, IReadOnlyCollection<Account> accounts,
                                           DateTime knownAt, DateTime now)
    {
        var usual = Deadline(accounts, knownAt, HasCrs(regime) ? Regime.Crs : Regime.Fatca, now);
        if (uncovered.Any(h => IsCrsIndicium(h.RuleId))) return usual; // DL-NEW-90 or DL-PRE-YEAR
        if (uncovered.Any(h => IsIndicium(h.RuleId)))                  // FATCA indicia only: FATCA-SC, no deadline
            return Deadline(accounts, knownAt, Regime.Fatca, now) with { Preexisting = usual.Preexisting };

        var check = uncovered.FirstOrDefault(h => h.RuleId.StartsWith("SC-", StringComparison.Ordinal)) ?? uncovered[0];
        var (en, ro) = CaseTexts.NoDeadlineForSelfCertificationCheck;
        return usual with
        {
            DueAt = null, RemindAt = null, RuleId = check.RuleId, GuidePage = check.GuidePage, Explanation = en, ExplanationRo = ro, DesignChoice = true,
        };
    }

    /// <summary>One deadline per account; several accounts give the earliest (GOLDEN-CASES I4, NEVERIFICAT). No account = a new one opened today.</summary>
    private DeadlineResult Deadline(IReadOnlyCollection<Account> accounts, DateTime knownAt, Regime regime, DateTime now)
    {
        var openedOn = accounts.Count == 0 ? [DateOnly.FromDateTime(now)] : accounts.OrderBy(a => a.Id).Select(a => a.OpenedOn).ToList();
        var results = openedOn.Select(d => deadlines.Compute(new DeadlineInput(knownAt, regime, d))).ToList();
        return results.Where(r => r.DueAt is not null).OrderBy(r => r.DueAt).FirstOrDefault() ?? results[0];
    }

    private static void ApplyDeadline(Case item, DeadlineResult deadline)
    {
        item.DueAt = deadline.DueAt;
        item.RemindAt = deadline.RemindAt;
        item.IsPreexisting = deadline.Preexisting;
        item.DeadlineRuleId = deadline.RuleId;
        item.DeadlineGuidePage = deadline.GuidePage;
    }

    private static async Task<string> NextCodeAsync(AppDbContext db, int year)
    {
        var prefix = $"CRS-{year}-";
        var codes = await db.Cases.Where(c => c.Code.StartsWith(prefix)).Select(c => c.Code).ToListAsync();
        // Cases added in this context but not saved yet (several FATCA follow-ups in one tick).
        codes.AddRange(db.ChangeTracker.Entries<Case>().Where(e => e.State == EntityState.Added && e.Entity.Code.StartsWith(prefix)).Select(e => e.Entity.Code));
        var last = codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{last + 1:0000}";
    }

    // ---- Evidence and signals ----

    public async Task<Evidence> AddEvidenceAsync(NewEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var user = await RequireEditor();
        if (string.IsNullOrWhiteSpace(evidence.Quote)) throw new ArgumentException("The quote is empty.", nameof(evidence));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var client = await FindClientAsync(db, evidence.ClientCode);
            var item = evidence.CaseCode is null ? null : await FindCaseOfClientAsync(db, evidence.CaseCode, client);

            bool verified;
            int? page = evidence.Page;
            string note;
            var hiddenOnly = false;
            if (evidence.Source == EvidenceSource.Document)
            {
                if (evidence.DocumentId is not { } documentId)
                    throw new ArgumentException("A quote from a document needs its DocumentId.", nameof(evidence));
                var document = await documents.GetAsync(documentId) ?? throw new KeyNotFoundException($"Document {documentId} not found.");
                if (document.ClientId != client.Id)
                    throw new InvalidOperationException($"Document {documentId} does not belong to client {client.Code}.");
                var check = verifier.Verify(evidence.Quote, document.Result, evidence.Page);
                verified = check.Verified;
                page = check.Page ?? evidence.Page;
                note = check.Note;
                hiddenOnly = check.FoundOnlyInHiddenText;
            }
            else
            {
                verified = true;
                note = "bank data";
            }

            var saved = new Evidence
            {
                ClientId = client.Id, CaseId = item?.Id, Source = evidence.Source, DocumentId = evidence.DocumentId, Page = page,
                Quote = evidence.Quote.Trim(), Verified = verified, VerificationNote = note, LinkedRuleId = evidence.LinkedRuleId,
                RecordedBy = string.IsNullOrWhiteSpace(evidence.RecordedBy) ? Actor(user) : evidence.RecordedBy, At = clock.UtcNow,
            };
            db.Evidence.Add(saved);
            await db.SaveChangesAsync();

            var actor = Actor(user);
            await audit.AppendAsync(actor, AuditKinds.EvidenceRecorded, new
            {
                evidenceId = saved.Id, client = client.Code, source = saved.Source.ToString(), documentId = saved.DocumentId,
                page = saved.Page, verified = saved.Verified, linkedRule = saved.LinkedRuleId,
            }, item?.Id);
            if (hiddenOnly)
                await audit.AppendAsync(actor, AuditKinds.ShieldAlert, new
                {
                    evidenceId = saved.Id, client = client.Code, documentId = saved.DocumentId, reason = "quote found only in hidden text",
                }, item?.Id);
            return saved;
        });
    }

    public async Task<ContextSignal> AddSignalAsync(NewSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var user = await RequireEditor();
        if (string.IsNullOrWhiteSpace(signal.Description)) throw new ArgumentException("The signal needs a description.", nameof(signal));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var client = await FindClientAsync(db, signal.ClientCode);
            var evidence = await db.Evidence.AsNoTracking().FirstOrDefaultAsync(e => e.Id == signal.EvidenceId)
                ?? throw new KeyNotFoundException($"Evidence {signal.EvidenceId} not found.");
            if (evidence.ClientId != client.Id)
                throw new InvalidOperationException($"Evidence {signal.EvidenceId} does not belong to client {client.Code}.");
            var item = signal.CaseCode is null ? null : await FindCaseOfClientAsync(db, signal.CaseCode, client);

            // A signal never changes a decision, a case status or a deadline (AGENTS.md): it is only saved and logged.
            var saved = new ContextSignal
            {
                ClientId = client.Id, CaseId = item?.Id, Description = signal.Description.Trim(), EvidenceId = evidence.Id,
                Strength = signal.Strength, At = clock.UtcNow,
            };
            db.ContextSignals.Add(saved);
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.SignalRecorded, new
            {
                signalId = saved.Id, client = client.Code, evidenceId = evidence.Id, strength = saved.Strength.ToString(),
            }, item?.Id);
            return saved;
        });
    }

    // ---- Requests ----

    public async Task<ClientRequest> DraftRequestAsync(string caseCode, string language, string body, RequestKind kind = RequestKind.Initial)
    {
        var user = await RequireEditor();
        var lang = (language ?? "").Trim().ToLowerInvariant();
        if (!Lang.Supported.Contains(lang))
            throw new ArgumentException($"Language must be one of: {string.Join(", ", Lang.Supported)}.", nameof(language));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("The request body is empty.", nameof(body));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await FindCaseAsync(db, caseCode);
            // A reminder can also be redrafted while the case waits for the client (e.g. after rejecting the watcher's draft).
            var allowed = item.Status is CaseStatus.Open or CaseStatus.ResponseReceived
                || (kind == RequestKind.Reminder && item.Status is CaseStatus.AwaitingClient or CaseStatus.Reminded or CaseStatus.Escalated);
            if (!allowed) throw Invalid(item, $"draft a {kind} request");

            var request = new ClientRequest
            {
                Kind = kind, Language = lang, Body = body.Trim(), RequestedDocuments = [.. Documents(item)], Status = RequestStatus.Draft,
                DraftedBy = Actor(user), CreatedAt = clock.UtcNow,
            };
            item.Requests.Add(request);
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.RequestDrafted, new
            {
                code = item.Code, requestId = request.Id, kind = kind.ToString(), language = lang, documents = request.RequestedDocuments,
            }, item.Id);
            return request;
        });
    }

    public async Task<ClientRequest> ApproveRequestAsync(string caseCode, int requestId, string channel)
    {
        var user = await RequireOperator();
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentException("The channel is required.", nameof(channel));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await FindCaseAsync(db, caseCode);
            var request = FindRequest(item, requestId);
            if (request.Status != RequestStatus.Draft)
                throw new InvalidOperationException($"Request {requestId} is {request.Status}: only a draft can be approved.");

            var from = item.Status;
            if (request.Kind == RequestKind.Reminder && item.Status is CaseStatus.AwaitingClient or CaseStatus.Reminded or CaseStatus.Escalated)
            {
                // A reminder keeps the status: the case is already waiting for the client.
            }
            else if (item.Status is CaseStatus.Open or CaseStatus.ResponseReceived) item.Status = CaseStatus.AwaitingClient;
            else throw Invalid(item, $"approve a {request.Kind} request");

            request.Status = RequestStatus.Sent;
            request.ApprovedBy = user.Name;
            request.ApprovedAt = clock.UtcNow;
            request.Channel = channel.Trim();
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.RequestApproved, new
            {
                code = item.Code, requestId, kind = request.Kind.ToString(), channel = request.Channel,
                from = from.ToString(), to = item.Status.ToString(),
            }, item.Id);
            return request;
        });
    }

    public async Task<ClientRequest> RejectRequestAsync(string caseCode, int requestId, string reason)
    {
        var user = await RequireOperator();
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("The reason is required.", nameof(reason));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await FindCaseAsync(db, caseCode);
            var request = FindRequest(item, requestId);
            if (request.Status != RequestStatus.Draft)
                throw new InvalidOperationException($"Request {requestId} is {request.Status}: only a draft can be rejected.");

            request.Status = RequestStatus.Rejected;
            request.RejectedBy = user.Name;
            request.RejectedAt = clock.UtcNow;
            request.RejectionReason = reason.Trim();
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.RequestRejected, new
            {
                code = item.Code, requestId, kind = request.Kind.ToString(), status = item.Status.ToString(),
            }, item.Id);
            return request;
        });
    }

    // ---- Responses and closing ----

    public async Task<ClientResponse> RegisterResponseAsync(string caseCode, NewResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var user = await RequireEditor();
        if (string.IsNullOrWhiteSpace(response.Channel)) throw new ArgumentException("The channel is required.", nameof(response));

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await FindCaseAsync(db, caseCode);
            if (item.Status is not (CaseStatus.AwaitingClient or CaseStatus.Reminded or CaseStatus.Escalated))
                throw Invalid(item, "register a response");
            if (response.DocumentId is { } documentId)
            {
                var owner = await db.ClientDocuments.Where(d => d.Id == documentId).Select(d => (int?)d.ClientId).FirstOrDefaultAsync()
                    ?? throw new KeyNotFoundException($"Document {documentId} not found.");
                if (owner != item.ClientId) throw new InvalidOperationException($"Document {documentId} does not belong to client {item.ClientCode}.");
            }
            if (response.SelfCertificationId is { } selfCertId)
            {
                var selfCert = await db.SelfCertifications.AsNoTracking().FirstOrDefaultAsync(s => s.Id == selfCertId)
                    ?? throw new KeyNotFoundException($"Self-certification {selfCertId} not found.");
                RequireAnswerSelfCertification(item, selfCert);
            }

            var from = item.Status;
            var saved = new ClientResponse
            {
                Kind = response.Kind, Category = response.Category, Channel = response.Channel.Trim(), ReceivedAt = clock.UtcNow,
                DocumentId = response.DocumentId, SelfCertificationId = response.SelfCertificationId,
                HasDocumentaryEvidence = response.HasDocumentaryEvidence, Summary = response.Summary?.Trim() ?? "",
            };
            item.Responses.Add(saved);
            item.Status = CaseStatus.ResponseReceived;
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.ResponseRegistered, new
            {
                code = item.Code, responseId = saved.Id, kind = saved.Kind.ToString(), category = saved.Category.ToString(),
                documentId = saved.DocumentId, selfCertificationId = saved.SelfCertificationId,
                hasDocumentaryEvidence = saved.HasDocumentaryEvidence, from = from.ToString(), to = item.Status.ToString(),
            }, item.Id);
            return saved;
        });
    }

    public async Task<Case> CloseAsync(string caseCode, ConfirmationChannel channel,
                                       ConfirmationEvidenceKind evidenceKind = ConfirmationEvidenceKind.SignedDocument, string? collectionSteps = null)
    {
        var user = await RequireOperator();

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await FindCaseAsync(db, caseCode);
            if (item.Status != CaseStatus.ResponseReceived) throw Invalid(item, "close (it needs a registered response)");
            var response = item.Responses.OrderBy(r => r.ReceivedAt).ThenBy(r => r.Id).LastOrDefault()
                ?? throw new InvalidOperationException($"Case {item.Code} has no response to close on.");
            var client = await ClientService.Full(db).AsTracking().FirstOrDefaultAsync(c => c.Id == item.ClientId)
                ?? throw new KeyNotFoundException($"Client {item.ClientCode} not found.");

            var explanation = IsExplanationEnough(item, response);
            SelfCertification? selfCert = null;
            if (!explanation)
            {
                selfCert = client.SelfCertifications.FirstOrDefault(s => s.Id == response.SelfCertificationId)
                    ?? throw new InvalidOperationException($"Self-certification {response.SelfCertificationId} does not belong to client {client.Code}.");
                RequireAnswerSelfCertification(item, selfCert); // again: a later submission may have superseded it
            }

            // Guide p. 34: the self-certification is dated at the latest on the date of receipt.
            var datedAt = selfCert?.SignedAt ?? response.ReceivedAt;
            var receivedAt = selfCert?.ReceivedAt ?? response.ReceivedAt;
            if (datedAt > receivedAt)
                throw new InvalidOperationException(
                    $"Case {item.Code}: the self-certification is dated {datedAt:O}, after it was received ({receivedAt:O}); it must be dated at the latest on the date of receipt (guide p. 34).");

            var now = clock.UtcNow;
            var snapshot = ClientSnapshot.From(client);
            var accepted = new List<AcceptedExplanation>();
            if (explanation)
            {
                accepted = item.Indicia.Where(i => i.Regime != Regime.Fatca).Select(i => i.Jurisdiction).Distinct().Order(StringComparer.Ordinal)
                    .Select(j => new AcceptedExplanation { ClientId = client.Id, Jurisdiction = j, Category = response.Category, CaseCode = item.Code, AcceptedAt = now })
                    .ToList();
                snapshot = snapshot with
                {
                    AcceptedExplanations = [.. snapshot.AcceptedExplanations, .. accepted.Select(a => new AcceptedExplanationSnapshot(a.Jurisdiction, a.Category, a.CaseCode, a.AcceptedAt))],
                };
            }

            // Code re-evaluates the client before closing; a model never decides that the answer is enough.
            var outcome = engine.EvaluateCurrent(snapshot);
            var open = outcome.Uncovered.Select(h => $"{h.RuleId}:{h.Jurisdiction}").ToList();
            if (open.Count > 0 || outcome.Decision is Decision.Clarification or Decision.FatcaAlert or Decision.ShieldAlert)
                throw new InvalidOperationException(
                    $"Case {item.Code}: re-evaluation gives {outcome.Decision}{(open.Count > 0 ? " (" + string.Join(", ", open) + ")" : "")}; it stays {item.Status}. Ask the client for a follow-up.");

            var (sha, reference) = await EvidenceHashAsync(db, client.Id, response, selfCert, item.Code);
            var lastSent = item.Requests.Where(r => r.Status == RequestStatus.Sent).OrderBy(r => r.ApprovedAt).ThenBy(r => r.Id).LastOrDefault();
            item.Proof = new ConfirmationProof
            {
                // The moment the client confirmed (guide p. 34, "momentul confirmării") is when the answer reached the bank;
                // the operator's closing time is Case.ClosedAt.
                Channel = channel, ConfirmedAt = response.ReceivedAt, DeclarationDatedAt = datedAt, DeclarationReceivedAt = receivedAt,
                CollectionSteps = string.IsNullOrWhiteSpace(collectionSteps)
                    ? CaseTexts.DefaultCollectionSteps(response, lastSent, user.Name, channel)
                    : collectionSteps.Trim(),
                TransactionOrDocumentId = reference, EvidenceKind = evidenceKind, EvidenceSha256 = sha,
                SignedBy = client.FullName, SignerCapacity = SignerCapacity.AccountHolder, ApprovedBy = user.Name,
            };

            var (en, ro) = explanation
                ? CaseTexts.ClosedOnExplanation(response.Category, [.. accepted.Select(a => a.Jurisdiction)])
                : CaseTexts.ClosedOnSelfCertification(item.Regime, outcome.ReportableFor);
            item.Status = CaseStatus.Closed;
            item.ClosedAt = now;
            item.ReportableFor = [.. outcome.ReportableFor];
            item.Outcome = en;
            item.OutcomeRo = ro;
            // The rule the closing rests on: RESP-EXPLANATION (p. 37); a valid new self-certification, SC-VALID (p. 34) for
            // CRS and Both (golden case 4), FATCA-SC (p. 38) for FATCA alone.
            item.OutcomeRuleId = explanation ? "RESP-EXPLANATION" : item.Regime == Regime.Fatca ? "FATCA-SC" : "SC-VALID";
            // The only write of CaseService to client data (Client.AcceptedExplanations, golden case 18).
            db.AcceptedExplanations.AddRange(accepted);
            await db.SaveChangesAsync();

            await audit.AppendAsync(Actor(user), AuditKinds.CaseClosed, new
            {
                code = item.Code, outcomeRule = item.OutcomeRuleId, decision = outcome.Decision.ToString(), engineVersion = outcome.EngineVersion,
                reportableFor = item.ReportableFor, responseKind = response.Kind.ToString(), category = response.Category.ToString(),
                explained = accepted.Select(a => a.Jurisdiction).ToArray(), channel = channel.ToString(), evidenceKind = evidenceKind.ToString(),
                reference, evidenceSha256 = sha,
            }, item.Id);
            return item;
        });
    }

    /// <summary>
    /// The self-certification that answers a case: the client's own, still Valid (not superseded or doubtful) and received
    /// after the case opened. An older one is the declaration the change made unreliable (p. 35-36), never the answer.
    /// </summary>
    private static void RequireAnswerSelfCertification(Case item, SelfCertification selfCert)
    {
        if (selfCert.ClientId != item.ClientId)
            throw new InvalidOperationException($"Self-certification {selfCert.Id} does not belong to client {item.ClientCode}.");
        if (selfCert.Status != SelfCertStatus.Valid)
            throw new InvalidOperationException(
                $"Case {item.Code}: self-certification {selfCert.Id} is {selfCert.Status}; only the current valid self-certification answers the case.");
        if (selfCert.ReceivedAt < item.OpenedAt)
            throw new InvalidOperationException(
                $"Case {item.Code}: self-certification {selfCert.Id} was received before the case opened; the answer must be a new self-certification.");
    }

    /// <summary>True for an accepted explanation, false for a self-certification; throws when the response is not enough.</summary>
    private static bool IsExplanationEnough(Case item, ClientResponse response)
    {
        var fatca = item.Regime is Regime.Fatca or Regime.Both;
        var crsPreexisting = HasCrs(item.Regime) && item.IsPreexisting;
        switch (response.Kind)
        {
            case ResponseKind.NewSelfCertification:
                if (response.SelfCertificationId is null)
                    throw new InvalidOperationException(
                        $"Case {item.Code}: the response names no self-certification. Record it through IChangeService (SelfCertSubmitted) and register its id.");
                if (crsPreexisting && !response.HasDocumentaryEvidence)
                    throw new InvalidOperationException(
                        $"Case {item.Code}: a preexisting CRS account needs a new self-certification AND new documentary evidence (guide p. 29).");
                return false;
            case ResponseKind.ReasonableExplanation:
                if (fatca)
                    throw new InvalidOperationException($"Case {item.Code}: FATCA needs a new self-certification (guide p. 38); an explanation does not close it.");
                if (crsPreexisting)
                    throw new InvalidOperationException(
                        $"Case {item.Code}: a preexisting CRS account needs a new self-certification and new documentary evidence (guide p. 29); an explanation does not close it.");
                if (!AcceptedCategories.Contains(response.Category))
                    throw new InvalidOperationException(
                        $"Case {item.Code}: explanation category {response.Category} is not on the guide's list (p. 37). A person decides outside this method.");
                return true;
            default:
                throw new InvalidOperationException(
                    $"Case {item.Code}: response kind {response.Kind} is not a self-certification or a listed explanation. A person decides outside this method.");
        }
    }

    private static async Task<(string Sha, string Reference)> EvidenceHashAsync(AppDbContext db, int clientId, ClientResponse response,
                                                                                SelfCertification? selfCert, string caseCode)
    {
        if (response.DocumentId is { } documentId)
        {
            var content = await db.ClientDocuments.Where(d => d.Id == documentId && d.ClientId == clientId).Select(d => d.Content).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException($"Document {documentId} of the response not found for this client.");
            return (Sha256(content), $"DOC-{documentId}");
        }
        if (selfCert is not null) return (Sha256(Encoding.UTF8.GetBytes(CanonicalJson(selfCert))), $"SC-{selfCert.Id}");
        return (Sha256(Encoding.UTF8.GetBytes(CanonicalJson(response, caseCode))), $"RESP-{response.Id}");
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The self-certification as stable JSON (fixed field order, residences sorted, ISO dates): hashed when there is no document.</summary>
    public static string CanonicalJson(SelfCertification s) => JsonSerializer.Serialize(new
    {
        id = s.Id,
        clientId = s.ClientId,
        signedAt = s.SignedAt.ToString("O", CultureInfo.InvariantCulture),
        receivedAt = s.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
        channel = s.Channel,
        declaresUsPerson = s.DeclaresUsPerson,
        residences = s.Residences.OrderBy(r => r.Country, StringComparer.Ordinal).ThenBy(r => r.Tin, StringComparer.Ordinal)
            .Select(r => new { country = r.Country, tin = r.Tin, noTinReason = r.NoTinReason.ToString(), noTinExplanation = r.NoTinExplanation }),
    });

    private static string CanonicalJson(ClientResponse r, string caseCode) => JsonSerializer.Serialize(new
    {
        caseCode,
        responseId = r.Id,
        kind = r.Kind.ToString(),
        category = r.Category.ToString(),
        channel = r.Channel,
        receivedAt = r.ReceivedAt.ToString("O", CultureInfo.InvariantCulture),
    });

    public async Task<Case> EscalateAsync(string caseCode, string reason)
    {
        var user = await RequireEditor();

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var item = await FindCaseAsync(db, caseCode);
            if (item.Status is not (CaseStatus.Open or CaseStatus.AwaitingClient or CaseStatus.Reminded)) throw Invalid(item, "escalate");

            var from = item.Status;
            item.Status = CaseStatus.Escalated;
            item.EscalatedAt = clock.UtcNow;
            item.EscalationReason = string.IsNullOrWhiteSpace(reason) ? null : PiiVault.ForStorage().Mask(reason.Trim());
            await db.SaveChangesAsync();

            // The free-text reason stays on the case (masked); the audit payload holds codes and statuses only.
            await audit.AppendAsync(Actor(user), AuditKinds.CaseEscalated, new
            {
                code = item.Code, from = from.ToString(), to = item.Status.ToString(), automatic = false, hasReason = item.EscalationReason is not null,
            }, item.Id);
            return item;
        });
    }

    // ---- Deadlines ----

    public async Task<DeadlineTick> TickAsync(DateTime now)
    {
        now = now.Kind == DateTimeKind.Local ? now.ToUniversalTime() : DateTime.SpecifyKind(now, DateTimeKind.Utc);

        return await WriteAsync(async () =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            // DueAt null (FATCA, p. 38; self-certification checks) never reminds or expires; ResponseReceived is not expired
            // (the answer came in time).
            var waiting = await db.Cases.Include(c => c.Indicia).Include(c => c.Requests)
                .Where(c => c.DueAt != null && (c.Status == CaseStatus.Open || c.Status == CaseStatus.AwaitingClient
                                                || c.Status == CaseStatus.Reminded || c.Status == CaseStatus.Escalated))
                .OrderBy(c => c.Id)
                .ToListAsync();
            var toExpire = waiting.Where(c => c.DueAt <= now).ToList();
            // RemindedAt == null: one reminder per case, even when a follow-up sends it back to AwaitingClient after day 85.
            var toRemind = waiting.Where(c => c.DueAt > now && c.RemindAt <= now && c.RemindedAt == null
                                              && c.Status is CaseStatus.Open or CaseStatus.AwaitingClient).ToList();
            var entries = new List<(string Kind, object Payload, Case Target)>();

            // Reminder day: Reminded, then Escalated to the tax department in the same tick (PLAN 9.4), plus a reminder draft.
            foreach (var item in toRemind)
            {
                var from = item.Status;
                item.Status = CaseStatus.Reminded;
                item.RemindedAt = now;
                var documents = Documents(item);
                item.Requests.Add(new ClientRequest
                {
                    Kind = RequestKind.Reminder, Language = "ro", Body = CaseTexts.ReminderBodyRo(item.DueAt!.Value, documents),
                    RequestedDocuments = [.. documents], Status = RequestStatus.Draft, DraftedBy = SystemActor, CreatedAt = now,
                });
                entries.Add((AuditKinds.CaseReminded, new { code = item.Code, from = from.ToString(), remindAt = item.RemindAt, dueAt = item.DueAt }, item));
                item.Status = CaseStatus.Escalated;
                item.EscalatedAt = now;
                item.EscalationReason = ReminderEscalationReason;
                entries.Add((AuditKinds.CaseEscalated, new { code = item.Code, from = CaseStatus.Reminded.ToString(), to = item.Status.ToString(), automatic = true }, item));
            }

            if (toExpire.Count > 0)
            {
                var clientIds = toExpire.Select(c => c.ClientId).Distinct().ToList();
                var selfCerts = await db.SelfCertifications.AsNoTracking().Where(s => clientIds.Contains(s.ClientId)).ToListAsync();
                var accounts = await db.Accounts.AsNoTracking().Where(a => clientIds.Contains(a.ClientId)).ToListAsync();
                foreach (var item in toExpire)
                {
                    var from = item.Status;
                    // The deadline is the CRS one, so is the consequence: FATCA indicia never enter the CRS ReportableFor.
                    var crsIndicia = Jurisdictions(item.Indicia.Where(i => i.Regime != Regime.Fatca).Select(i => i.Jurisdiction));
                    item.Status = CaseStatus.Expired;
                    item.ExpiredAt = now;

                    // Design choice (the guide has no rule for a case with both regimes): a Both case expires on its CRS
                    // deadline with the CRS consequence, and its FATCA indicia continue in a new FATCA-only case with no
                    // deadline (FATCA-SC, p. 38), so the FATCA part is not lost.
                    var followUp = await FatcaFollowUpAsync(db, item, accounts.Where(a => a.ClientId == item.ClientId).ToList(), now);

                    if (item.IsPreexisting)
                    {
                        // Preexisting (p. 29, example 2): resident at least in the new jurisdiction.
                        item.OutcomeRuleId = "DL-EXPIRED-PRE";
                        item.ReportableFor = crsIndicia;
                        (item.Outcome, item.OutcomeRo) = CaseTexts.ExpiredPreexisting(crsIndicia, followUp?.Code);
                    }
                    else
                    {
                        // New account (p. 36): resident in the jurisdiction of the original self-certification AND in the new one.
                        var original = OriginalJurisdictions(selfCerts.Where(s => s.ClientId == item.ClientId), item.OpenedAt);
                        item.OutcomeRuleId = "DL-EXPIRED-NEW";
                        item.ReportableFor = Jurisdictions(original.Concat(crsIndicia));
                        (item.Outcome, item.OutcomeRo) = CaseTexts.ExpiredNew(original, crsIndicia, followUp?.Code);
                    }
                    entries.Add((AuditKinds.CaseExpired, new
                    {
                        code = item.Code, from = from.ToString(), outcomeRule = item.OutcomeRuleId, reportableFor = item.ReportableFor, dueAt = item.DueAt,
                        fatcaFollowUp = followUp?.Code,
                    }, item));
                    if (followUp is not null)
                        entries.Add((AuditKinds.CaseOpened, new
                        {
                            code = followUp.Code, client = followUp.ClientCode, changeId = followUp.TriggerChangeId, regime = followUp.Regime.ToString(),
                            rules = followUp.Indicia.Select(i => $"{i.RuleId}:{i.Jurisdiction}").ToArray(),
                            deadlineRule = followUp.DeadlineRuleId, preexisting = followUp.IsPreexisting, dueAt = followUp.DueAt, remindAt = followUp.RemindAt,
                            followUpOf = item.Code, note = $"fatca follow-up of {item.Code}",
                        }, followUp));
                }
            }

            if (entries.Count > 0) await db.SaveChangesAsync();
            foreach (var (kind, payload, target) in entries) await audit.AppendAsync(SystemActor, kind, payload, target.Id);
            return new DeadlineTick(toRemind.Count, toRemind.Count, toExpire.Count);
        });
    }

    /// <summary>The FATCA-only case that takes over the FATCA indicia of an expiring Both case; null when there are none.</summary>
    private async Task<Case?> FatcaFollowUpAsync(AppDbContext db, Case expired, List<Account> accounts, DateTime now)
    {
        var fatca = expired.Indicia.Where(i => i.Regime == Regime.Fatca).ToList();
        if (fatca.Count == 0) return null;
        var followUp = new Case
        {
            Code = await NextCodeAsync(db, now.Year),
            ClientId = expired.ClientId,
            ClientCode = expired.ClientCode,
            TriggerChangeId = expired.TriggerChangeId,
            Regime = Regime.Fatca,
            Status = CaseStatus.Open,
            OpenedAt = now,
            AssignedTo = DefaultAssignee,
            Indicia =
            [
                .. fatca.Select(i => new Indicium
                {
                    ChangeEventId = i.ChangeEventId, RuleId = i.RuleId, Regime = i.Regime, Jurisdiction = i.Jurisdiction, Explanation = i.Explanation,
                    ExplanationRo = i.ExplanationRo, GuidePage = i.GuidePage, SourceField = i.SourceField, AddedAt = now,
                }),
            ],
        };
        ApplyDeadline(followUp, Deadline(accounts, now, Regime.Fatca, now)); // FATCA: no deadline in the guide (p. 38)
        db.Cases.Add(followUp);
        return followUp;
    }

    /// <summary>The jurisdictions of the self-certification in force when the case opened (Doubtful ones never count).</summary>
    private static List<string> OriginalJurisdictions(IEnumerable<SelfCertification> selfCerts, DateTime openedAt) =>
        selfCerts.Where(s => s.ReceivedAt <= openedAt && s.Status != SelfCertStatus.Doubtful)
            .OrderByDescending(s => s.SignedAt).ThenByDescending(s => s.ReceivedAt).ThenByDescending(s => s.Id)
            .FirstOrDefault()?.Residences.Select(r => r.Country.Trim().ToUpperInvariant()).Distinct().ToList() ?? [];

    /// <summary>Reportable jurisdictions: distinct, sorted, never RO (the bank reports RO residents to no one).</summary>
    private static List<string> Jurisdictions(IEnumerable<string> countries) =>
        countries.Select(c => c.Trim().ToUpperInvariant()).Where(c => c.Length > 0 && c != "RO").Distinct().Order(StringComparer.Ordinal).ToList();

    // ---- Helpers ----

    private Task<DemoUser> RequireEditor() => currentUser.Require(Roles.Admin, Roles.Member);
    private Task<DemoUser> RequireOperator() => currentUser.Require(Roles.Operator);
    private static string Actor(DemoUser user) => "user:" + user.Id;

    private static async Task<T> WriteAsync<T>(Func<Task<T>> work)
    {
        await Writer.WaitAsync();
        try { return await work(); }
        finally { Writer.Release(); }
    }

    private static IQueryable<Case> Full(IQueryable<Case> cases) => cases.AsSplitQuery()
        .Include(c => c.Indicia.OrderBy(i => i.Id))
        .Include(c => c.Evidence.OrderBy(e => e.Id))
        .Include(c => c.Signals.OrderBy(s => s.Id))
        .Include(c => c.Requests.OrderBy(r => r.Id))
        .Include(c => c.Responses.OrderBy(r => r.Id))
        .Include(c => c.Proof);

    private static async Task<Case> FindCaseAsync(AppDbContext db, string code) =>
        await Full(db.Cases).FirstOrDefaultAsync(c => c.Code == code) ?? throw CaseNotFound(code);

    private static async Task<Case> FindCaseOfClientAsync(AppDbContext db, string code, Client client)
    {
        var item = await db.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code) ?? throw CaseNotFound(code);
        if (item.ClientId != client.Id) throw new InvalidOperationException($"Case {code} does not belong to client {client.Code}.");
        return item;
    }

    private static async Task<Client> FindClientAsync(AppDbContext db, string code) =>
        await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code) ?? throw new KeyNotFoundException($"Client {code} not found.");

    private static ClientRequest FindRequest(Case item, int requestId) =>
        item.Requests.FirstOrDefault(r => r.Id == requestId) ?? throw new KeyNotFoundException($"Request {requestId} not found on case {item.Code}.");

    private static KeyNotFoundException CaseNotFound(string code) => new($"Case {code} not found.");

    private static InvalidOperationException Invalid(Case item, string action) =>
        new($"Case {item.Code} is {item.Status}: cannot {action}.");

    private static IReadOnlyList<string> Documents(Case item) =>
        RequiredDocuments.For(item.Regime, item.IsPreexisting, item.Indicia.Select(i => i.RuleId));

    private static bool HasCrs(Regime regime) => regime is Regime.Crs or Regime.Both;

    /// <summary>An indicium of a change of circumstances (CRS p. 29, FATCA p. 31): the only rules that give a case a deadline.</summary>
    private static bool IsIndicium(string ruleId) => IsCrsIndicium(ruleId) || ruleId.StartsWith("FATCA-IND-", StringComparison.Ordinal);

    private static bool IsCrsIndicium(string ruleId) => ruleId.StartsWith("CRS-IND-", StringComparison.Ordinal);

    private static Regime Combine(IEnumerable<Regime> regimes)
    {
        var set = regimes.Distinct().ToList();
        return set.Count == 1 ? set[0] : Regime.Both;
    }

    private static Indicium ToIndicium(RuleHit hit, int changeId, DateTime now) => new()
    {
        ChangeEventId = changeId == 0 ? null : changeId, RuleId = hit.RuleId, Regime = hit.Regime, Jurisdiction = hit.Jurisdiction,
        Explanation = hit.Explanation, ExplanationRo = hit.ExplanationRo, GuidePage = hit.GuidePage, SourceField = hit.SourceField, AddedAt = now,
    };
}

public static class CasesSetup
{
    public static IServiceCollection AddCases(this IServiceCollection services)
    {
        services.AddScoped<ICaseService, CaseService>();
        services.AddHostedService<DeadlineWatcher>();
        return services;
    }
}
