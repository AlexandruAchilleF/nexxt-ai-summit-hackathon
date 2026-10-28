using System.Text.RegularExpressions;
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
/// Phase 0 contracts (docs/PLAN.md 16 and 20): the CRS modules wire up like Program.cs, every entity maps on SQLite,
/// and the shared catalogs (rules, golden cases) keep their shape. No network, synthetic data only.
/// </summary>
public class Phase0ContractTests
{
    private static readonly Regex PageReference = new(@"^\d{1,3}(-\d{1,3})?(, \d{1,3}(-\d{1,3})?)*$");

    [Fact]
    public async Task Schema_is_created_on_sqlite_with_every_crs_table()
    {
        await using var crs = new CrsServices();
        await using var db = await crs.CreateDbAsync();

        Assert.True(await db.Database.EnsureCreatedAsync());

        Assert.Equal(0, await db.Clients.CountAsync());
        Assert.Equal(0, await db.Addresses.CountAsync());
        Assert.Equal(0, await db.Phones.CountAsync());
        Assert.Equal(0, await db.Accounts.CountAsync());
        Assert.Equal(0, await db.StandingOrders.CountAsync());
        Assert.Equal(0, await db.PowersOfAttorney.CountAsync());
        Assert.Equal(0, await db.SelfCertifications.CountAsync());
        Assert.Equal(0, await db.ClientDocuments.CountAsync());
        Assert.Equal(0, await db.ChangeEvents.CountAsync());
        Assert.Equal(0, await db.RuleEvaluations.CountAsync());
        Assert.Equal(0, await db.Cases.CountAsync());
        Assert.Equal(0, await db.Indicia.CountAsync());
        Assert.Equal(0, await db.Evidence.CountAsync());
        Assert.Equal(0, await db.ContextSignals.CountAsync());
        Assert.Equal(0, await db.ClientRequests.CountAsync());
        Assert.Equal(0, await db.ClientResponses.CountAsync());
        Assert.Equal(0, await db.ConfirmationProofs.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Every_crs_contract_resolves_from_a_scope()
    {
        await using var crs = new CrsServices();
        await using var scope = crs.Root.CreateAsyncScope();

        Type[] contracts =
        [
            typeof(IClientService), typeof(IChangeService), typeof(ICrsRuleEngine), typeof(IDeadlineCalculator),
            typeof(SimClock), typeof(ICaseService), typeof(IHiddenTextShield), typeof(IEvidenceVerifier),
            typeof(IShieldedDocuments), typeof(ITinFormat), typeof(ITinValidator), typeof(IAuditLog), typeof(IEvaluationService),
        ];
        foreach (var contract in contracts)
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(contract)); // throws with the type name if missing

        Assert.Same(crs.Root.GetRequiredService<SimClock>(), scope.ServiceProvider.GetRequiredService<SimClock>());
        Assert.Same(RuleCatalog.All, scope.ServiceProvider.GetRequiredService<ICrsRuleEngine>().Catalog);

        // The rule engine is a singleton and checks TIN formats synchronously: it may depend on ITinFormat, never on the async validator.
        Assert.Contains(typeof(CrsRuleEngine).GetConstructors().SelectMany(c => c.GetParameters()), p => p.ParameterType == typeof(ITinFormat));
        Assert.DoesNotContain(typeof(CrsRuleEngine).GetConstructors().SelectMany(c => c.GetParameters()), p => p.ParameterType == typeof(ITinValidator));

        // The audit log is scoped (Tamper checks the current user's role): one per scope, never from the root.
        await using var other = crs.Root.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        Assert.Same(audit, scope.ServiceProvider.GetRequiredService<IAuditLog>());
        Assert.NotSame(audit, other.ServiceProvider.GetRequiredService<IAuditLog>());
        Assert.Throws<InvalidOperationException>(() => crs.Root.GetRequiredService<IAuditLog>());
    }

    [Fact]
    public async Task Client_round_trips_and_the_snapshot_keeps_the_values()
    {
        await using var crs = await CrsServices.CreateAsync();
        var data = new SyntheticData(seed: 7);
        var birth = new DateOnly(1990, 5, 15);
        var cnp = data.Cnp(birth, male: false);
        var roIban = data.Iban();
        var deIban = SyntheticData.IbanWithCheckDigits("DE", "100000000000000001");
        var signedAt = new DateTime(2026, 2, 10, 9, 30, 0, DateTimeKind.Utc);
        var uploadedAt = new DateTime(2026, 2, 11, 14, 0, 0, DateTimeKind.Utc);
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D]; // "%PDF-": a few bytes, never parsed here
        var client = new Client
        {
            Code = "CL-TEST",
            FirstName = "Maria",
            LastName = "Test",
            BirthDate = birth,
            BirthCountry = "RO",
            Cnp = cnp,
            Citizenships = ["RO"],
            Addresses = [new Address { Kind = AddressKind.Residence, Line = "Str. Test nr. 1", City = "Cluj-Napoca", Country = "RO", ValidFrom = new DateOnly(2020, 1, 1) }],
            Phones = [new Phone { Number = "+40700000001", Country = "RO" }],
            Accounts =
            [
                new Account
                {
                    Iban = roIban, Type = AccountType.Custodial, Product = "Cont de custodie", OpenedOn = new DateOnly(2018, 3, 1),
                    IsPreexisting = false, Balance = 12500.50m, Currency = "RON",
                    StandingOrders = [new StandingOrder { BeneficiaryIban = deIban, BeneficiaryCountry = "DE", Amount = 250m }],
                },
            ],
            PowersOfAttorney = [new PowerOfAttorney { HolderName = "Ion Test", HolderAddress = "Teststrasse 1, Wien", HolderCountry = "AT", Scope = "Full", ValidFrom = new DateOnly(2025, 6, 1) }],
            SelfCertifications =
            [
                new SelfCertification
                {
                    SignedAt = signedAt, ReceivedAt = signedAt.AddHours(1), Channel = "branch",
                    Residences =
                    [
                        new TaxResidence { Country = "RO", Tin = cnp },
                        new TaxResidence { Country = "AT", NoTinReason = NoTinReason.A_CountryDoesNotIssue, NoTinExplanation = "Synthetic test reason" },
                    ],
                },
            ],
            Documents =
            [
                new ClientDocument
                {
                    Kind = DocumentKind.Payslip, FileName = "payslip-test.pdf", Content = pdfBytes, Pages = 1, UploadedAt = uploadedAt,
                },
            ],
        };
        await using (var db = await crs.CreateDbAsync())
        {
            db.Clients.Add(client);
            await db.SaveChangesAsync();
        }

        await using var scope = crs.Root.CreateAsyncScope();
        var clients = scope.ServiceProvider.GetRequiredService<IClientService>();

        var loaded = await clients.GetAsync("CL-TEST");
        Assert.NotNull(loaded);
        Assert.Equal(12500.50m, Assert.Single(loaded.Accounts).Balance);
        Assert.Empty(loaded.Documents); // GetAsync never loads documents (they carry the PDF bytes)

        var docs = await clients.ListDocumentsAsync("CL-TEST");
        var info = Assert.Single(docs);
        Assert.Equal(client.Id, info.ClientId);
        Assert.Equal(DocumentKind.Payslip, info.Kind);
        Assert.Equal("payslip-test.pdf", info.FileName);
        Assert.Equal(1, info.Pages);
        Assert.Equal(ShieldVerdict.NotScanned, info.ShieldVerdict);
        Assert.Equal(uploadedAt, info.UploadedAt);
        Assert.Equal(DateTimeKind.Utc, info.UploadedAt.Kind);
        Assert.DoesNotContain(typeof(DocumentInfo).GetProperties(), p => p.PropertyType == typeof(byte[]));
        Assert.Empty(await clients.ListDocumentsAsync("CL-NONE"));

        var document = await clients.GetDocumentAsync(info.Id);
        Assert.NotNull(document);
        Assert.Equal(pdfBytes, document.Content);

        var snapshot = await clients.SnapshotAsync(client.Id);
        Assert.NotNull(snapshot);
        Assert.Equal(client.Id, snapshot.ClientId);
        Assert.Equal("CL-TEST", snapshot.Code);
        Assert.Equal("Maria Test", snapshot.FullName);
        Assert.Equal(birth, snapshot.BirthDate);
        Assert.Equal("RO", snapshot.BirthCountry);
        Assert.Equal("RO", Assert.Single(snapshot.Citizenships));

        var address = Assert.Single(snapshot.Addresses);
        Assert.Equal(AddressKind.Residence, address.Kind);
        Assert.Equal("Cluj-Napoca", address.City);
        Assert.Equal("RO", address.Country);
        Assert.True(address.IsCurrent);

        var phone = Assert.Single(snapshot.Phones);
        Assert.Equal("+40700000001", phone.Number);
        Assert.Equal("RO", phone.Country);

        var account = Assert.Single(snapshot.Accounts);
        Assert.Equal(roIban, account.Iban);
        Assert.Equal(AccountType.Custodial, account.Type);
        Assert.False(account.IsPreexisting);
        var order = Assert.Single(account.StandingOrders);
        Assert.Equal(deIban, order.BeneficiaryIban);
        Assert.Equal("DE", order.BeneficiaryCountry);
        Assert.True(order.IsActive);

        var power = Assert.Single(snapshot.PowersOfAttorney);
        Assert.Equal("Ion Test", power.HolderName);
        Assert.Equal("AT", power.HolderCountry);

        var selfCert = snapshot.SelfCertification;
        Assert.NotNull(selfCert);
        Assert.Equal(signedAt, selfCert.SignedAt);
        Assert.False(selfCert.DeclaresUsPerson);
        Assert.Equal(new[] { "RO", "AT" }, selfCert.Residences.Select(r => r.Country));
        var ro = Assert.Single(selfCert.Residences, r => r.Country == "RO");
        Assert.Equal(cnp, ro.Tin);
        Assert.Equal(NoTinReason.None, ro.NoTinReason);
        var at = Assert.Single(selfCert.Residences, r => r.Country == "AT");
        Assert.Null(at.Tin);
        Assert.Equal(NoTinReason.A_CountryDoesNotIssue, at.NoTinReason);
        Assert.Equal("Synthetic test reason", at.NoTinExplanation);
    }

