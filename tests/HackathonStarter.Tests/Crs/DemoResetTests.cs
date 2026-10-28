using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Audit;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs;

/// <summary>Settings → "Reset demo data": the CRS tables, the audit chain and the demo clock go back to a new database's state.</summary>
public class DemoResetTests
{
    [Fact]
    public async Task Reset_brings_back_the_startup_state_with_an_intact_chain_and_the_clock_at_today()
    {
        var file = Path.Combine(Path.GetTempPath(), $"crs-reset-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={file}"));
        services.AddScoped<CurrentUser>();
        services.AddClients();
        services.AddChanges();
        services.AddRules();
        services.AddDeadlines();
        services.AddCases();
        services.AddShield();
        services.AddTin();
        services.AddAudit();
        services.AddScoped<DemoData>();
        await using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        try
        {
            var clock = root.GetRequiredService<ISimClock>();
            var now = clock.UtcNow;
            await using (var db = await root.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
            {
                await db.Database.EnsureCreatedAsync();
                db.Clients.AddRange(ClientSeed.Create(now));
                await db.SaveChangesAsync();
            }
            await ChangeHistorySeed.RunIfEmptyAsync(root, now);
            var startup = await CountsAsync(root);

            // A demo take: a new change opens a case, the clock jumps 85 days, the watcher's tick reminds and escalates.
            await using (var scope = root.CreateAsyncScope())
            {
                scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(SiteConfig.Users[0]);
                await scope.ServiceProvider.GetRequiredService<IChangeService>().RecordAsync(new ChangeRequest("CL-0015",
                    ChangeKind.AddressChanged, "online", Address: new AddressSnapshot(AddressKind.Residence, "Calle Mayor 1", "Madrid", "ES")));
                clock.Advance(TimeSpan.FromDays(85));
                await scope.ServiceProvider.GetRequiredService<ICaseService>().TickAsync(clock.UtcNow);
            }
            Assert.NotEqual(startup, await CountsAsync(root));

            await using (var scope = root.CreateAsyncScope())
            {
                scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(SiteConfig.Users[1]); // member: refused
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<DemoData>().ResetAsync());
            }
            var maintenance = root.GetRequiredService<DemoMaintenance>();
            var pausedWhenTheClockWentBack = false;
            clock.Changed += () => pausedWhenTheClockWentBack = maintenance.IsPaused;
            await using (var scope = root.CreateAsyncScope())
            {
                scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(SiteConfig.Users[0]);
                await scope.ServiceProvider.GetRequiredService<DemoData>().ResetAsync();
                Assert.True(pausedWhenTheClockWentBack); // the watcher's wake-up from clock.Reset() is skipped
                Assert.False(maintenance.IsPaused); // and the pause ends with the reset
                Assert.Equal(TimeSpan.Zero, clock.Offset);
                Assert.Equal(startup, await CountsAsync(root));
                var verification = await scope.ServiceProvider.GetRequiredService<IAuditLog>().VerifyAsync();
                Assert.True(verification.Ok, $"broken at #{verification.FirstBrokenSeq}");
                Assert.True(verification.Checked > 0);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }

    private static async Task<(int Clients, int Changes, int Cases, int OpenCases, int Requests, int Audit)> CountsAsync(IServiceProvider root)
    {
        await using var db = await root.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        return (await db.Clients.CountAsync(), await db.ChangeEvents.CountAsync(), await db.Cases.CountAsync(),
            await db.Cases.CountAsync(c => c.Status != CaseStatus.Closed && c.Status != CaseStatus.Expired),
            await db.ClientRequests.CountAsync(), await db.AuditEntries.CountAsync());
    }
}
