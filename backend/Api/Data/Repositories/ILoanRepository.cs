using Api.Data.Entities;

namespace Api.Data.Repositories;

/// <summary>
/// Persistence for <see cref="Loan"/>. Query and persistence only —
/// the checkout/return rules live in the service layer, and the "one open loan per item"
/// rule is enforced by the database itself via <c>ux_loans_item_open</c>.
/// </summary>
public interface ILoanRepository
{
    /// <summary>Single loan, read-only, with item, borrower and status eager-loaded.</summary>
    Task<Loan?> GetByIdAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Single loan as a TRACKED entity, for the service layer to mutate (e.g. returning it).
    /// Related data is eager-loaded so a response can be rendered without a second round trip.
    /// </summary>
    Task<Loan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged loan list with item, borrower and status eager-loaded (no N+1).
    /// </summary>
    /// <param name="openOnly">Null = all; true = open loans only; false = closed loans only.</param>
    Task<(IReadOnlyList<Loan> Items, int TotalCount)> GetPagedAsync(
        PageRequest page,
        bool? openOnly = null,
        Guid? itemId = null,
        Guid? borrowerId = null,
        Guid? loanStatusId = null,
        string? search = null,
        bool includeInactive = false,
        SortRequest sort = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The open loan for an item, if one exists — read-only, related data eager-loaded.
    /// </summary>
    Task<Loan?> GetOpenLoanForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>True when the item currently has an open loan.</summary>
    Task<bool> HasOpenLoanForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Of the supplied item ids, those that currently have an open loan.
    /// One query for a whole page of items — this is how the item list derives availability
    /// without an N+1 per row.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetItemIdsWithOpenLoanAsync(
        IEnumerable<Guid> itemIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Open-loan counts for a page of borrowers, in one query — keeps the borrower
    /// list's "Open loans" column off the N+1 path.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetOpenLoanCountsByBorrowerAsync(
        IEnumerable<Guid> borrowerIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(Loan loan, CancellationToken cancellationToken = default);

    void Update(Loan loan);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
