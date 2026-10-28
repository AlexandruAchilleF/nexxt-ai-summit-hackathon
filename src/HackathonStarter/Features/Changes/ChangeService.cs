using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Changes;

public interface IChangeService
{
    /// <summary>Saves the change, applies it, evaluates the rules (code), saves the evaluation, opens a case if needed, writes the audit log.</summary>
    Task<ChangeResult> RecordAsync(ChangeRequest request);
    Task<List<ChangeEvent>> ListAsync(string? clientCode = null, int take = 50);
}

/// <summary>
/// The only writer of client data (besides CaseService's AcceptedExplanation on close). One change: apply it to the client,
/// snapshot before/after, let code decide (rule engine or shield), save the change and the evaluation in one transaction,
/// then the case, then the audit log. Depends on ICaseService; CaseService never depends on this. Owner: R2.
/// </summary>
public class ChangeService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICrsRuleEngine engine,
    ICaseService cases,
    IShieldedDocuments shieldedDocuments,
    IAuditLog audit,
    ISimClock clock,
    CurrentUser currentUser) : IChangeService
{
    public const string ShieldVersion = "shield";

    private static readonly SemaphoreSlim Writer = new(1, 1);
    // Relaxed escaping: "+40..." must stay readable, or PiiVault would miss the phone behind "\u002B" (stored text, never HTML).
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<ChangeResult> RecordAsync(ChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await currentUser.Require(Roles.Admin, Roles.Member);
        Validate(request);

        await Writer.WaitAsync();
        try { return await RecordLockedAsync(request, "user:" + user.Id, systemCases: null); }
        finally { Writer.Release(); }
    }

    /// <summary>
    /// RecordAsync with no signed-in user, for a system writer (the startup seed, ChangeHistorySeed): the same validation,
    /// lock and decisions; <paramref name="actor"/> ("system:seed") goes to the audit log. Not on IChangeService, so pages
    /// and agent tools cannot skip the role check; the caller builds this service with a CaseService.
    /// </summary>
    internal async Task<ChangeResult> RecordAsSystemAsync(ChangeRequest request, string actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!actor.StartsWith("system", StringComparison.Ordinal))
            throw new ArgumentException($"A system actor starts with \"system\", not '{actor}'.", nameof(actor));
        var systemCases = cases as CaseService ?? throw new InvalidOperationException("The system path needs CaseService.");
        Validate(request);

        await Writer.WaitAsync();
        try { return await RecordLockedAsync(request, actor, systemCases); }
        finally { Writer.Release(); }
    }

    private async Task<ChangeResult> RecordLockedAsync(ChangeRequest request, string actor, CaseService? systemCases)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var client = await ClientService.Full(db).AsTracking().FirstOrDefaultAsync(c => c.Code == request.ClientCode)
            ?? throw new KeyNotFoundException($"Client {request.ClientCode} not found.");

        // A self-certification sent as the answer to a case: that case must exist and be this client's.
        Case? answered = null;
        if (request.Kind == ChangeKind.SelfCertSubmitted && request.CaseCode is { } caseCode)
        {
            answered = await cases.GetAsync(caseCode) ?? throw new KeyNotFoundException($"Case {caseCode} not found.");
            if (answered.ClientId != client.Id) throw new InvalidOperationException($"Case {caseCode} does not belong to client {client.Code}.");
            if (!answered.IsOpen)
                throw new InvalidOperationException($"Case {caseCode} is {answered.Status}: a self-certification can no longer answer it.");
        }
        if (request.DocumentId is { } documentId && !await db.ClientDocuments.AnyAsync(d => d.Id == documentId && d.ClientId == client.Id))
            throw new KeyNotFoundException($"Document {documentId} not found for client {client.Code}.");
        var shielded = request.Kind == ChangeKind.DocumentUploaded
            ? await shieldedDocuments.GetAsync(request.DocumentId!.Value) ?? throw new KeyNotFoundException($"Document {request.DocumentId} not found.")
            : null;

        var now = clock.UtcNow;
        var before = ClientSnapshot.From(client);
        var applied = Apply(client, request, now);
        var after = ClientSnapshot.From(client);

        RuleOutcome outcome;
        if (shielded is not null) outcome = ShieldOutcome(shielded.Result);
        else if (applied.SelfCertification is { } selfCert) outcome = engine.EvaluateSelfCertification(after, SelfCertificationSnapshot.From(selfCert));
        else outcome = engine.Evaluate(before, after, request.Kind);

        ChangeEvent change;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            if (shielded is not null)
                await db.ClientDocuments.Where(d => d.Id == shielded.DocumentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.ShieldVerdict, shielded.Result.Verdict));
            await db.SaveChangesAsync(); // the client's new state first: the new self-certification gets its id

            var vault = PiiVault.ForStorage(); // one vault: the old and new values share placeholders
            change = new ChangeEvent
            {
                ClientId = client.Id,
                Kind = request.Kind,
                Summary = string.IsNullOrWhiteSpace(request.Summary) ? Summarize(request, applied) : vault.Mask(request.Summary.Trim()),
                OldValueJson = applied.Old is null ? null : vault.Mask(JsonSerializer.Serialize(applied.Old, Json)),
                NewValueJson = applied.New is null ? null : vault.Mask(JsonSerializer.Serialize(applied.New, Json)),
                SourceChannel = request.Channel.Trim(),
                ReceivedAt = now,
            };
            db.ChangeEvents.Add(change);
            await db.SaveChangesAsync();

            var evaluation = new RuleEvaluation
            {
                ChangeEventId = change.Id,
                Decision = outcome.Decision,
                ResultJson = PiiVault.ForStorage().Mask(JsonSerializer.Serialize(outcome, Json)),
                EngineVersion = outcome.EngineVersion,
                EvaluatedAt = now,
            };
            db.RuleEvaluations.Add(evaluation);
            await db.SaveChangesAsync();
            change.EvaluationId = evaluation.Id;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // The case first, then the audit log (another connection; codes, rule IDs and jurisdictions only), so a failed
        // append never leaves a change with uncovered indicia and no case. An answer to an open case that brings a new
        // uncovered hit (e.g. a TIN in the wrong format) extends that case: OpenAsync adds to the client's open case.
        string? resultCase = answered?.Code;
        int? caseId = answered?.Id;
        if (outcome.Uncovered.Any())
        {
            var opened = systemCases is null ? await cases.OpenAsync(change, outcome) : await systemCases.OpenAsSystemAsync(change, outcome, actor);
            (resultCase, caseId) = (opened.Code, opened.Id);
        }

        await audit.AppendAsync(actor, AuditKinds.ChangeReceived, new
        {
            changeId = change.Id, client = client.Code, kind = change.Kind.ToString(), channel = change.SourceChannel,
            decision = outcome.Decision.ToString(), engineVersion = outcome.EngineVersion, selfCertificationId = applied.SelfCertification?.Id,
            documentId = request.DocumentId, answers = answered?.Code,
        }, caseId);
        foreach (var hit in outcome.Indicia)
            await audit.AppendAsync(actor, AuditKinds.RuleApplied, RulePayload(change.Id, hit, applied: true), caseId);
        foreach (var hit in outcome.Cleared)
            await audit.AppendAsync(actor, AuditKinds.RuleApplied, RulePayload(change.Id, hit, applied: false), caseId);
        if (shielded?.Result.Verdict == ShieldVerdict.HiddenTextRemoved)
            await audit.AppendAsync(actor, AuditKinds.ShieldAlert, new
            {
                changeId = change.Id, client = client.Code, documentId = shielded.DocumentId, verdict = shielded.Result.Verdict.ToString(),
                hiddenFragments = shielded.Result.Hidden.Count, pages = shielded.Result.Hidden.Select(h => h.Page).Distinct().Order().ToArray(),
            });

        return new ChangeResult(change, outcome, resultCase);
    }

    public async Task<List<ChangeEvent>> ListAsync(string? clientCode = null, int take = 50)
    {
        take = Math.Clamp(take, 1, 500);
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.ChangeEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            var code = clientCode.Trim();
            query = query.Where(e => db.Clients.Any(c => c.Id == e.ClientId && c.Code == code));
        }
        return await query.OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.Id).Take(take).ToListAsync();
    }

    // ---- Applying a change ----

    /// <param name="SelfCertification">The new self-certification entity (SelfCertSubmitted), with its id after saving.</param>
    private sealed record Applied(object? Old, object? New, SelfCertification? SelfCertification = null);

    private static Applied Apply(Client client, ChangeRequest r, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        switch (r.Kind)
        {
            case ChangeKind.AddressChanged:
            {
                var address = r.Address!;
                // Design choice (golden case 12): an in-care-of or hold-mail address replaces every current address, since the
                // bank then has only that address: what CRS-IND-CO ("no other address", p. 29 f) and FATCA-IND-CO ("the only
                // address", p. 31) describe. A residence or mailing address replaces only the current ones of its kind.
                var replacesAll = address.Kind is AddressKind.InCareOf or AddressKind.HoldMail;
                var replaced = client.Addresses.Where(a => a.IsCurrent && (replacesAll || a.Kind == address.Kind)).ToList();
                replaced.ForEach(a => a.IsCurrent = false);
                client.Addresses.Add(NewAddress(address, today));
                // GOLDEN-CASES I3: one AddressChanged event may also carry the phone that replaces the current ones (Maria).
                var replacedPhones = r.Phone is null ? [] : ReplacePhones(client, r.Phone);
                return new Applied(
                    new { addresses = replaced.Select(AddressValue).ToList(), phones = replacedPhones.Select(PhoneValue).ToList() },
                    new { address = AddressValue(client.Addresses[^1]), phone = r.Phone is null ? null : PhoneValue(client.Phones[^1]) });
            }
            case ChangeKind.AddressAdded:
                // Never replaces, a c/o neither: the bank keeps the client's other addresses.
                client.Addresses.Add(NewAddress(r.Address!, today));
                return new Applied(null, new { address = AddressValue(client.Addresses[^1]) });
            case ChangeKind.PhoneAdded:
                client.Phones.Add(NewPhone(r.Phone!));
                return new Applied(null, new { phone = PhoneValue(client.Phones[^1]) });
            case ChangeKind.PhoneChanged:
            {
                var replaced = ReplacePhones(client, r.Phone!);
                return new Applied(new { phones = replaced.Select(PhoneValue).ToList() }, new { phone = PhoneValue(client.Phones[^1]) });
            }
            case ChangeKind.PhoneRemoved:
            {
                var number = NormalizeNumber(r.Phone!.Number);
                var removed = client.Phones.Where(p => p.IsCurrent && NormalizeNumber(p.Number) == number).ToList();
                if (removed.Count == 0) throw new KeyNotFoundException($"Client {client.Code} has no current phone with that number.");
                removed.ForEach(p => p.IsCurrent = false);
                return new Applied(new { phones = removed.Select(PhoneValue).ToList() }, null);
            }
            case ChangeKind.StandingOrderCreated:
            {
                var account = FindAccount(client, r.AccountIban);
                var order = r.StandingOrder!;
                account.StandingOrders.Add(new StandingOrder
                {
                    BeneficiaryAccount = order.BeneficiaryAccount.Trim(), BeneficiaryCountry = Country(order.BeneficiaryCountry), IsActive = order.IsActive,
                });
                return new Applied(null, new
                {
                    accountIban = account.Iban, beneficiaryAccount = order.BeneficiaryAccount.Trim(),
                    beneficiaryCountry = Country(order.BeneficiaryCountry), accountType = account.Type,
                });
            }
            case ChangeKind.PowerOfAttorneyGranted:
            {
                var power = r.PowerOfAttorney!;
                client.PowersOfAttorney.Add(new PowerOfAttorney
                {
                    HolderName = power.HolderName.Trim(), HolderCountry = Country(power.HolderCountry), IsActive = power.IsActive, ValidFrom = today,
                });
                return new Applied(null, new { holderCountry = Country(power.HolderCountry), isActive = power.IsActive });
            }
            case ChangeKind.PersonalDataChanged:
            {
                var old = new { birthCountry = client.BirthCountry, citizenships = client.Citizenships.ToList() };
                if (r.BirthCountry is not null) client.BirthCountry = Country(r.BirthCountry);
                if (r.Citizenships is not null) client.Citizenships = [.. r.Citizenships.Select(Country).Distinct()];
                return new Applied(old, new { birthCountry = client.BirthCountry, citizenships = client.Citizenships.ToList() });
            }
            case ChangeKind.SelfCertSubmitted:
            {
                var snapshot = r.SelfCertification!;
                var previous = client.SelfCertifications.Where(s => s.Status == SelfCertStatus.Valid).ToList();
                previous.ForEach(s => s.Status = SelfCertStatus.Superseded);
                var selfCert = new SelfCertification
                {
                    SignedAt = Utc(snapshot.SignedAt),
                    ReceivedAt = now,
                    Channel = r.Channel.Trim(),
                    DeclaresUsPerson = snapshot.DeclaresUsPerson,
                    Status = SelfCertStatus.Valid,
                    DocumentId = r.DocumentId,
                    Residences = [.. snapshot.Residences.Select(x => new TaxResidence
                    {
                        Country = Country(x.Country), Tin = string.IsNullOrWhiteSpace(x.Tin) ? null : x.Tin.Trim(),
                        NoTinReason = x.NoTinReason, NoTinExplanation = x.NoTinExplanation,
                    })],
                };
                client.SelfCertifications.Add(selfCert);
                return new Applied(
                    previous.Count == 0 ? null : new { superseded = previous.Select(SelfCertValue).ToList() },
                    new SelfCertificationValue(selfCert), selfCert);
            }
            case ChangeKind.DocumentUploaded:
                return new Applied(null, new { documentId = r.DocumentId });
            default:
                throw new ArgumentOutOfRangeException(nameof(r), r.Kind, "Unknown change kind.");
        }
    }

    /// <summary>The new value of a self-certification; its id is read when serialized (after saving).</summary>
    private sealed class SelfCertificationValue(SelfCertification s)
    {
        public int SelfCertificationId => s.Id;
        public DateTime SignedAt => s.SignedAt;
        public DateTime ReceivedAt => s.ReceivedAt;
        public bool DeclaresUsPerson => s.DeclaresUsPerson;
        public IEnumerable<object> Residences => s.Residences.Select(r => new { r.Country, r.Tin, r.NoTinReason });
    }

    private static List<Phone> ReplacePhones(Client client, PhoneSnapshot phone)
    {
        var replaced = client.Phones.Where(p => p.IsCurrent).ToList();
        replaced.ForEach(p => p.IsCurrent = false);
        client.Phones.Add(NewPhone(phone));
        return replaced;
    }

    private static Address NewAddress(AddressSnapshot a, DateOnly today) => new()
    {
        Kind = a.Kind, Line = a.Line.Trim(), City = a.City.Trim(), Country = Country(a.Country), IsCurrent = true, ValidFrom = today,
    };

    private static Phone NewPhone(PhoneSnapshot p) => new() { Number = p.Number.Trim(), Country = Country(p.Country), IsCurrent = true };

    private static Account FindAccount(Client client, string? iban)
    {
        if (iban is null)
            return client.Accounts.OrderBy(a => a.Id).FirstOrDefault() ?? throw new KeyNotFoundException($"Client {client.Code} has no account.");
        var wanted = NormalizeIban(iban);
        return client.Accounts.FirstOrDefault(a => NormalizeIban(a.Iban) == wanted)
            ?? throw new KeyNotFoundException($"Client {client.Code} has no account with that IBAN.");
    }

    private static object AddressValue(Address a) => new { kind = a.Kind, a.Line, a.City, a.Country, a.IsCurrent };
    private static object PhoneValue(Phone p) => new { p.Number, p.Country, p.IsCurrent };
    private static object SelfCertValue(SelfCertification s) => new
    {
        selfCertificationId = s.Id, s.SignedAt, residences = s.Residences.Select(r => r.Country).ToList(), status = s.Status,
    };

    /// <summary>A summary built by code: kinds and countries, never names, numbers or addresses.</summary>
    private static string Summarize(ChangeRequest r, Applied applied) => r.Kind switch
    {
        ChangeKind.AddressChanged => $"{r.Address!.Kind} address changed: now in {Country(r.Address.Country)}"
                                     + (r.Phone is null ? "" : $"; a phone in {Country(r.Phone.Country)} replaces the current phones"),
        ChangeKind.AddressAdded => $"{r.Address!.Kind} address added in {Country(r.Address.Country)}",
        ChangeKind.PhoneAdded => $"Phone added ({Country(r.Phone!.Country)})",
        ChangeKind.PhoneChanged => $"Phone changed: now {Country(r.Phone!.Country)}",
        ChangeKind.PhoneRemoved => $"Phone removed ({Country(r.Phone!.Country)})",
        ChangeKind.StandingOrderCreated => $"Standing order to an account in {Country(r.StandingOrder!.BeneficiaryCountry)}",
        ChangeKind.PowerOfAttorneyGranted => $"Power of attorney for a person in {Country(r.PowerOfAttorney!.HolderCountry)}",
        ChangeKind.PersonalDataChanged => "Personal data changed ("
            + string.Join(", ", new[] { r.BirthCountry is null ? null : "birth country", r.Citizenships is null ? null : "citizenships" }.OfType<string>()) + ")",
        ChangeKind.SelfCertSubmitted => $"New self-certification: {string.Join(", ", applied.SelfCertification!.Residences.Select(x => x.Country))}"
                                        + (r.CaseCode is null ? "" : $" (answer to {r.CaseCode})"),
        ChangeKind.DocumentUploaded => $"Document {r.DocumentId} uploaded",
        _ => r.Kind.ToString(),
    };

    // ---- The shield decides DocumentUploaded ----

    /// <summary>Hidden text gives ShieldAlert (no indicia, never a case); the explanation is a code template.</summary>
    private static RuleOutcome ShieldOutcome(ShieldResult result) => result.Verdict == ShieldVerdict.HiddenTextRemoved
        ? new RuleOutcome(Decision.ShieldAlert, [], [], [],
            $"Hidden text found and removed on {result.Hidden.Select(h => h.Page).Distinct().Count()} page(s): it never reaches the model. A person checks the document.",
            $"Text ascuns găsit și eliminat pe {result.Hidden.Select(h => h.Page).Distinct().Count()} pagină(i): nu ajunge niciodată la model. Un om verifică documentul.",
            ShieldVersion)
        : new RuleOutcome(Decision.NoCase, [], [], [],
            "Document scanned: no hidden text.", "Document scanat: fără text ascuns.", ShieldVersion);

    private static object RulePayload(int changeId, RuleHit hit, bool applied) => new
    {
        changeId, rule = hit.RuleId, regime = hit.Regime.ToString(), jurisdiction = hit.Jurisdiction, page = hit.GuidePage,
        applied, coveredBy = applied ? hit.CoveredBy.ToString() : null,
    };

    // ---- Validation ----

    private static void Validate(ChangeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ClientCode)) throw new ArgumentException("The client code is required.", nameof(r));
        if (string.IsNullOrWhiteSpace(r.Channel)) throw new ArgumentException("The channel is required.", nameof(r));
        string? missing = r.Kind switch
        {
            ChangeKind.AddressChanged or ChangeKind.AddressAdded when r.Address is null => "Address",
            ChangeKind.PhoneAdded or ChangeKind.PhoneChanged or ChangeKind.PhoneRemoved when r.Phone is null => "Phone",
            ChangeKind.StandingOrderCreated when r.StandingOrder is null => "StandingOrder",
            ChangeKind.PowerOfAttorneyGranted when r.PowerOfAttorney is null => "PowerOfAttorney",
            ChangeKind.SelfCertSubmitted when r.SelfCertification is null || r.SelfCertification.Residences.Count == 0 => "SelfCertification (with a residence)",
            ChangeKind.DocumentUploaded when r.DocumentId is null => "DocumentId",
            ChangeKind.PersonalDataChanged when r.BirthCountry is null && r.Citizenships is null => "BirthCountry or Citizenships",
            _ => null,
        };
        if (missing is not null) throw new ArgumentException($"A {r.Kind} change needs {missing}.", nameof(r));

        IEnumerable<string?> countries = r.Kind switch
        {
            ChangeKind.AddressChanged or ChangeKind.AddressAdded => [r.Address!.Country, r.Phone?.Country],
            ChangeKind.PhoneAdded or ChangeKind.PhoneChanged => [r.Phone!.Country],
            ChangeKind.StandingOrderCreated => [r.StandingOrder!.BeneficiaryCountry],
            ChangeKind.PowerOfAttorneyGranted => [r.PowerOfAttorney!.HolderCountry],
            ChangeKind.SelfCertSubmitted => r.SelfCertification!.Residences.Select(x => (string?)x.Country),
            ChangeKind.PersonalDataChanged => [r.BirthCountry, .. r.Citizenships ?? []],
            _ => [],
        };
        foreach (var country in countries.OfType<string>())
            if (Country(country) is not { Length: 2 } code || !code.All(char.IsAsciiLetterUpper))
                throw new ArgumentException($"Country '{country}' is not an ISO 3166-1 alpha-2 code.", nameof(r));
        if (r.Kind is ChangeKind.AddressChanged or ChangeKind.AddressAdded && string.IsNullOrWhiteSpace(r.Address!.Line + r.Address.City))
            throw new ArgumentException("The address is empty.", nameof(r));
        if (r.Kind is ChangeKind.PhoneAdded or ChangeKind.PhoneChanged or ChangeKind.PhoneRemoved && string.IsNullOrWhiteSpace(r.Phone!.Number))
            throw new ArgumentException("The phone number is empty.", nameof(r));
        if (r.Kind is ChangeKind.StandingOrderCreated && string.IsNullOrWhiteSpace(r.StandingOrder!.BeneficiaryAccount))
            throw new ArgumentException("The beneficiary account is empty.", nameof(r));
    }

    private static string Country(string country) => country.Trim().ToUpperInvariant();
    private static string NormalizeNumber(string number) => string.Concat(number.Where(c => char.IsDigit(c) || c == '+'));
    private static string NormalizeIban(string iban) => string.Concat(iban.Where(c => !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant));
    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public static class ChangesSetup
{
    public static IServiceCollection AddChanges(this IServiceCollection services) =>
        services.AddScoped<IChangeService, ChangeService>();
}

public class ChangeEventConfiguration : IEntityTypeConfiguration<ChangeEvent>
{
    public void Configure(EntityTypeBuilder<ChangeEvent> e)
    {
        e.Property(c => c.Kind).HasConversion<string>();
        e.HasIndex(c => c.ClientId);
        e.HasIndex(c => c.ReceivedAt);
    }
}

public class RuleEvaluationConfiguration : IEntityTypeConfiguration<RuleEvaluation>
{
    public void Configure(EntityTypeBuilder<RuleEvaluation> e)
    {
        e.Property(r => r.Decision).HasConversion<string>();
        e.HasIndex(r => r.ChangeEventId);
    }
}
