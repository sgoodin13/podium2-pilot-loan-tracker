using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Repositories;

/// <inheritdoc cref="IBorrowerRepository"/>
public class BorrowerRepository : IBorrowerRepository
{
    private readonly AppDbContext _db;

    public BorrowerRepository(AppDbContext db) => _db = db;

    public Task<Borrower?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Borrowers.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        return query.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Borrower> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        string? search = null,
        string? department = null,
        bool includeInactive = false,
        SortRequest sort = default,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Borrowers.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b =>
                EF.Functions.ILike(b.Name, $"%{term}%") ||
                (b.ContactEmail != null && EF.Functions.ILike(b.ContactEmail, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var dept = department.Trim();
            query = query.Where(b => b.Department != null && b.Department.ToLower() == dept.ToLower());
        }

        var total = await query.CountAsync(cancellationToken);

        var ordered = sort.Key switch
        {
            "department" => query.OrderByDirection(b => b.Department, sort.Descending),
            "name" => query.OrderByDirection(b => b.Name, sort.Descending),
            _ => query.OrderBy(b => b.Name),
        };

        var rows = await ordered
            .ThenBy(b => b.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public async Task<IReadOnlyList<Borrower>> GetLookupAsync(CancellationToken cancellationToken = default) =>
        await _db.Borrowers.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default) =>
        _db.Borrowers.AsNoTracking()
            .AnyAsync(b => b.Id == id && (includeInactive || b.IsActive), cancellationToken);

    public Task<bool> HasOpenLoansAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        _db.Loans.AsNoTracking()
            .AnyAsync(l => l.BorrowerId == borrowerId && l.IsActive && l.ReturnedAt == null, cancellationToken);

    public async Task AddAsync(Borrower borrower, CancellationToken cancellationToken = default) =>
        await _db.Borrowers.AddAsync(borrower, cancellationToken);

    public void Update(Borrower borrower) => _db.Borrowers.Update(borrower);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