    [Fact]
    public async Task Case_with_proof_and_audit_entries_round_trip()
    {
        await using var crs = await CrsServices.CreateAsync();
        var at = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

        await using (var db = await crs.CreateDbAsync())
        {
            db.Cases.Add(new Case
            {
                Code = "CRS-TEST-0001", ClientId = 1, ClientCode = "CL-TEST", TriggerChangeId = 1, Regime = Regime.Crs,
                OpenedAt = at, DueAt = at.AddDays(90), RemindAt = at.AddDays(85),
                DeadlineRuleId = "DL-CHANGE-90", DeadlineGuidePage = "29, 35-36",
                Outcome = "Synthetic outcome", OutcomeRo = "Rezultat sintetic", OutcomeRuleId = "RESP-EXPLANATION",
                Indicia =
                [
                    new Indicium
                    {
                        RuleId = "CRS-IND-ADDR", Regime = Regime.Crs, Jurisdiction = "DE", GuidePage = "29", AddedAt = at,
                        Explanation = "Synthetic explanation", ExplanationRo = "Explicatie sintetica",
                    },
                ],
                Requests = [new ClientRequest { Kind = RequestKind.Initial, Body = "Synthetic request", RequestedDocuments = ["Self-certification", "Proof of address"], CreatedAt = at }],
                Proof = new ConfirmationProof
                {
                    Channel = ConfirmationChannel.Online, ConfirmedAt = at, DeclarationDatedAt = at, DeclarationReceivedAt = at,
                    EvidenceKind = ConfirmationEvidenceKind.ElectronicAcceptance, EvidenceSha256 = new string('0', 64),
                    SignedBy = "Maria Test", ApprovedBy = "user:admin",
                },
            });
            db.AuditEntries.AddRange(
                new AuditEntry { Seq = 1, At = at, Actor = "system", Kind = AuditKinds.CaseOpened, PrevHash = "GENESIS", Hash = "h1" },
                new AuditEntry { Seq = 2, At = at, Actor = "user:admin", Kind = AuditKinds.CaseClosed, PrevHash = "h1", Hash = "h2" });
            await db.SaveChangesAsync();
        }

        await using var check = await crs.CreateDbAsync();
        var saved = await check.Cases.Include(c => c.Indicia).Include(c => c.Requests).Include(c => c.Proof)
            .SingleAsync(c => c.Code == "CRS-TEST-0001");
        Assert.Equal(CaseStatus.Open, saved.Status);
        Assert.Equal("CL-TEST", saved.ClientCode);
        Assert.Equal("Synthetic outcome", saved.Outcome);
        Assert.Equal("Rezultat sintetic", saved.OutcomeRo);
        var indicium = Assert.Single(saved.Indicia);
        Assert.Equal("CRS-IND-ADDR", indicium.RuleId);
        Assert.Equal("Synthetic explanation", indicium.Explanation);
        Assert.Equal("Explicatie sintetica", indicium.ExplanationRo);
        Assert.Equal(new[] { "Self-certification", "Proof of address" }, Assert.Single(saved.Requests).RequestedDocuments);
        var proof = saved.Proof;
        Assert.NotNull(proof);
        Assert.Equal(saved.Id, proof.CaseId);
        Assert.Equal(new long[] { 1, 2 }, await check.AuditEntries.OrderBy(a => a.Seq).Select(a => a.Seq).ToListAsync());
    }

