using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Repositories;

/// <inheritdoc cref="IItemRepository"/>
public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _db;

    public ItemRepository(AppDbContext db) => _db = db;

    public Task<Item?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Items.AsNoTracking()
            .Include(i => i.ItemCategory);

        return includeInactive
            ? query.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            : query.Where(i => i.IsActive).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Item> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        string? search = null,
        Guid? itemCategoryId = null,
        bool? onLoanOnly = null,
        bool includeInactive = false,
        SortRequest sort = default,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Items.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(i => i.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.Name, $"%{term}%") ||
                EF.Functions.ILike(i.AssetTag, $"%{term}%"));
        }

        if (itemCategoryId.HasValue)
        {
            query = query.Where(i => i.ItemCategoryId == itemCategoryId.Value);
        }

        if (onLoanOnly.HasValue)
        {
            // Availability is derived from the existence of an open loan — there is no
            // stored status column, by ruling. Translates to an EXISTS subquery.
            query = onLoanOnly.Value
                ? query.Where(i => i.Loans.Any(l => l.IsActive && l.ReturnedAt == null))
                : query.Where(i => !i.Loans.Any(l => l.IsActive && l.ReturnedAt == null));
        }

        var total = await query.CountAsync(cancellationToken);

        // Sort keys are allow-listed and resolved to typed expressions — no
        // caller-supplied string reaches SQL. An unknown key falls back to the
        // default order rather than erroring.
        // Eager-load the category before ordering: Include() returns an
        // IIncludableQueryable, which is not IOrderedQueryable, so a ThenBy after
        // it would not compile.
        var withCategory = query.Include(i => i.ItemCategory);

        var ordered = sort.Key switch
        {
            "name" => withCategory.OrderByDirection(i => i.Name, sort.Descending),
            "itemcategoryname" => withCategory.OrderByDirection(i => i.ItemCategory!.Name, sort.Descending),
            "assettag" => withCategory.OrderByDirection(i => i.AssetTag, sort.Descending),
            _ => withCategory.OrderBy(i => i.AssetTag),
        };

        var rows = await ordered
            // Id breaks ties so paging is stable across requests.
            .ThenBy(i => i.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public async Task<IReadOnlyList<Item>> GetLookupAsync(CancellationToken cancellationToken = default) =>
        await _db.Items.AsNoTracking()
            .Include(i => i.ItemCategory)
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);

    public Task<Item?> GetByAssetTagAsync(string assetTag, CancellationToken cancellationToken = default)
    {
        var term = assetTag.Trim();
        return _db.Items.AsNoTracking()
            .Include(i => i.ItemCategory)
            .FirstOrDefaultAsync(i => i.AssetTag.ToLower() == term.ToLower(), cancellationToken);
    }

    public Task<bool> AssetTagExistsAsync(string assetTag, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var term = assetTag.Trim();
        return _db.Items.AsNoTracking()
            .Where(i => excludeId == null || i.Id != excludeId)
            .AnyAsync(i => i.AssetTag.ToLower() == term.ToLower(), cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default) =>
        _db.Items.AsNoTracking()
            .AnyAsync(i => i.Id == id && (includeInactive || i.IsActive), cancellationToken);

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default) =>
        await _db.Items.AddAsync(item, cancellationToken);

    public void Update(Item item) => _db.Items.Update(item);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
