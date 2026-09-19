namespace Api.Data.Repositories;

/// <summary>
/// Who currently holds an item, and under which loan.
/// </summary>
/// <remarks>
/// Returned by <see cref="ILoanRepository.GetOpenLoanHoldersByItemAsync"/> so the
/// item list can name the current borrower for a whole page in one query rather
/// than one per row.
/// </remarks>
/// <param name="LoanId">The open loan.</param>
/// <param name="BorrowerName">The borrower currently holding the item.</param>
public readonly record struct OpenLoanHolder(Guid LoanId, string BorrowerName);
