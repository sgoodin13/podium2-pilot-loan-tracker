using Api.Data.Entities;

namespace Api.Dtos;

/// <summary>Server-side pagination envelope — every list endpoint returns this.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public record ItemCategoryResponse(Guid Id, string Name, string? Description, bool IsActive)
{
    public static ItemCategoryResponse From(ItemCategory c) =>
        new(c.Id, c.Name, c.Description, c.IsActive);
}

public record LoanStatusResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsTerminal,
    bool IsActive)
{
    public static LoanStatusResponse From(LoanStatus s) =>
        new(s.Id, s.Name, s.Description, s.IsTerminal, s.IsActive);
}

/// <summary>
/// An item, with its availability derived rather than stored.
/// </summary>
/// <remarks>
/// <see cref="IsOnLoan"/> is computed from whether an open loan exists — the SME
/// ruling in BR §8 is explicit that availability must never be a stored status
/// field that can drift from the loan ledger.
/// </remarks>
public record ItemResponse(
    Guid Id,
    string Name,
    string? Description,
    string AssetTag,
    Guid ItemCategoryId,
    string ItemCategoryName,
    bool IsActive,
    bool IsOnLoan,
    string? CurrentBorrowerName,
    Guid? CurrentLoanId)
{
    public static ItemResponse From(Item item, Loan? openLoan) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.AssetTag,
            item.ItemCategoryId,
            item.ItemCategory?.Name ?? string.Empty,
            item.IsActive,
            openLoan is not null,
            openLoan?.Borrower?.Name,
            openLoan?.Id);

    /// <summary>Projection for a page of items where only the open/closed fact is known.</summary>
    public static ItemResponse From(Item item, bool isOnLoan) =>
        new(
            item.Id,
            item.Name,
            item.Description,
            item.AssetTag,
            item.ItemCategoryId,
            item.ItemCategory?.Name ?? string.Empty,
            item.IsActive,
            isOnLoan,
            null,
            null);
}

public record BorrowerResponse(
    Guid Id,
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? Department,
    bool IsActive,
    int OpenLoanCount)
{
    public static BorrowerResponse From(Borrower b, int openLoanCount) =>
        new(
            b.Id,
            b.Name,
            b.ContactEmail,
            b.ContactPhone,
            b.Department,
            b.IsActive,
            openLoanCount);
}

public record LoanResponse(
    Guid Id,
    Guid ItemId,
    string ItemName,
    string ItemAssetTag,
    Guid BorrowerId,
    string BorrowerName,
    Guid LoanStatusId,
    string LoanStatusName,
    bool LoanStatusIsTerminal,
    DateTimeOffset CheckedOutAt,
    DateTimeOffset? ReturnedAt,
    bool IsOpen)
{
    public static LoanResponse From(Loan loan) =>
        new(
            loan.Id,
            loan.ItemId,
            loan.Item?.Name ?? string.Empty,
            loan.Item?.AssetTag ?? string.Empty,
            loan.BorrowerId,
            loan.Borrower?.Name ?? string.Empty,
            loan.LoanStatusId,
            loan.LoanStatus?.Name ?? string.Empty,
            loan.LoanStatus?.IsTerminal ?? false,
            loan.CheckedOutAt,
            loan.ReturnedAt,
            loan.ReturnedAt is null);
}
