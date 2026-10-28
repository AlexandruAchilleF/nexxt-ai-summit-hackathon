using System.Text.Json;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// R2's test host: the CRS modules wired like Program.cs on a temp SQLite file, a simulated clock over a fixed real time
/// (GOLDEN-CASES convention: 2026-10-28T07:00:00Z), a scripted rule engine (the real one is still a placeholder), a
/// recording audit log and a shield fake that returns the visible text set by the test. Synthetic data only.
/// </summary>
internal sealed class R2WorkflowHost : IAsyncDisposable
{
    public static readonly DateTime Base = Utc("2026-10-28T07:00:00");
    public static DemoUser Admin => SiteConfig.Users.Single(u => u.Role == Roles.Admin);
    public static DemoUser Member => SiteConfig.Users.Single(u => u.Role == Roles.Member);
    public static DemoUser Viewer => SiteConfig.Users.Single(u => u.Role == Roles.Viewer);

    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"crs-r2-{Guid.NewGuid():N}.db");
    private readonly List<AsyncServiceScope> _scopes = [];
    private readonly SyntheticData _data = new(seed: 2);

    public ManualTime Time { get; } = new(Base);
    public SimClock Clock { get; }
    public ScriptedRuleEngine Engine { get; } = new();
    public RecordingAudit Audit { get; } = new();
    public Dictionary<int, ShieldResult> ShieldResults { get; } = [];
    public ServiceProvider Root { get; }

    private R2WorkflowHost(Action<IServiceCollection>? configure)
    {
        Clock = new SimClock(Time);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbFile}"));
        services.AddScoped<CurrentUser>();
        services.AddClients();
        services.AddChanges();
        services.AddRules();
        services.AddDeadlines();
        services.AddCases();
        services.AddShield();
        services.AddTin();
        services.AddAudit();
        services.AddEvaluation();
        // Replacements (the last registration wins).
        services.AddSingleton<ISimClock>(Clock);
        services.AddSingleton<ICrsRuleEngine>(Engine);
        services.AddScoped<IAuditLog>(_ => Audit);
        services.AddScoped<IShieldedDocuments>(sp => new FakeShieldedDocuments(sp.GetRequiredService<IClientService>(), ShieldResults));
        configure?.Invoke(services);
        Root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public static async Task<R2WorkflowHost> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var host = new R2WorkflowHost(configure);
        await using var db = await host.DbAsync();
        await db.Database.EnsureCreatedAsync();
        return host;
    }

    public Task<AppDbContext> DbAsync() => Root.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    /// <summary>Services in a scope signed in as <paramref name="user"/> (the scope is disposed with the host).</summary>
    public Session As(DemoUser user)
    {
        var scope = Root.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(user);
        _scopes.Add(scope);
        return new Session(scope.ServiceProvider);
    }

    /// <summary>Sets the simulated now with no offset (the real time of the fixture moves, like a test of another day).</summary>
    public void SetNow(DateTime utc)
    {
        Clock.Reset();
        Time.Now = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    /// <summary>The demo way: the offset grows ("+85 days").</summary>
    public void AdvanceDays(int days) => Clock.Advance(TimeSpan.FromDays(days));
    public void Advance(TimeSpan by) => Clock.Advance(by);

    /// <summary>A standard RO resident (GOLDEN-CASES section 1) with one depository account and a valid [RO] self-certification.</summary>
    public async Task<Client> SeedClientAsync(string code, DateOnly accountOpenedOn, params DateOnly[] moreAccounts)
    {
        var birth = new DateOnly(1986, 6, 14);
        var cnp = _data.Cnp(birth, male: false);
        var signedAt = accountOpenedOn.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        var client = new Client
        {
            Code = code, FirstName = "Irina", LastName = "Test" + code[^2..], BirthDate = birth, BirthCountry = "RO", Cnp = cnp,
            Citizenships = ["RO"],
            Addresses = [new Address { Kind = AddressKind.Residence, Line = "Str. Sintetica nr. 7", City = "Bucuresti", Country = "RO", ValidFrom = accountOpenedOn }],
            Phones = [new Phone { Number = "+40723655190", Country = "RO" }],
            Accounts =
            [
                .. new[] { accountOpenedOn }.Concat(moreAccounts).Select(d => new Account
                {
                    Iban = _data.Iban(), Type = AccountType.Depository, Product = "Cont curent", OpenedOn = d, Balance = 1000m, Currency = "RON",
                }),
            ],
            SelfCertifications =
            [
                new SelfCertification
                {
                    SignedAt = signedAt, ReceivedAt = signedAt, Channel = "branch",
                    Residences = [new TaxResidence { Country = "RO", Tin = cnp }],
                },
            ],
        };
        await using var db = await DbAsync();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    public async Task<int> AddDocumentAsync(int clientId, string visibleText, DocumentKind kind = DocumentKind.Letter, string fileName = "document-test.pdf")
    {
        var content = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 synthetic " + Guid.NewGuid());
        var document = new ClientDocument { ClientId = clientId, Kind = kind, FileName = fileName, Content = content, Pages = 1, UploadedAt = Clock.UtcNow };
        await using var db = await DbAsync();
        db.ClientDocuments.Add(document);
        await db.SaveChangesAsync();
        ShieldResults[document.Id] = new ShieldResult(ShieldVerdict.Clean, [new ShieldedPage(1, visibleText)], []);
        return document.Id;
    }

    /// <summary>An AddressChanged to a residence in <paramref name="country"/>, recorded by the member (the scripted engine sees it).</summary>
    public Task<ChangeResult> MoveAsync(string clientCode, string country, DemoUser? by = null) =>
        As(by ?? Member).Changes.RecordAsync(new ChangeRequest(clientCode, ChangeKind.AddressChanged, "online",
            Address: new AddressSnapshot(AddressKind.Residence, "Calle Sintetica 120", "Madrid", country)));

    /// <summary>Change, then the operator approves the initial request: the case waits for the client (AwaitingClient).</summary>
    public async Task<string> AwaitingClientAsync(string clientCode, string country)
    {
        var change = await MoveAsync(clientCode, country);
        var code = change.CaseCode ?? throw new InvalidOperationException("No case was opened.");
        var draft = await As(Member).Cases.DraftRequestAsync(code, "ro", "Buna ziua, va rugam sa ne trimiteti documentele.");
        await As(Admin).Cases.ApproveRequestAsync(code, draft.Id, "email");
        return code;
    }

    /// <summary>A new self-certification saved through IChangeService (the owner of client data), linked to the case.</summary>
    public async Task<int> SubmitSelfCertificationAsync(string clientCode, string? caseCode, DateTime signedAt, int? documentId, params string[] countries)
    {
        var result = await As(Member).Changes.RecordAsync(new ChangeRequest(clientCode, ChangeKind.SelfCertSubmitted, "online",
            SelfCertification: new SelfCertificationSnapshot(signedAt, [.. countries.Select(c => new TaxResidenceSnapshot(c, c == "DE" ? "86095174223" : null,
                c == "DE" ? NoTinReason.None : NoTinReason.B_UnableToObtain, c == "DE" ? null : "Synthetic reason"))], DeclaresUsPerson: false),
            DocumentId: documentId, CaseCode: caseCode));
        await using var db = await DbAsync();
        var clientId = await db.Clients.Where(c => c.Code == clientCode).Select(c => c.Id).SingleAsync();
        return await db.SelfCertifications.Where(s => s.ClientId == clientId && s.Status == SelfCertStatus.Valid).Select(s => s.Id).SingleAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes) await scope.DisposeAsync();
        await Root.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbFile); } catch { /* best effort */ }
    }

    public static DateTime Utc(string iso) => DateTime.SpecifyKind(DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
}

