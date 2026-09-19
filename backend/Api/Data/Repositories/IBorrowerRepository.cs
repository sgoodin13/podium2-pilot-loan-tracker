using Api.Data.Entities;

namespace Api.Data.Repositories;

/// <summary>
/// Persistence for <see cref="Borrower"/>. Query and persistence only —
/// business rules live in the service layer.
/// </summary>
public interface IBorrowerRepository
{
    Task<Borrower?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Borrower> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        string? search = null,
        string? department = null,
        bool includeInactive = false,
        SortRequest sort = default,
        CancellationToken cancellationToken = default);

    /// <summary>Active borrowers, ordered by name — for pick lists (the checkout wizard).</summary>
    Task<IReadOnlyList<Borrower>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>True when the borrower still holds at least one open loan.</summary>
    Task<bool> HasOpenLoansAsync(Guid borrowerId, CancellationToken cancellationToken = default);

    Task AddAsync(Borrower borrower, CancellationToken cancellationToken = default);

    void Update(Borrower borrower);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
