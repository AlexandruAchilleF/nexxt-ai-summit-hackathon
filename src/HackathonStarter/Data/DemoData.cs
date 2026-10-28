using HackathonStarter.Config;
using HackathonStarter.Features.Items;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Data;

/// <summary>Resets the demo data (admin only): a clean state right before going on stage.</summary>
public class DemoData(IDbContextFactory<AppDbContext> dbFactory, CurrentUser currentUser)
{
    public async Task ResetAsync()
    {
        await currentUser.Require(Roles.Admin);
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.AgentRuns.ExecuteDeleteAsync();
        await db.RunExports.ExecuteDeleteAsync();
        await db.Items.ExecuteDeleteAsync();
        db.Items.AddRange(ItemSeed.Create());
        // @gen:reset (add your own tables here: delete + re-seed)
        await db.SaveChangesAsync();
    }
}
