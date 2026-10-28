using HackathonStarter.Data;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Audit;

/// <summary>
/// A change and its case share one context, transaction and audit writer lock. Nested calls borrow the context;
/// only the outer operation commits. The case writer must be acquired before entering this operation.
/// </summary>
internal static class AuditOperation
{
    internal static readonly SemaphoreSlim Writer = new(1, 1);
    private static readonly AsyncLocal<AppDbContext?> Ambient = new();
    internal static AppDbContext? Current => Ambient.Value;

    internal static async Task<T> RunAsync<T>(IDbContextFactory<AppDbContext> factory, Func<AppDbContext, Task<T>> work)
    {
        if (Current is { } current) return await work(current);
        await Writer.WaitAsync();
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();
            Ambient.Value = db;
            try
            {
                var result = await work(db);
                await transaction.CommitAsync();
                return result;
            }
            finally { Ambient.Value = null; }
        }
        finally { Writer.Release(); }
    }
}
