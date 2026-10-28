using System.ComponentModel.DataAnnotations;

namespace HackathonStarter.Features.Items;

/// <summary>
/// Example entity. Copy this feature folder for your own entities (or run tools/NewFeature).
/// The validation attributes are used by the form AND checked again by <see cref="ItemService"/>.
/// </summary>
public class Item
{
    public int Id { get; set; }

    [Required, StringLength(120, MinimumLength = 2)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string Description { get; set; } = "";

    public ItemStatus Status { get; set; } = ItemStatus.Draft;
    public ItemPriority Priority { get; set; } = ItemPriority.Medium;

    [Range(0, 1_000_000_000)]
    public decimal Value { get; set; }

    public string OwnerId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Copy of the editable fields, for edit forms (so cancel doesn't change the original).</summary>
    public Item Clone() => (Item)MemberwiseClone();
}

public enum ItemStatus { Draft, Active, Archived }

public enum ItemPriority { Low, Medium, High }

public record ItemFilter(string? Search = null, ItemStatus? Status = null);

public record ItemStats(
    int Total,
    decimal TotalValue,
    IReadOnlyDictionary<ItemStatus, int> ByStatus,
    IReadOnlyDictionary<ItemPriority, int> ByPriority,
    IReadOnlyList<(DateOnly Day, int Count)> PerDay,
    IReadOnlyList<Item> Recent);
