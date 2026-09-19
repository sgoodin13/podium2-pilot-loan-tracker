namespace Api.Data.Entities;

/// <summary>
/// A checkout of one <see cref="Item"/> to one <see cref="Borrower"/>. Maps to <c>loans</c>.
/// </summary>
/// <remarks>
/// A null <see cref="ReturnedAt"/> means the loan is OPEN. The filtered unique index
/// <c>ux_loans_item_open</c> on <c>(item_id) WHERE returned_at IS NULL</c> makes the
/// database itself refuse a second open loan on the same item, even under a race.
/// </remarks>
public class Loan : EntityBase
{
    /// <summary>The item on loan.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Navigation to the item on loan.</summary>
    public Item? Item { get; set; }

    /// <summary>The borrower holding the item.</summary>
    public Guid BorrowerId { get; set; }

    /// <summary>Navigation to the borrower holding the item.</summary>
    public Borrower? Borrower { get; set; }

    /// <summary>Current lifecycle status of the loan.</summary>
    public Guid LoanStatusId { get; set; }

    /// <summary>Navigation to the current lifecycle status.</summary>
    public LoanStatus? LoanStatus { get; set; }

    /// <summary>When the item was checked out (UTC).</summary>
    public DateTimeOffset CheckedOutAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the item came back (UTC). Null means the loan is still open.</summary>
    public DateTimeOffset? ReturnedAt { get; set; }

    /// <summary>Convenience projection of the open/closed state. Not mapped to a column.</summary>
    public bool IsOpen => ReturnedAt is null;
}
