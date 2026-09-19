using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Repositories;

/// <inheritdoc cref="IItemCategoryRepository"/>
public class ItemCategoryRepository : IItemCategoryRepository
{
    private readonly AppDbContext _db;

    public ItemCategoryRepository(AppDbContext db) => _db = db;

    public Task<ItemCategory?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.ItemCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<ItemCategory> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        string? search = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ItemCategories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{term}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public async Task<IReadOnlyList<ItemCategory>> GetLookupAsync(CancellationToken cancellationToken = default) =>
        await _db.ItemCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var term = name.Trim();
        return _db.ItemCategories.AsNoTracking()
            .Where(c => excludeId == null || c.Id != excludeId)
            .AnyAsync(c => c.Name.ToLower() == term.ToLower(), cancellationToken);
    }

    public Task<bool> HasActiveItemsAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        _db.Items.AsNoTracking()
            .AnyAsync(i => i.ItemCategoryId == categoryId && i.IsActive, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetActiveItemCountsAsync(
        IEnumerable<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        var ids = categoryIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // One grouped query for the whole page, not one per row.
        var counts = await _db.Items.AsNoTracking()
            .Where(i => i.IsActive && ids.Contains(i.ItemCategoryId))
            .GroupBy(i => i.ItemCategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.CategoryId, c => c.Count);
    }

    public async Task AddAsync(ItemCategory category, CancellationToken cancellationToken = default) =>
        await _db.ItemCategories.AddAsync(category, cancellationToken);

    public void Update(ItemCategory category) => _db.ItemCategories.Update(category);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
