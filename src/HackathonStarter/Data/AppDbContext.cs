using HackathonStarter.Agents;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Exports;
using HackathonStarter.Features.Items;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Data;

/// <summary>
/// EF Core database. SQLite file by default (zero setup), PostgreSQL if Database:Provider = "Postgres".
/// Hackathon rule: no migrations. Change the model, delete app.db (or keep RecreateOnStartup = true in
/// Development) and restart: the schema is recreated and demo data re-seeded.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<AgentRunRecord> AgentRuns => Set<AgentRunRecord>();
    public DbSet<RunExport> RunExports => Set<RunExport>();
    /// <summary>Encryption keys (cookies, sealed approvals), kept as long as the data they protect.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // CRS Sentinel. Each module configures its entities in its own folder (IEntityTypeConfiguration<T>).
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Phone> Phones => Set<Phone>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<StandingOrder> StandingOrders => Set<StandingOrder>();
    public DbSet<PowerOfAttorney> PowersOfAttorney => Set<PowerOfAttorney>();
    public DbSet<SelfCertification> SelfCertifications => Set<SelfCertification>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<ChangeEvent> ChangeEvents => Set<ChangeEvent>();
    public DbSet<RuleEvaluation> RuleEvaluations => Set<RuleEvaluation>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Indicium> Indicia => Set<Indicium>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<ContextSignal> ContextSignals => Set<ContextSignal>();
    public DbSet<ClientRequest> ClientRequests => Set<ClientRequest>();
    public DbSet<ClientResponse> ClientResponses => Set<ClientResponse>();
    public DbSet<ConfirmationProof> ConfirmationProofs => Set<ConfirmationProof>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    // @gen:dbsets (the feature generator inserts new tables above this line)

    /// <summary>
    /// Every DateTime is UTC in the database and comes back with Kind = Utc (SQLite returns Unspecified; Npgsql refuses to
    /// write Unspecified into timestamptz). The audit hash and the deadlines rely on it.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder conventions)
    {
        conventions.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        conventions.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        model.Entity<Item>(e =>
        {
            e.Property(i => i.Status).HasConversion<string>();
            e.Property(i => i.Priority).HasConversion<string>();
            e.HasIndex(i => i.Status);
        });

        model.Entity<AgentRunRecord>(e =>
        {
            e.HasIndex(r => r.CreatedAt);
            e.HasIndex(r => r.ConversationId);
        });

        model.Entity<RunExport>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.RunId);
            e.HasIndex(x => x.CreatedAt);
        });
    }
}

public static class DatabaseSetup
{
    public static void AddAppDatabase(this WebApplicationBuilder builder)
    {
        var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
        var connection = builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db";

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(connection);
            else
                options.UseSqlite(connection);
        });
    }

    /// <summary>Creates the schema if needed and seeds demo data into empty tables.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        if (app.Configuration.GetValue<bool>("Database:RecreateOnStartup"))
            await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        // Single-instance app: a run still "Running" in the database lost its process in the last restart.
        var interrupted = await RunStore.MarkInterruptedAsync(db);
        if (interrupted > 0)
            app.Logger.LogWarning("Marked {Count} run(s) interrupted by the last restart as failed", interrupted);

        if (!await db.Items.AnyAsync())
        {
            db.Items.AddRange(ItemSeed.Create());
            await db.SaveChangesAsync();
        }
        if (!await db.Clients.AnyAsync())
        {
            db.Clients.AddRange(ClientSeed.Create(app.Services.GetRequiredService<Features.Deadlines.SimClock>().UtcNow));
            await db.SaveChangesAsync();
        }
        // @gen:seed (the feature generator inserts new seeds above this line)
    }
}

public sealed class UtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
    v => ToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
{
    internal static DateTime ToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    };
}

public sealed class UtcNullableDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? UtcDateTimeConverter.ToUtc(v.Value) : v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
