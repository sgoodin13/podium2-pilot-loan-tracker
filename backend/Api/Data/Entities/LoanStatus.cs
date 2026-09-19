namespace Api.Data.Entities;

/// <summary>
/// Reference table describing the state of a <see cref="Loan"/>.
/// Maps to <c>loan_statuses</c>.
/// </summary>
public class LoanStatus : EntityBase
{
    /// <summary>Display name. Unique across the table.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// True when the status ends the loan lifecycle (Returned / Lost / Damaged).
    /// False for the single open status (Checked Out).
    /// </summary>
    public bool IsTerminal { get; set; }

    /// <summary>Loans currently carrying this status.</summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
