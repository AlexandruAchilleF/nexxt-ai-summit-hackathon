using HackathonStarter.Agents;
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
    // @gen:dbsets (the feature generator inserts new tables above this line)

    protected override void OnModelCreating(ModelBuilder model)
    {
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
        // @gen:seed (the feature generator inserts new seeds above this line)
    }
}
