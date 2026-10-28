using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HackathonStarter.Data;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// The demo seed (ClientSeed): the golden clients go into the database exactly as on their cards (docs/GOLDEN-CASES.md),
/// with their documents, and the filler tops the table up to DemoClientCount without touching a golden name or code.
/// </summary>
public class SeedTests
{
    private static readonly DateTime Now = new(2027, 1, 21, 7, 0, 0, DateTimeKind.Utc);

    private static IEnumerable<string> GoldenCodes =>
        GoldenCases.All.Where(c => c.HasInput).Select(c => c.ClientCode).Distinct().Order(StringComparer.Ordinal);

    [Fact]
    public async Task Maria_and_Andrei_are_in_the_database_exactly_as_on_their_cards()
    {
        await using var db = await SeededDb.CreateAsync();
        var service = new ClientService(db.Factory);

        var maria = await service.GetAsync("CL-0001");
        Assert.NotNull(maria);
        Assert.Equal(("Maria", "Popescu", new DateOnly(1991, 6, 8), "RO", "2910608400319"),
            (maria.FirstName, maria.LastName, maria.BirthDate, maria.BirthCountry, maria.Cnp));
        Assert.Equal(["RO"], maria.Citizenships);
        var home = Assert.Single(maria.Addresses);
        Assert.Equal((AddressKind.Residence, "Bd. Unirii 15, bl. 7, ap. 22", "030823 București", "RO", true),
            (home.Kind, home.Line, home.City, home.Country, home.IsCurrent));
        var phone = Assert.Single(maria.Phones);
        Assert.Equal(("+40721384516", "RO", true), (phone.Number, phone.Country, phone.IsCurrent));
        var account = Assert.Single(maria.Accounts);
        Assert.Equal(("RO19RZBR0000060021458391", AccountType.Depository, "Cont curent", new DateOnly(2019, 3, 14), 18_450.00m, "RON"),
            (account.Iban, account.Type, account.Product, account.OpenedOn, account.Balance, account.Currency));
        Assert.Empty(account.StandingOrders);
        Assert.False(Preexisting.For(Regime.Crs, account.OpenedOn));
        Assert.False(Preexisting.For(Regime.Fatca, account.OpenedOn));
        var selfCert = Assert.Single(maria.SelfCertifications);
        // The card says "la ghișeu"; in code the channel is "branch".
        Assert.Equal((new DateTime(2019, 3, 14, 0, 0, 0, DateTimeKind.Utc), "branch", SelfCertStatus.Valid, false),
            (selfCert.SignedAt, selfCert.Channel, selfCert.Status, selfCert.DeclaresUsPerson));
        var residence = Assert.Single(selfCert.Residences);
        Assert.Equal(("RO", "2910608400319"), (residence.Country, residence.Tin));
        Assert.Empty(maria.PowersOfAttorney);
        Assert.Empty(maria.AcceptedExplanations);

        var andrei = await service.GetAsync("CL-0013");
        Assert.NotNull(andrei);
        Assert.Equal(("Andrei", "Pop", new DateOnly(1995, 9, 23), "RO", "1950923350125"),
            (andrei.FirstName, andrei.LastName, andrei.BirthDate, andrei.BirthCountry, andrei.Cnp));
        home = Assert.Single(andrei.Addresses);
        Assert.Equal(("Str. Mărășești 6, ap. 3", "300086 Timișoara", "RO", true), (home.Line, home.City, home.Country, home.IsCurrent));
        // Before case 13: only the RO phone; the German SIM is the change.
        phone = Assert.Single(andrei.Phones);
        Assert.Equal(("+40745210963", "RO", true), (phone.Number, phone.Country, phone.IsCurrent));
        account = Assert.Single(andrei.Accounts);
        Assert.Equal(("RO20RZBR0000060033317205", AccountType.Depository, "Cont curent", new DateOnly(2021, 4, 6), "RON"),
            (account.Iban, account.Type, account.Product, account.OpenedOn, account.Currency));
        residence = Assert.Single(Assert.Single(andrei.SelfCertifications).Residences);
        Assert.Equal(("RO", "1950923350125"), (residence.Country, residence.Tin));

        // The same snapshot the golden cases run on (ClientSnapshot.ClientId included).
        Assert.Equal(Json(GoldenCases.Get("4").Before!), Json((await service.SnapshotAsync(1))!));
        Assert.Equal(Json(GoldenCases.Get("13").Before!), Json((await service.SnapshotAsync(13))!));
    }

