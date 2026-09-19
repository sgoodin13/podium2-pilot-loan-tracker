using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Repositories;

/// <inheritdoc cref="ILoanRepository"/>
public class LoanRepository : ILoanRepository
{
    private readonly AppDbContext _db;

    public LoanRepository(AppDbContext db) => _db = db;

    /// <summary>Every read of a loan carries its item, borrower and status — never lazy-loaded.</summary>
    private IQueryable<Loan> WithRelated(IQueryable<Loan> query) =>
        query
            .Include(l => l.Item)!
                .ThenInclude(i => i!.ItemCategory)
            .Include(l => l.Borrower)
            .Include(l => l.LoanStatus);

    public Task<Loan?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Loans.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(l => l.IsActive);
        }

        return WithRelated(query).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public Task<Loan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithRelated(_db.Loans.AsQueryable())
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Loan> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        bool? openOnly = null,
        Guid? itemId = null,
        Guid? borrowerId = null,
        Guid? loanStatusId = null,
        string? search = null,
        bool includeInactive = false,
        SortRequest sort = default,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Loans.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(l => l.IsActive);
        }

        if (openOnly.HasValue)
        {
            // Served by idx_loans_returned_at.
            query = openOnly.Value
                ? query.Where(l => l.ReturnedAt == null)
                : query.Where(l => l.ReturnedAt != null);
        }

        if (itemId.HasValue)
        {
            query = query.Where(l => l.ItemId == itemId.Value);
        }

        if (borrowerId.HasValue)
        {
            query = query.Where(l => l.BorrowerId == borrowerId.Value);
        }

        if (loanStatusId.HasValue)
        {
            query = query.Where(l => l.LoanStatusId == loanStatusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                EF.Functions.ILike(l.Item!.Name, $"%{term}%") ||
                EF.Functions.ILike(l.Item!.AssetTag, $"%{term}%") ||
                EF.Functions.ILike(l.Borrower!.Name, $"%{term}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var related = WithRelated(query);

        var ordered = sort.Key switch
        {
            "itemname" => related.OrderByDirection(l => l.Item!.Name, sort.Descending),
            "borrowername" => related.OrderByDirection(l => l.Borrower!.Name, sort.Descending),
            "checkedoutat" => related.OrderByDirection(l => l.CheckedOutAt, sort.Descending),
            "returnedat" => related.OrderByDirection(l => l.ReturnedAt, sort.Descending),
            // Default: open loans first, then most recently checked out.
            _ => related
                .OrderBy(l => l.ReturnedAt == null ? 0 : 1)
                .ThenByDescending(l => l.CheckedOutAt),
        };

        var rows = await ordered
            .ThenBy(l => l.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public Task<Loan?> GetOpenLoanForItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        WithRelated(_db.Loans.AsNoTracking().Where(l => l.IsActive))
            .FirstOrDefaultAsync(l => l.ItemId == itemId && l.ReturnedAt == null, cancellationToken);

    public Task<bool> HasOpenLoanForItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        _db.Loans.AsNoTracking()
            .AnyAsync(l => l.ItemId == itemId && l.IsActive && l.ReturnedAt == null, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetItemIdsWithOpenLoanAsync(
        IEnumerable<Guid> itemIds,
        CancellationToken cancellationToken = default)
    {
        var ids = itemIds as IReadOnlyCollection<Guid> ?? itemIds.ToList();
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var matches = await _db.Loans.AsNoTracking()
            .Where(l => l.IsActive && l.ReturnedAt == null && ids.Contains(l.ItemId))
            .Select(l => l.ItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return matches.ToHashSet();
    }

    /// <summary>
    /// Open-loan counts for a page of borrowers, in one query.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="GetItemIdsWithOpenLoanAsync"/>. The borrower list
    /// shows an "Open loans" column, which without this would be one count query
    /// per row — the N+1 the data-layer standard forbids.
    /// Borrowers with no open loans are simply absent from the result.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, int>> GetOpenLoanCountsByBorrowerAsync(
        IEnumerable<Guid> borrowerIds,
        CancellationToken cancellationToken = default)
    {
        var ids = borrowerIds as IReadOnlyCollection<Guid> ?? borrowerIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var counts = await _db.Loans.AsNoTracking()
            .Where(l => l.IsActive && l.ReturnedAt == null && ids.Contains(l.BorrowerId))
            .GroupBy(l => l.BorrowerId)
            .Select(g => new { BorrowerId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.BorrowerId, c => c.Count);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, OpenLoanHolder>> GetOpenLoanHoldersByItemAsync(
        IEnumerable<Guid> itemIds,
        CancellationToken cancellationToken = default)
    {
        var ids = itemIds as IReadOnlyCollection<Guid> ?? itemIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, OpenLoanHolder>();
        }

        // Projected rather than materialised as entities: the list only needs the
        // holder's name and the loan id, and ux_loans_item_open guarantees at most
        // one open loan per item, so there is nothing to disambiguate.
        var holders = await _db.Loans.AsNoTracking()
            .Where(l => l.IsActive && l.ReturnedAt == null && ids.Contains(l.ItemId))
            .Select(l => new
            {
                l.ItemId,
                l.Id,
                BorrowerName = l.Borrower!.Name,
            })
            .ToListAsync(cancellationToken);

        return holders.ToDictionary(
            h => h.ItemId,
            h => new OpenLoanHolder(h.Id, h.BorrowerName));
    }

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken = default) =>
        await _db.Loans.AddAsync(loan, cancellationToken);

    public void Update(Loan loan) => _db.Loans.Update(loan);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