    [Fact]
    public async Task Evidence_and_signal_without_a_case_round_trip()
    {
        await using var crs = await CrsServices.CreateAsync();
        var at = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
        var client = new Client { Code = "CL-SIGNAL", FirstName = "Ana", LastName = "Test", BirthDate = new DateOnly(1992, 1, 20), Citizenships = ["RO"] };

        // Golden case 8: a salary from a German employer is a signal with no case. The contract allows it (CaseCode = null).
        var request = new NewEvidence("CL-SIGNAL", CaseCode: null, EvidenceSource.Document, "Arbeitgeber: Synthetic GmbH, Muenchen", DocumentId: 1, Page: 1);
        Assert.Null(request.CaseCode);
        Assert.Null(new NewSignal("CL-SIGNAL", CaseCode: null, "Salary from a German employer", EvidenceId: 1).CaseCode);

        await using (var db = await crs.CreateDbAsync())
        {
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            var evidence = new Evidence
            {
                ClientId = client.Id, CaseId = null, Source = request.Source, DocumentId = request.DocumentId, Page = request.Page,
                Quote = request.Quote, Verified = true, VerificationNote = "Quote found in the visible text.", RecordedBy = request.RecordedBy, At = at,
            };
            db.Evidence.Add(evidence);
            await db.SaveChangesAsync();
            db.ContextSignals.Add(new ContextSignal
            {
                ClientId = client.Id, CaseId = null, Description = "Salary from a German employer", EvidenceId = evidence.Id,
                Strength = SignalStrength.Weak, At = at,
            });
            await db.SaveChangesAsync();
        }

        await using var check = await crs.CreateDbAsync();
        var savedEvidence = await check.Evidence.SingleAsync(e => e.ClientId == client.Id);
        Assert.Null(savedEvidence.CaseId);
        Assert.Equal(EvidenceSource.Document, savedEvidence.Source);
        Assert.Equal("Arbeitgeber: Synthetic GmbH, Muenchen", savedEvidence.Quote);
        Assert.Equal(1, savedEvidence.Page);
        Assert.True(savedEvidence.Verified);
        Assert.Equal("agent", savedEvidence.RecordedBy);

        var savedSignal = await check.ContextSignals.SingleAsync(s => s.ClientId == client.Id);
        Assert.Null(savedSignal.CaseId);
        Assert.Equal(savedEvidence.Id, savedSignal.EvidenceId);
        Assert.Equal(SignalStrength.Weak, savedSignal.Strength);
        Assert.Equal("Salary from a German employer", savedSignal.Description);

        Assert.Equal(0, await check.Cases.CountAsync()); // a signal never opens a case
    }