internal sealed class Session(IServiceProvider services)
{
    public ICaseService Cases => services.GetRequiredService<ICaseService>();
    public IChangeService Changes => services.GetRequiredService<IChangeService>();
    public SimClockControl ClockControl => services.GetRequiredService<SimClockControl>();
    public IClientService Clients => services.GetRequiredService<IClientService>();
}

internal sealed class ManualTime(DateTime start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(DateTime.SpecifyKind(start, DateTimeKind.Utc));
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// The rule engine for workflow tests (R2 never writes CRS rules): by default a tiny stand-in that knows two rules, enough to
/// drive cases. CRS-IND-ADDR for each current residence outside RO, FATCA-IND-CIT for a US citizen. Coverage: a self-
/// certification declaring the jurisdiction (reportable) or an accepted explanation (not reportable). Tests can override.
/// </summary>
internal sealed class ScriptedRuleEngine : ICrsRuleEngine
{
    public const string Version = "test-engine";

    public Func<ClientSnapshot, ClientSnapshot, ChangeKind, RuleOutcome>? OnEvaluate { get; set; }
    public Func<ClientSnapshot, RuleOutcome>? OnEvaluateCurrent { get; set; }
    public Func<ClientSnapshot, SelfCertificationSnapshot, RuleOutcome>? OnEvaluateSelfCertification { get; set; }
    public List<ClientSnapshot> CurrentCalls { get; } = [];
    public List<(ClientSnapshot Before, ClientSnapshot After, ChangeKind Kind)> ChangeCalls { get; } = [];
    public List<(ClientSnapshot Client, SelfCertificationSnapshot SelfCertification)> SelfCertCalls { get; } = [];

    public IReadOnlyList<RuleDefinition> Catalog => RuleCatalog.All;

    public RuleOutcome Evaluate(ClientSnapshot before, ClientSnapshot after, ChangeKind kind)
    {
        lock (ChangeCalls) ChangeCalls.Add((before, after, kind));
        return OnEvaluate?.Invoke(before, after, kind) ?? Rules(after);
    }

    public RuleOutcome EvaluateCurrent(ClientSnapshot client)
    {
        lock (CurrentCalls) CurrentCalls.Add(client);
        return OnEvaluateCurrent?.Invoke(client) ?? Rules(client);
    }

    public RuleOutcome EvaluateSelfCertification(ClientSnapshot client, SelfCertificationSnapshot selfCertification)
    {
        lock (SelfCertCalls) SelfCertCalls.Add((client, selfCertification));
        return OnEvaluateSelfCertification?.Invoke(client, selfCertification) ?? Rules(client);
    }

    public static RuleOutcome Rules(ClientSnapshot c)
    {
        var declared = c.SelfCertification?.Residences.Select(r => r.Country).ToHashSet() ?? [];
        var explained = c.AcceptedExplanations.Select(e => e.Jurisdiction).ToHashSet();
        var hits = c.CurrentAddresses.Where(a => a.Kind == AddressKind.Residence && a.Country != "RO").Select(a => a.Country).Distinct()
            .Select(j => Hit("CRS-IND-ADDR", j, declared.Contains(j) ? Coverage.SelfCertification : explained.Contains(j) ? Coverage.AcceptedExplanation : Coverage.None))
            .ToList();
        if (c.Citizenships.Contains("US"))
            hits.Add(Hit("FATCA-IND-CIT", "US", c.SelfCertification?.DeclaresUsPerson == true ? Coverage.SelfCertification : Coverage.None));
        return Outcome(hits);
    }

    public static RuleHit Hit(string ruleId, string jurisdiction, Coverage coveredBy = Coverage.None)
    {
        var rule = RuleCatalog.Get(ruleId);
        return new RuleHit(ruleId, rule.Regime, jurisdiction, $"{rule.Title} ({jurisdiction})", $"{rule.TitleRo} ({jurisdiction})",
            rule.GuidePage, ruleId.Contains("ADDR") ? "Addresses/Residence" : "Data", coveredBy);
    }

    public static RuleOutcome Outcome(IReadOnlyList<RuleHit> hits, IReadOnlyList<RuleHit>? cleared = null)
    {
        var open = hits.Where(h => h.CoveredBy == Coverage.None).ToList();
        var decision = open.Any(h => h.Regime == Regime.Fatca) ? Decision.FatcaAlert
            : open.Count > 0 ? Decision.Clarification
            : hits.Count > 0 ? Decision.Compliant
            : Decision.NoCase;
        var reportable = hits.Where(h => h.CoveredBy == Coverage.SelfCertification).Select(h => h.Jurisdiction).Distinct().Order().ToList();
        return new RuleOutcome(decision, hits, cleared ?? [], reportable, $"Test outcome: {decision}", $"Rezultat de test: {decision}", Version);
    }
}

/// <summary>Keeps every entry in memory with a continuous Seq and a non-empty hash (the real hash chain is R3's).</summary>
internal sealed class RecordingAudit : IAuditLog
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries { get { lock (_entries) return [.. _entries]; } }
    public IEnumerable<AuditEntry> OfKind(string kind) => Entries.Where(e => e.Kind == kind);

    /// <summary>An entry kind whose append throws (a failing audit store), or null.</summary>
    public string? FailOn { get; set; }

    public Task<AuditEntry> AppendAsync(string actor, string kind, object? payload = null, int? caseId = null)
    {
        if (kind == FailOn) throw new InvalidOperationException($"Synthetic audit failure on {kind}.");
        lock (_entries)
        {
            var entry = new AuditEntry
            {
                Seq = _entries.Count + 1, Actor = actor, Kind = kind, CaseId = caseId, PayloadJson = JsonSerializer.Serialize(payload),
                PrevHash = _entries.Count == 0 ? "GENESIS" : _entries[^1].Hash, Hash = $"h{_entries.Count + 1}",
            };
            _entries.Add(entry);
            return Task.FromResult(entry);
        }
    }

    public Task<List<AuditEntry>> ListAsync(int? caseId = null, int take = 200) =>
        Task.FromResult(Entries.Where(e => caseId is null || e.CaseId == caseId).Take(take).ToList());

    public Task<AuditVerification> VerifyAsync() => Task.FromResult(new AuditVerification(true, Entries.Count, null, null, null));
    public Task TamperAsync(long seq) => Task.CompletedTask;
}

/// <summary>The shield for tests: the document comes from the database (owner, file), the visible text from the test.</summary>
internal sealed class FakeShieldedDocuments(IClientService clients, Dictionary<int, ShieldResult> results) : IShieldedDocuments
{
    public async Task<ShieldedDocument?> GetAsync(int documentId)
    {
        var document = await clients.GetDocumentAsync(documentId);
        if (document is null) return null;
        var result = results.GetValueOrDefault(documentId) ?? new ShieldResult(ShieldVerdict.Clean, [], []);
        return new ShieldedDocument(document.Id, document.ClientId, document.FileName, document.Kind, result);
    }
}
