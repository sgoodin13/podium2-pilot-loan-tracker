using System.ComponentModel.DataAnnotations;

namespace Api.Dtos;

/// <summary>
/// Write-side DTOs. EF Core entities never cross the API boundary
/// (LoanTracker_Stack_Rules.md [STACK_RULES]).
/// </summary>
/// <remarks>
/// Validation is DataAnnotations plus guard clauses in the service layer
/// (Gate 3 fork B4). <c>[Required(AllowEmptyStrings = false)]</c> combined with
/// <c>MinLength(1)</c> rejects whitespace-only input once trimmed by the service.
/// </remarks>
public record CreateItemRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(FieldLimits.Name)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(FieldLimits.Description)]
    public string? Description { get; init; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Asset tag is required.")]
    [MaxLength(FieldLimits.AssetTag)]
    public string AssetTag { get; init; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    public Guid ItemCategoryId { get; init; }
}

public record UpdateItemRequest : CreateItemRequest;

public record CreateBorrowerRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(FieldLimits.Name)]
    public string Name { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(FieldLimits.Email)]
    public string? ContactEmail { get; init; }

    [MaxLength(FieldLimits.Phone)]
    public string? ContactPhone { get; init; }

    [MaxLength(FieldLimits.Department)]
    public string? Department { get; init; }
}

public record UpdateBorrowerRequest : CreateBorrowerRequest;

/// <summary>
/// The checkout request — the one real business-rule endpoint on this product.
/// </summary>
public record CheckoutRequest
{
    [Required(ErrorMessage = "A borrower is required.")]
    public Guid BorrowerId { get; init; }

    [Required(ErrorMessage = "An item is required.")]
    public Guid ItemId { get; init; }
}

/// <summary>
/// Closing a loan. The status must be terminal — a non-terminal status cannot
/// close a loan (BR §6).
/// </summary>
public record ReturnLoanRequest
{
    [Required(ErrorMessage = "A resulting status is required.")]
    public Guid LoanStatusId { get; init; }
}

public record ItemCategoryRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(FieldLimits.Name)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(FieldLimits.Description)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;
}

public record LoanStatusRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(FieldLimits.Name)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(FieldLimits.Description)]
    public string? Description { get; init; }

    public bool IsTerminal { get; init; }

    public bool IsActive { get; init; } = true;
}
