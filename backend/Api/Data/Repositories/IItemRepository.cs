using Api.Data.Entities;

namespace Api.Data.Repositories;

/// <summary>
/// Persistence for <see cref="Item"/>. Query and persistence only —
/// business rules live in the service layer.
/// </summary>
public interface IItemRepository
{
    /// <summary>Single item with its category eager-loaded.</summary>
    Task<Item?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged item list, category eager-loaded (no N+1).
    /// </summary>
    /// <param name="onLoanOnly">
    /// Null = no availability filter; true = only items with an open loan;
    /// false = only items with no open loan. Availability is derived, never stored.
    /// </param>
    Task<(IReadOnlyList<Item> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        string? search = null,
        Guid? itemCategoryId = null,
        bool? onLoanOnly = null,
        bool includeInactive = false,
        SortRequest sort = default,
        CancellationToken cancellationToken = default);

    /// <summary>Active items, ordered by name — for pick lists (the checkout wizard).</summary>
    Task<IReadOnlyList<Item>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<Item?> GetByAssetTagAsync(string assetTag, CancellationToken cancellationToken = default);

    Task<bool> AssetTagExistsAsync(string assetTag, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task AddAsync(Item item, CancellationToken cancellationToken = default);

    void Update(Item item);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
