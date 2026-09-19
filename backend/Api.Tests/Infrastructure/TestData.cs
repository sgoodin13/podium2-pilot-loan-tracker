using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Synthetic fixtures. Every row carries a unique suffix so tests self-isolate —
/// no test depends on another test's leftovers or on a specific seeded row surviving.
/// </summary>
public static class TestData
{
    /// <summary>A short, collision-free suffix for names and asset tags.</summary>
    public static string Unique() => Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    public static async Task<Item> AddItemAsync(
        AppDbContext db,
        Guid categoryId,
        string? assetTag = null,
        string? name = null,
        bool isActive = true)
    {
        var suffix = Unique();

        var item = new Item
        {
            Name = name ?? $"QA Item {suffix}",
            Description = "Synthetic test fixture",
            AssetTag = assetTag ?? $"QA-{suffix}",
            ItemCategoryId = categoryId,
            IsActive = isActive,
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public static async Task<Borrower> AddBorrowerAsync(
        AppDbContext db,
        string? name = null,
        bool isActive = true)
    {
        var borrower = new Borrower
        {
            Name = name ?? $"QA Borrower {Unique()}",
            ContactEmail = "qa.fixture@example.invalid",
            ContactPhone = "555-0199",
            Department = "QA",
            IsActive = isActive,
        };

        db.Borrowers.Add(borrower);
        await db.SaveChangesAsync();
        return borrower;
    }

    /// <summary>
    /// Inserts a loan straight through EF, bypassing the service layer — used
    /// where the point of the test is the DATABASE's verdict, not the service's.
    /// </summary>
    public static Loan NewLoan(Guid itemId, Guid borrowerId, Guid statusId, DateTimeOffset? returnedAt = null) =>
        new()
        {
            ItemId = itemId,
            BorrowerId = borrowerId,
            LoanStatusId = statusId,
            CheckedOutAt = DateTimeOffset.UtcNow,
            ReturnedAt = returnedAt,
        };

    public static async Task<Loan> AddOpenLoanAsync(
        AppDbContext db,
        Guid itemId,
        Guid borrowerId,
        Guid checkedOutStatusId)
    {
        var loan = NewLoan(itemId, borrowerId, checkedOutStatusId);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return loan;
    }

    /// <summary>Closes a loan by setting returned_at — the filter's escape hatch.</summary>
    public static async Task CloseLoanAsync(AppDbContext db, Guid loanId, Guid terminalStatusId)
    {
        var loan = await db.Loans.SingleAsync(l => l.Id == loanId);
        loan.ReturnedAt = DateTimeOffset.UtcNow;
        loan.LoanStatusId = terminalStatusId;
        await db.SaveChangesAsync();
    }
}
