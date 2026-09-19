namespace Api.Data.Entities;

/// <summary>
/// A person who can take an <see cref="Item"/> on loan. Maps to <c>borrowers</c>.
/// </summary>
public class Borrower : EntityBase
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional contact email.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Optional contact phone.</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Optional owning department.</summary>
    public string? Department { get; set; }

    /// <summary>Loan history for this borrower, open and closed.</summary>
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