    [Fact]
    public async Task Every_golden_client_with_input_is_seeded_once_and_round_trips_unchanged()
    {
        await using var db = await SeededDb.CreateAsync();
        var service = new ClientService(db.Factory);
        await using var ctx = await db.Factory.CreateDbContextAsync();

        var golden = await ctx.Clients.Where(c => c.Code.StartsWith("CL-00")).OrderBy(c => c.Code).Select(c => c.Code).ToListAsync();
        Assert.Equal(GoldenCodes, golden);
        Assert.Contains("CL-0001", golden);
        Assert.Contains("CL-0013", golden);

        foreach (var code in golden)
        {
            var expected = ClientSnapshot.From(GoldenClients.Create(code));
            Assert.Equal(int.Parse(code[3..]), expected.ClientId);
            Assert.Equal(Json(expected), Json((await service.SnapshotAsync(expected.ClientId))!));
            var cases = GoldenCases.All.Where(c => c.ClientCode == code && c.HasInput).ToList();
            // Case 1 starts where case 15 ends (Ioana already in Iași): the seed holds the first state.
            Assert.Contains(cases, c => Json(c.Before!) == Json(expected));
            Assert.All(cases, c => Assert.True(Json(c.Before!) == Json(expected) || cases.Any(o => o.Id != c.Id && Json(o.After!) == Json(c.Before!)),
                $"case {c.Id}: Before is neither the seeded client nor another case's After"));
        }
    }

    [Fact]
    public async Task Golden_clients_get_their_case_documents_but_not_the_answers()
    {
        await using var db = await SeededDb.CreateAsync();
        var service = new ClientService(db.Factory);

        var maria = Assert.Single(await service.ListDocumentsAsync("CL-0001"));
        Assert.Equal((SyntheticPdfs.PayslipMaria, DocumentKind.Payslip, 1, ShieldVerdict.Clean, Now),
            (maria.FileName, maria.Kind, maria.Pages, maria.ShieldVerdict, maria.UploadedAt));
        var bytes = (await service.GetDocumentAsync(maria.Id))!;
        Assert.Equal(SyntheticPdfs.Get(SyntheticPdfs.PayslipMaria).Bytes, bytes.Content);
        Assert.Equal("application/pdf", bytes.MediaType);

        foreach (var code in GoldenCodes)
        {
            var cases = GoldenCases.All.Where(c => c.ClientCode == code).ToList();
            var listed = cases.SelectMany(c => c.Documents).ToList();
            Assert.All(listed, d =>
            {
                Assert.Contains(d.FileName, SyntheticPdfs.FileNames);
                Assert.Equal(code, SyntheticPdfs.Get(d.FileName).ClientCode);
            });
            var seeded = await service.ListDocumentsAsync(code);
            Assert.Equal(listed.Select(d => d.FileName).Distinct().Order(), seeded.Select(d => d.FileName).Order());
            var uploads = cases.Where(c => c.Change?.Kind == ChangeKind.DocumentUploaded).SelectMany(c => c.Documents).Select(d => d.FileName).ToList();
            Assert.All(seeded, d =>
            {
                var card = listed.First(l => l.FileName == d.FileName);
                Assert.Equal(card.Kind, d.Kind);
                if (card.Pages is { } pages) Assert.Equal(pages, d.Pages);
                // The change itself is scanned by ChangeService when the demo sends it; the rest are scanned at seed.
                var verdict = uploads.Contains(d.FileName)
                    ? ShieldVerdict.NotScanned
                    : new HiddenTextShield().Scan(SyntheticPdfs.Get(d.FileName).Bytes).Verdict;
                Assert.Equal(verdict, d.ShieldVerdict);
            });
            // The answer arrives during the demo (run 2), so it is not on file at the start.
            Assert.All(cases.Where(c => c.Response?.Document is not null),
                c => Assert.DoesNotContain(c.Response!.Document!.FileName, seeded.Select(d => d.FileName)));
        }
        Assert.DoesNotContain(SyntheticPdfs.SelfCertMaria, (await service.ListDocumentsAsync("CL-0001")).Select(d => d.FileName));
    }

