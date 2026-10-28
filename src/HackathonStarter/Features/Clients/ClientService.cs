using HackathonStarter.Data;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Clients;

public interface IClientService
{
    Task<List<Client>> ListAsync(string? search = null);
    /// <summary>The client with addresses, phones, accounts (with standing orders), powers of attorney and self-certifications. No documents.</summary>
    Task<Client?> GetAsync(string code);
    /// <summary>Document metadata only (no bytes), for lists and the agent's listDocuments tool.</summary>
    Task<List<DocumentInfo>> ListDocumentsAsync(string clientCode);
    Task<ClientSnapshot?> SnapshotAsync(int clientId);
    /// <summary>The document with its bytes (for the shield). Null if it does not exist.</summary>
    Task<ClientDocument?> GetDocumentAsync(int documentId);
}

/// <summary>Read side of the client data. Changes to a client go through IChangeService, never directly. Owner: R5 (data), R2 (changes).</summary>
public class ClientService(IDbContextFactory<AppDbContext> dbFactory) : IClientService
{
    public async Task<List<Client>> ListAsync(string? search = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(s) || c.FirstName.ToLower().Contains(s) || c.LastName.ToLower().Contains(s));
        }
        return await query.OrderBy(c => c.Code).ToListAsync();
    }

    public async Task<Client?> GetAsync(string code)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Full(db).FirstOrDefaultAsync(c => c.Code == code);
    }

    public async Task<List<DocumentInfo>> ListDocumentsAsync(string clientCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ClientDocuments.AsNoTracking()
            .Where(d => db.Clients.Any(c => c.Id == d.ClientId && c.Code == clientCode))
            .OrderBy(d => d.Id)
            .Select(d => new DocumentInfo(d.Id, d.ClientId, d.Kind, d.FileName, d.Pages, d.ShieldVerdict, d.UploadedAt))
            .ToListAsync();
    }

    public async Task<ClientSnapshot?> SnapshotAsync(int clientId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var client = await Full(db).FirstOrDefaultAsync(c => c.Id == clientId);
        return client is null ? null : ClientSnapshot.From(client);
    }

    public async Task<ClientDocument?> GetDocumentAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ClientDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId);
    }

    /// <summary>The client data the rules look at (documents are loaded separately: they carry the PDF bytes).</summary>
    public static IQueryable<Client> Full(AppDbContext db) => db.Clients.AsNoTracking().AsSplitQuery()
        .Include(c => c.Addresses)
        .Include(c => c.Phones)
        .Include(c => c.Accounts).ThenInclude(a => a.StandingOrders)
        .Include(c => c.PowersOfAttorney)
        .Include(c => c.SelfCertifications)
        .Include(c => c.AcceptedExplanations);
}

public static class ClientsSetup
{
    public static IServiceCollection AddClients(this IServiceCollection services) =>
        services.AddScoped<IClientService, ClientService>();
}
