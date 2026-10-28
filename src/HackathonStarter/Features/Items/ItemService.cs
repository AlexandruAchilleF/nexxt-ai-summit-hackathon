using System.ComponentModel.DataAnnotations;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Features.Items;

/// <summary>
/// All data access and business rules for Items. Pages and agent tools call this service, never the
/// DbContext directly, so permissions and validation live in one place.
/// </summary>
public class ItemService(IDbContextFactory<AppDbContext> dbFactory, CurrentUser currentUser)
{
    public async Task<List<Item>> ListAsync(ItemFilter? filter = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Items.AsNoTracking().AsQueryable();

        if (filter?.Status is { } status)
            query = query.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Title.ToLower().Contains(s) || i.Description.ToLower().Contains(s));
        }

        return await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
    }

    public async Task<Item?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Item> CreateAsync(Item input)
    {
        var user = await currentUser.Require(Roles.Admin, Roles.Member);
        Validate(input);
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = input.Clone();
        item.Id = 0;
        item.OwnerId = user.Id;
        item.CreatedAt = item.UpdatedAt = DateTime.UtcNow;
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<Item> UpdateAsync(Item input)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        Validate(input);
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.Items.FindAsync(input.Id) ?? throw new KeyNotFoundException("Item not found");
        item.Title = input.Title.Trim();
        item.Description = input.Description.Trim();
        item.Status = input.Status;
        item.Priority = input.Priority;
        item.Value = input.Value;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return item;
    }

    public async Task DeleteAsync(int id)
    {
        await currentUser.Require(Roles.Admin);
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Items.Where(i => i.Id == id).ExecuteDeleteAsync();
    }

    /// <summary>Dashboard aggregates. Computed in memory: fine for hackathon-sized data, works on any database.</summary>
    public async Task<ItemStats> StatsAsync(int days = 14)
    {
        var items = await ListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var perDay = Enumerable.Range(0, days)
            .Select(i => today.AddDays(i - days + 1))
            .Select(d => (d, items.Count(x => DateOnly.FromDateTime(x.CreatedAt) == d)))
            .ToList();

        return new ItemStats(
            items.Count,
            items.Sum(i => i.Value),
            Enum.GetValues<ItemStatus>().ToDictionary(s => s, s => items.Count(i => i.Status == s)),
            Enum.GetValues<ItemPriority>().ToDictionary(p => p, p => items.Count(i => i.Priority == p)),
            perDay,
            items.Take(5).ToList());
    }

    private static void Validate(Item item)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(item, new ValidationContext(item), results, validateAllProperties: true))
            throw new ValidationException(string.Join(" ", results.Select(r => r.ErrorMessage)));
    }
}
