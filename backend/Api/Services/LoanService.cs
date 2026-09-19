using Api.Data;
using Api.Data.Entities;
using Api.Data.Repositories;
using Api.Dtos;
using Npgsql;

namespace Api.Services;

public interface ILoanService
{
    Task<PagedResult<LoanResponse>> ListAsync(
        PageRequest page,
        string? search,
        string? state,
        Guid? borrowerId,
        Guid? itemId,
        SortRequest sort = default,
        CancellationToken ct = default);

    Task<LoanResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<LoanResponse> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default);

    Task<LoanResponse> ReturnAsync(Guid id, ReturnLoanRequest request, CancellationToken ct = default);
}

/// <summary>
/// Loan lifecycle: checkout and return. This is the product's one real
/// business-rule surface.
/// </summary>
public class LoanService(
    ILoanRepository loans,
    IItemRepository items,
    IBorrowerRepository borrowers,
    ILoanStatusRepository statuses,
    ILogger<LoanService> logger) : ILoanService
{
    /// <summary>The non-terminal status a new loan opens in (BR §5.3).</summary>
    private const string CheckedOutStatusName = "Checked Out";

    public async Task<PagedResult<LoanResponse>> ListAsync(
        PageRequest page,
        string? search,
        string? state,
        Guid? borrowerId,
        Guid? itemId,
        SortRequest sort = default,
        CancellationToken ct = default)
    {
        bool? openOnly = state?.ToLowerInvariant() switch
        {
            "open" => true,
            "closed" => false,
            _ => null,
        };

        var (rows, total) = await loans.GetPagedAsync(
            page,
            openOnly: openOnly,
            itemId: itemId,
            borrowerId: borrowerId,
            search: search,
            sort: sort,
            cancellationToken: ct);

        return new PagedResult<LoanResponse>(
            rows.Select(LoanResponse.From).ToList(),
            total,
            page.SafePage,
            page.SafePageSize);
    }

    public async Task<LoanResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var loan = await loans.GetByIdAsync(id, cancellationToken: ct)
            ?? throw DomainException.NotFound("That loan");

        return LoanResponse.From(loan);
    }

    /// <summary>
    /// Creates a Loan — BR-1, the rule this pilot exists to prove.
    /// </summary>
    /// <remarks>
    /// There is deliberately NO "is it still available?" read before the insert.
    /// A read-then-write leaves exactly the gap the SME called out: two staff both
    /// see the item as available, both pass the check, both insert. Instead the
    /// insert is attempted and the database's filtered unique index
    /// <c>ux_loans_item_open</c> decides. One insert wins; the other raises a
    /// unique violation and is translated into the rejection the wizard renders.
    ///
    /// That is what makes the guarantee atomic and commit-time rather than a
    /// courtesy check in application code (trigger spec §4
    /// <c>[RULING: BR-1 enforcement]</c> — "not negotiable at Gate 3").
    /// </remarks>
    public async Task<LoanResponse> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken ct = default)
    {
        var borrower = await borrowers.GetByIdAsync(request.BorrowerId, cancellationToken: ct)
            ?? throw DomainException.NotFound("That borrower");

        if (!borrower.IsActive)
        {
            throw DomainException.Unprocessable(
                $"{borrower.Name} is deactivated and cannot borrow items.");
        }

        var item = await items.GetByIdAsync(request.ItemId, cancellationToken: ct)
            ?? throw DomainException.NotFound("That item");

        if (!item.IsActive)
        {
            throw DomainException.Unprocessable(
                $"{item.Name} ({item.AssetTag}) is retired and cannot be checked out.");
        }

        var checkedOut = await statuses.GetByNameAsync(CheckedOutStatusName, ct)
            ?? throw DomainException.Unprocessable(
                $"The '{CheckedOutStatusName}' loan status is missing. Reference data must be seeded before checkout.");

        var loan = new Loan
        {
            ItemId = item.Id,
            BorrowerId = borrower.Id,
            LoanStatusId = checkedOut.Id,
            CheckedOutAt = DateTimeOffset.UtcNow,
            ReturnedAt = null,
        };

        await loans.AddAsync(loan, ct);

        try
        {
            await loans.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsOpenLoanConflict(ex))
        {
            // The blocked path. Nothing was persisted — the insert failed
            // atomically, so there is no partial Loan to clean up.
            logger.LogWarning(
                "Checkout rejected by {Constraint}: item {AssetTag} ({ItemId}) already has an open loan",
                DatabaseConstraintNames.OpenLoanPerItem,
                item.AssetTag,
                item.Id);

            throw DomainException.Conflict(
                $"{item.Name} ({item.AssetTag}) was just checked out to another borrower. "
                + "Nothing was saved. Pick a different item to continue.");
        }

        logger.LogInformation(
            "Item checked out: {AssetTag} ({ItemId}) to {BorrowerName} ({BorrowerId}), loan {LoanId}",
            item.AssetTag,
            item.Id,
            borrower.Name,
            borrower.Id,
            loan.Id);

        // Re-read so the response carries the eager-loaded navigation properties.
        var created = await loans.GetByIdAsync(loan.Id, cancellationToken: ct)
            ?? throw DomainException.NotFound("The new loan");

        return LoanResponse.From(created);
    }

    /// <summary>
    /// Closes a loan. Requires a terminal status — a non-terminal status cannot
    /// close a loan (BR §6). "Is terminal" is maintained reference data, so Staff
    /// can add a further terminal status without a code change.
    /// </summary>
    public async Task<LoanResponse> ReturnAsync(
        Guid id,
        ReturnLoanRequest request,
        CancellationToken ct = default)
    {
        var loan = await loans.GetForUpdateAsync(id, ct)
            ?? throw DomainException.NotFound("That loan");

        if (loan.ReturnedAt is not null)
        {
            throw DomainException.Unprocessable(
                "That loan is already closed and cannot be returned again.");
        }

        var status = await statuses.GetByIdAsync(request.LoanStatusId, cancellationToken: ct)
            ?? throw DomainException.NotFound("That loan status");

        if (!status.IsTerminal)
        {
            throw DomainException.Unprocessable(
                $"'{status.Name}' is not a terminal status, so it cannot close a loan. "
                + "Choose a terminal status such as Returned, Lost or Damaged.");
        }

        if (!status.IsActive)
        {
            throw DomainException.Unprocessable(
                $"'{status.Name}' is deactivated and cannot be used to close a loan.");
        }

        loan.ReturnedAt = DateTimeOffset.UtcNow;
        loan.LoanStatusId = status.Id;

        loans.Update(loan);
        await loans.SaveChangesAsync(ct);

        logger.LogInformation(
            "Loan returned: loan {LoanId} closed with status {StatusName}",
            loan.Id,
            status.Name);

        var updated = await loans.GetByIdAsync(loan.Id, cancellationToken: ct)
            ?? throw DomainException.NotFound("That loan");

        return LoanResponse.From(updated);
    }

    /// <summary>
    /// True when the exception is the BR-1 filtered unique index rejecting a
    /// second open loan. Matched on the constraint name, never on message text.
    /// </summary>
    private static bool IsOpenLoanConflict(Exception ex)
    {
        var postgres = ex as PostgresException ?? ex.InnerException as PostgresException;

        return postgres is not null
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == DatabaseConstraintNames.OpenLoanPerItem;
    }
}
