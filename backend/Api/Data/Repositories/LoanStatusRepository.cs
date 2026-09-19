using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.Repositories;

/// <inheritdoc cref="ILoanStatusRepository"/>
public class LoanStatusRepository : ILoanStatusRepository
{
    private readonly AppDbContext _db;

    public LoanStatusRepository(AppDbContext db) => _db = db;

    public Task<LoanStatus?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.LoanStatuses.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public Task<bool> HasOpenLoansAsync(Guid statusId, CancellationToken cancellationToken = default) =>
        _db.Loans.AsNoTracking()
            .AnyAsync(
                l => l.IsActive && l.ReturnedAt == null && l.LoanStatusId == statusId,
                cancellationToken);

    public Task<int> CountOtherActiveTerminalAsync(Guid excludeId, CancellationToken cancellationToken = default) =>
        _db.LoanStatuses.AsNoTracking()
            .CountAsync(
                s => s.IsActive && s.IsTerminal && s.Id != excludeId,
                cancellationToken);

    public Task<LoanStatus?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var term = name.Trim();
        return _db.LoanStatuses.AsNoTracking()
            .Where(s => s.IsActive)
            .FirstOrDefaultAsync(s => s.Name.ToLower() == term.ToLower(), cancellationToken);
    }

    public async Task<(IReadOnlyList<LoanStatus> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.LoanStatuses.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public async Task<IReadOnlyList<LoanStatus>> GetLookupAsync(CancellationToken cancellationToken = default) =>
        await _db.LoanStatuses.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var term = name.Trim();
        return _db.LoanStatuses.AsNoTracking()
            .Where(s => excludeId == null || s.Id != excludeId)
            .AnyAsync(s => s.Name.ToLower() == term.ToLower(), cancellationToken);
    }

    public async Task AddAsync(LoanStatus status, CancellationToken cancellationToken = default) =>
        await _db.LoanStatuses.AddAsync(status, cancellationToken);

    public void Update(LoanStatus status) => _db.LoanStatuses.Update(status);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
