using Api.Data.Entities;

namespace Api.Data.Repositories;

/// <summary>
/// Persistence for <see cref="ItemCategory"/>. Query and persistence only —
/// business rules live in the service layer.
/// </summary>
public interface IItemCategoryRepository
{
    Task<ItemCategory?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ItemCategory> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Active categories, ordered by name — for pick lists.</summary>
    Task<IReadOnlyList<ItemCategory>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> HasActiveItemsAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task AddAsync(ItemCategory category, CancellationToken cancellationToken = default);

    void Update(ItemCategory category);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