    [Fact]
    public async Task Dates_come_back_as_utc()
    {
        await using var crs = await CrsServices.CreateAsync();
        var received = DateTime.SpecifyKind(new DateTime(2026, 3, 2, 9, 15, 30, 123), DateTimeKind.Unspecified);
        var opened = new DateTime(2026, 3, 2, 9, 16, 0, DateTimeKind.Utc);
        var closed = new DateTime(2026, 5, 20, 16, 45, 10, 500, DateTimeKind.Utc);

        await using (var db = await crs.CreateDbAsync())
        {
            db.ChangeEvents.Add(new ChangeEvent { ClientId = 1, Kind = ChangeKind.AddressChanged, Summary = "Synthetic", SourceChannel = "branch", ReceivedAt = received });
            db.Cases.Add(new Case
            {
                Code = "CRS-TEST-UTC", ClientId = 1, ClientCode = "CL-TEST", TriggerChangeId = 1, Regime = Regime.Crs, Status = CaseStatus.Closed,
                OpenedAt = opened, DueAt = opened.AddDays(90), RemindAt = opened.AddDays(85), ClosedAt = closed, ExpiredAt = null,
            });
            await db.SaveChangesAsync();
        }

        await using var check = await crs.CreateDbAsync();
        var change = await check.ChangeEvents.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, change.ReceivedAt.Kind);
        Assert.Equal(received.Ticks, change.ReceivedAt.Ticks);

