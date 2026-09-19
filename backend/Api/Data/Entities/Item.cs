namespace Api.Data.Entities;

/// <summary>
/// A loanable piece of equipment. Maps to <c>items</c>.
/// </summary>
/// <remarks>
/// Deliberately carries no stored availability/status column — availability is derived
/// from whether an open <see cref="Loan"/> (one with a null <c>ReturnedAt</c>) exists,
/// per the approved Logical and Physical Data Models.
/// </remarks>
public class Item : EntityBase
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Physical asset tag. Unique across the table.</summary>
    public string AssetTag { get; set; } = string.Empty;

    /// <summary>Owning category.</summary>
    public Guid ItemCategoryId { get; set; }

    /// <summary>Navigation to the owning category.</summary>
    public ItemCategory? ItemCategory { get; set; }

    /// <summary>Loan history for this item, open and closed.</summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
