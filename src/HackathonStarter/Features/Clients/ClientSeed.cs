namespace HackathonStarter.Features.Clients;

/// <summary>
/// Demo clients (synthetic, Features/Synthetic). Owner: R5: Maria, Andrei and one client per golden case, plus the 50
/// generated ones. Empty in phase 0; AppDbContext seeds whatever this returns into an empty table.
/// </summary>
public static class ClientSeed
{
    /// <param name="now">The demo date (SimClock) at startup: seed dates relative to it, so open cases stay open on any day.</param>
    public static List<Client> Create(DateTime now) => [];
}