    [Fact]
    public async Task The_database_holds_at_most_DemoClientCount_clients()
    {
        await using var db = await SeededDb.CreateAsync();
        await using var ctx = await db.Factory.CreateDbContextAsync();

        var codes = await ctx.Clients.Select(c => c.Code).ToListAsync();
        Assert.True(codes.Count <= ClientSeed.DemoClientCount, $"{codes.Count} clients, more than {ClientSeed.DemoClientCount}");
        Assert.Equal(ClientSeed.DemoClientCount, codes.Count); // the filler tops the golden clients up
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Filler_codes_and_names_stay_clear_of_the_golden_and_synthetic_clients()
    {
        var filler = ClientSeed.Filler(ClientSeed.DemoClientCount);
        var synthetic = SyntheticClients.All.Select(c => c.Code).ToHashSet();
        var goldenNames = new[]
        {
            "Maria Popescu", "Cristina Dobre", "Bogdan Lazăr", "Radu Ionescu", "Alexandra Vlad", "Marius Stoica", "Andrei Pop",
            "Sorin Matei", "Victor Neagu", "Doina Cazacu", "Petru Sârbu", "Lucian Toma", "Ioana Mureșan", "Irina Constantin",
            "Gheorghe Marin", "Teodora Iliescu",
        }.Concat(GoldenClients.Codes.Select(c => GoldenClients.Create(c).FullName))
            .SelectMany(n => n.Split(' ')).Select(TextMatch.Normalize).ToHashSet();

        Assert.Equal(filler.Count, filler.Select(c => c.FullName).Distinct().Count());
        Assert.All(filler, c =>
        {
            Assert.Matches(@"^CL-2\d{3}$", c.Code);
            Assert.Equal(int.Parse(c.Code[3..]), c.Id);
            Assert.DoesNotContain(c.Code, synthetic);
            Assert.DoesNotContain(c.Code, GoldenCases.All.Select(g => g.ClientCode));
            foreach (var name in new[] { c.FirstName, c.LastName }.Select(TextMatch.Normalize))
                Assert.DoesNotContain(goldenNames, g => g.Contains(name) || name.Contains(g));
        });
    }

    [Fact]
    public void Filler_streets_are_not_on_any_golden_card()
    {
        var golden = GoldenClients.Codes.SelectMany(c => GoldenClients.Create(c).Addresses).Select(a => a.Line)
            .Concat(GoldenCases.All.SelectMany(c => new[] { c.Before, c.After }).OfType<ClientSnapshot>().SelectMany(s => s.Addresses).Select(a => a.Line))
            .Concat(GoldenCases.All.Select(c => c.Change?.Address?.Line).OfType<string>())
            .Select(TextMatch.Normalize).Distinct().ToList();
        var streets = ClientSeed.StreetNames.ToList();
        Assert.Contains(golden, l => l.StartsWith("bd. unirii ", StringComparison.Ordinal)); // Maria, so the list is the cards'

        // Without the kind ("Republicii" of "Bd. Republicii"), so "Str. Republicii" on a card counts too.
        Assert.All(streets, street =>
        {
            var name = TextMatch.Normalize(street[(street.IndexOf(' ') + 1)..]);
            Assert.DoesNotContain(golden, line => line.Contains(name, StringComparison.Ordinal));
        });
        Assert.All(ClientSeed.Filler(ClientSeed.DemoClientCount),
            c => Assert.Contains(streets, s => c.Addresses[0].Line.StartsWith(s + " ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Filler_throws_when_asked_for_more_names_than_SyntheticData_has()
    {
        // SyntheticData has 24 first and 16 last names: at most 384 full names, before the golden ones are left out.
        var error = Assert.Throws<InvalidOperationException>(() => ClientSeed.Filler(1_000));
        Assert.Contains("1000 clients asked", error.Message);
    }

    [Fact]
    public void Filler_clients_are_standard_ro_residents_with_valid_identifiers()
    {
        Assert.All(ClientSeed.Filler(ClientSeed.DemoClientCount), c =>
        {
            Assert.Equal("RO", c.BirthCountry);
            Assert.Equal(["RO"], c.Citizenships);
            Assert.True(PiiVault.IsValidCnp(c.Cnp!), $"{c.Code}: CNP {c.Cnp}");
            Assert.Equal(c.BirthDate.ToString("yyMMdd"), c.Cnp![1..7]);

            var home = Assert.Single(c.Addresses);
            Assert.Equal((AddressKind.Residence, "RO", true), (home.Kind, home.Country, home.IsCurrent));
            Assert.Matches(@"^\d{6} \S", home.City);
            var phone = Assert.Single(c.Phones);
            Assert.Matches(@"^\+407\d{8}$", phone.Number);
            Assert.Equal("RO", phone.Country);

            Assert.InRange(c.Accounts.Count, 1, 2);
            Assert.Equal("Cont curent", c.Accounts[0].Product);
            Assert.All(c.Accounts, a =>
            {
                Assert.Equal(SyntheticData.IbanWithCheckDigits("RO", a.Iban[4..]), a.Iban);
                Assert.Equal((AccountType.Depository, "RON"), (a.Type, a.Currency));
                Assert.False(Preexisting.For(Regime.Crs, a.OpenedOn), $"{c.Code}: account opened {a.OpenedOn}");
                Assert.True(a.OpenedOn >= c.BirthDate.AddYears(18), $"{c.Code}: account before 18");
                Assert.True(a.Balance > 0);
                Assert.Empty(a.StandingOrders);
            });

            var selfCert = Assert.Single(c.SelfCertifications);
            Assert.Equal((SelfCertStatus.Valid, false, DateTimeKind.Utc), (selfCert.Status, selfCert.DeclaresUsPerson, selfCert.SignedAt.Kind));
            Assert.Equal(c.Accounts[0].OpenedOn, DateOnly.FromDateTime(selfCert.SignedAt));
            var residence = Assert.Single(selfCert.Residences);
            Assert.Equal(("RO", c.Cnp), (residence.Country, residence.Tin));

            Assert.Empty(c.PowersOfAttorney);
            Assert.Empty(c.Documents);
            Assert.Empty(c.AcceptedExplanations);
        });
    }

    [Fact]
    public void Seed_is_deterministic_and_the_filler_is_one_fixed_sequence()
    {
        Assert.Equal(Json(ClientSeed.Create(Now)), Json(ClientSeed.Create(Now)));
        Assert.Equal(Json(ClientSeed.Filler(5)), Json(ClientSeed.Filler(20).Take(5)));
        Assert.Empty(ClientSeed.Filler(0));
        Assert.Equal(GoldenCodes, ClientSeed.Create(Now).Take(GoldenCodes.Count()).Select(c => c.Code));
        Assert.NotSame(ClientSeed.Create(Now)[0], ClientSeed.Create(Now)[0]);
    }

    // SQLite gives a decimal back with its trailing zeros dropped (18450.00m as 18450.0m): equal, but not in JSON.
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new DecimalByValue() } };

    private sealed class DecimalByValue : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("0.############################", CultureInfo.InvariantCulture));
    }

    /// <summary>A temp SQLite file seeded the way AppDbContext.InitializeDatabaseAsync does it.</summary>
    private sealed class SeededDb : IAsyncDisposable
    {
        private readonly string _file = Path.Combine(Path.GetTempPath(), $"crs-seed-{Guid.NewGuid():N}.db");

        private SeededDb() => Factory = new Factory(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_file}").Options);

        public IDbContextFactory<AppDbContext> Factory { get; }

        public static async Task<SeededDb> CreateAsync()
        {
            var seeded = new SeededDb();
            await using var db = await seeded.Factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
            db.Clients.AddRange(ClientSeed.Create(Now));
            await db.SaveChangesAsync();
            return seeded;
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(_file); } catch { /* best effort */ }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Factory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
