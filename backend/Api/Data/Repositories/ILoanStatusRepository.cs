using Api.Data.Entities;

namespace Api.Data.Repositories;

/// <summary>
/// Persistence for <see cref="LoanStatus"/>. Query and persistence only —
/// business rules live in the service layer.
/// </summary>
public interface ILoanStatusRepository
{
    Task<LoanStatus?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive lookup by name — lets the service layer resolve "Checked Out" / "Returned".</summary>
    Task<LoanStatus?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LoanStatus> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Active statuses, ordered by name — for pick lists.</summary>
    Task<IReadOnlyList<LoanStatus>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this status is the current status of at least one open loan.
    /// Deactivating such a status would strand those loans (Compliance finding F2).
    /// </summary>
    Task<bool> HasOpenLoansAsync(Guid statusId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of active terminal statuses other than <paramref name="excludeId"/> — used to
    /// refuse the edit that would leave no way to close a loan.
    /// </summary>
    Task<int> CountOtherActiveTerminalAsync(Guid excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(LoanStatus status, CancellationToken cancellationToken = default);

    void Update(LoanStatus status);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
