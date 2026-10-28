using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Clients;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Sentinel.Screens;

/// <summary>
/// Read-only query to get the latest (most recent) valid self-certification for a client.
/// Used by the UI when registering a response after recording a SelfCertSubmitted change.
/// Owned by R4 (Sentinel screens).
/// </summary>
public interface IGetLatestSelfCertificationQuery
{
    /// <summary>Returns the ID of the latest valid self-certification for the given client code, or null if none exists.</summary>
    Task<int?> GetLatestIdAsync(string clientCode);
}

public class GetLatestSelfCertificationQuery(
    IDbContextFactory<AppDbContext> dbFactory,
    CurrentUser currentUser) : IGetLatestSelfCertificationQuery
{
    public async Task<int?> GetLatestIdAsync(string clientCode)
    {
        // Any role can read (the UI shows what the role is authorized to see).
        await currentUser.Require(Roles.Admin, Roles.Member, Roles.Viewer);

        await using var db = await dbFactory.CreateDbContextAsync();
        var result = await db.Clients
            .Where(c => c.Code == clientCode)
            .SelectMany(c => c.SelfCertifications.Where(s => s.Status == SelfCertStatus.Valid))
            .OrderByDescending(s => s.ReceivedAt)
            .ThenByDescending(s => s.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        return result;
    }
}
