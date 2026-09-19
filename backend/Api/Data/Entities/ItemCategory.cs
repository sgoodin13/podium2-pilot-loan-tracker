namespace Api.Data.Entities;

/// <summary>
/// Reference table classifying <see cref="Item"/> rows (Power Tools, AV Equipment, ...).
/// Maps to <c>item_categories</c>.
/// </summary>
public class ItemCategory : EntityBase
{
    /// <summary>Display name. Unique across the table.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Items classified under this category.</summary>
    public ICollection<Item> Items { get; set; } = new List<Item>();
}
