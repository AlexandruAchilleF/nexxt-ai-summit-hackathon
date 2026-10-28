using HackathonStarter.Config;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Items;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Data;

/// <summary>
/// Resets the demo data (admin only): a clean state right before going on stage or before the next take of the video.
/// CRS: every case, change, evaluation and the audit chain go, the demo clock goes back to today, then the clients and the
/// change history are seeded again exactly as on a new database (ClientSeed + ChangeHistorySeed).
/// </summary>
public class DemoData(IDbContextFactory<AppDbContext> dbFactory, CurrentUser currentUser, ISimClock clock, DemoMaintenance maintenance, IServiceProvider services)
{
    public async Task ResetAsync()
    {
        await currentUser.Require(Roles.Admin);
        // DeadlineWatcher sits the reset out (clock.Reset() below wakes it) and ticks once when it ends, as after a new start.
        using var _ = maintenance.Pause();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            await db.AgentRuns.ExecuteDeleteAsync();
            await db.RunExports.ExecuteDeleteAsync();
            await db.Items.ExecuteDeleteAsync();
            db.Items.AddRange(ItemSeed.Create());
            // @gen:reset (add your own tables here: delete + re-seed)
            await db.SaveChangesAsync();

            // Children before parents: ExecuteDelete bypasses EF's cascade, and evidence and signals may have no case.
            await db.ConfirmationProofs.ExecuteDeleteAsync();
            await db.ClientResponses.ExecuteDeleteAsync();
            await db.ClientRequests.ExecuteDeleteAsync();
            await db.ContextSignals.ExecuteDeleteAsync();
            await db.Evidence.ExecuteDeleteAsync();
            await db.Indicia.ExecuteDeleteAsync();
            await db.Cases.ExecuteDeleteAsync();
            await db.RuleEvaluations.ExecuteDeleteAsync();
            await db.ChangeEvents.ExecuteDeleteAsync();
            // The chain starts again from GENESIS: the old one described data that no longer exists.
            await db.AuditEntries.ExecuteDeleteAsync();
            await db.AcceptedExplanations.ExecuteDeleteAsync();
            await db.StandingOrders.ExecuteDeleteAsync();
            await db.Accounts.ExecuteDeleteAsync();
            await db.Addresses.ExecuteDeleteAsync();
            await db.Phones.ExecuteDeleteAsync();
            await db.PowersOfAttorney.ExecuteDeleteAsync();
            await db.SelfCertifications.ExecuteDeleteAsync();
            await db.ClientDocuments.ExecuteDeleteAsync();
            await db.Clients.ExecuteDeleteAsync();
        }

        // No clock.changed audit entry on purpose: the chain was just wiped and must match a new database's.
        clock.Reset();
        var now = clock.UtcNow;
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.Clients.AddRange(ClientSeed.Create(now));
            await db.SaveChangesAsync();
        }
        await ChangeHistorySeed.RunAsync(services, now);
    }
}