        var projected = await check.ChangeEvents.Select(c => c.ReceivedAt).SingleAsync();
        Assert.Equal(DateTimeKind.Utc, projected.Kind);
        Assert.Equal(received.Ticks, projected.Ticks);

        var saved = await check.Cases.SingleAsync(c => c.Code == "CRS-TEST-UTC");
        Assert.Equal(DateTimeKind.Utc, saved.OpenedAt.Kind);
        Assert.Equal(opened.Ticks, saved.OpenedAt.Ticks);
        Assert.NotNull(saved.ClosedAt);
        Assert.Equal(DateTimeKind.Utc, saved.ClosedAt.Value.Kind);
        Assert.Equal(closed.Ticks, saved.ClosedAt.Value.Ticks);
        Assert.Null(saved.ExpiredAt);
    }

    [Fact]
    public void SimClock_moves_forward_resets_and_never_moves_back()
    {
        var start = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        var clock = new SimClock(new FixedTime(start));
        var changed = 0;
        clock.Changed += () => changed++;
        Assert.Equal(start, clock.GetUtcNow());

        clock.Advance(TimeSpan.FromDays(85));

        Assert.Equal(start.AddDays(85), clock.GetUtcNow());
        Assert.Equal(start.UtcDateTime.AddDays(85), clock.UtcNow);
        Assert.Equal(TimeSpan.FromDays(85), clock.Offset);
        Assert.Equal(1, changed);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromDays(-1)));
        Assert.Equal(start.AddDays(85), clock.GetUtcNow());
        Assert.Equal(1, changed);

        clock.Reset();

        Assert.Equal(start, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.Offset);
        Assert.Equal(2, changed);
    }

    [Fact]
    public void Rule_catalog_ids_are_unique_with_titles_and_a_guide_page()
    {
        var duplicates = RuleCatalog.All.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);

        foreach (var rule in RuleCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Id), "A rule has an empty Id");
            Assert.False(string.IsNullOrWhiteSpace(rule.Title), $"{rule.Id}: empty Title");
            Assert.False(string.IsNullOrWhiteSpace(rule.TitleRo), $"{rule.Id}: empty TitleRo");
            Assert.True(rule.GuidePage == RuleCatalog.NotVerified || PageReference.IsMatch(rule.GuidePage),
                $"{rule.Id}: GuidePage '{rule.GuidePage}' is neither a page reference (\"29\", \"36-37\", \"29, 48\") nor {RuleCatalog.NotVerified}");
        }
    }

    [Fact]
    public void Rule_catalog_pages_exist_in_the_guide()
    {
        var pagesDir = Path.Combine(RepoRoot(), "docs", "ghid", "pagini");
        var missing = new List<string>();
        foreach (var rule in RuleCatalog.All.Where(r => r.GuidePage != RuleCatalog.NotVerified && PageReference.IsMatch(r.GuidePage)))
            foreach (var part in rule.GuidePage.Split(',', StringSplitOptions.TrimEntries))
            {
                var bounds = part.Split('-');
                var from = int.Parse(bounds[0]);
                var to = bounds.Length > 1 ? int.Parse(bounds[1]) : from;
                if (to < from) { missing.Add($"{rule.Id}: range {part} is reversed"); continue; }
                for (var page = from; page <= to; page++)
                    if (!File.Exists(Path.Combine(pagesDir, $"p-{page:000}.md")))
                        missing.Add($"{rule.Id}: p-{page:000}.md");
            }
        Assert.Empty(missing);
    }

    [Fact]
    public void Golden_cases_are_the_18_cases_with_case_5_split_in_a_and_b()
    {
        string[] expected = ["1", "2", "3", "4", "5a", "5b", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18"];
        var ids = GoldenCases.All.Select(c => c.Id).ToList();

        var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), ids.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(18, ids.Select(id => id.TrimEnd('a', 'b')).Distinct().Count());
    }

    [Fact]
    public void Golden_case_rules_exist_in_the_catalog()
    {
        var known = RuleCatalog.All.Select(r => r.Id).ToHashSet();
        var unknown = GoldenCases.All
            .SelectMany(c => c.ExpectedIndicia.Concat(c.ExpectedCleared).Where(id => !known.Contains(id)).Select(id => $"case {c.Id}: {id}"))
            .ToList();
        Assert.Empty(unknown);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The CRS modules wired like Program.cs, on a temp SQLite file deleted on dispose.</summary>
    private sealed class CrsServices : IAsyncDisposable
    {
        private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"crs-phase0-{Guid.NewGuid():N}.db");

        public CrsServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbFile}"));
            services.AddScoped<CurrentUser>(); // no AuthenticationStateProvider: falls back to SiteConfig.Users[0]
            services.AddClients();
            services.AddChanges();
            services.AddRules();
            services.AddDeadlines();
            services.AddCases();
            services.AddShield();
            services.AddTin();
            services.AddAudit();
            services.AddEvaluation();
            Root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public ServiceProvider Root { get; }

        public static async Task<CrsServices> CreateAsync()
        {
            var crs = new CrsServices();
            await using var db = await crs.CreateDbAsync();
            await db.Database.EnsureCreatedAsync();
            return crs;
        }

        public Task<AppDbContext> CreateDbAsync() =>
            Root.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        public async ValueTask DisposeAsync()
        {
            await Root.DisposeAsync();
            SqliteConnection.ClearAllPools();
            try { File.Delete(_dbFile); } catch { /* best effort */ }
        }
    }
}
