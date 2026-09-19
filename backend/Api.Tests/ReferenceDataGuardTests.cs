using Api.Data.Seed;
using Api.Dtos;
using Api.Services;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Reference data is editable by Staff, which means a maintenance screen can reach
/// the rows that checkout and return depend on. These are the guards that stop an
/// ordinary-looking edit from disabling the product (Compliance finding F2).
/// </summary>
/// <remarks>
/// Each of these was reachable from the Loan Status screen before the guard existed:
/// one checkbox or one text edit, with no confirmation, and every checkout in the
/// product starts failing with an error that misdirects the reader to seeding.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class ReferenceDataGuardTests(PostgresFixture fixture)
{
    private static LoanStatusRequest From(
        string name,
        bool isTerminal,
        bool isActive,
        string? description = null) =>
        new() { Name = name, Description = description, IsTerminal = isTerminal, IsActive = isActive };

    /// <summary>
    /// One update, one context — matching production, where every request gets its own
    /// scoped <c>DbContext</c>. Reusing a single harness across a create and an update of
    /// the same row makes EF track two instances of one key and throw, which is an
    /// artefact of the test setup rather than anything the API can hit.
    /// </summary>
    private async Task<LoanStatusResponse> UpdateStatusAsync(Guid id, LoanStatusRequest request)
    {
        await using var harness = fixture.CreateHarness();
        return await harness.Statuses.UpdateAsync(id, request);
    }

    private async Task<DomainException> RejectedUpdateAsync(Guid id, LoanStatusRequest request)
    {
        await using var harness = fixture.CreateHarness();
        return await Assert.ThrowsAsync<DomainException>(() =>
            harness.Statuses.UpdateAsync(id, request));
    }

    [Fact]
    public async Task The_checkout_status_cannot_be_renamed()
    {
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Statuses.UpdateAsync(
                checkedOut.Id,
                From("On Loan", isTerminal: false, isActive: true)));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("cannot be renamed");

        // And the row is untouched, so checkout still resolves it.
        await using var verify = fixture.CreateContext();
        var after = await verify.LoanStatuses.AsNoTracking()
            .SingleAsync(s => s.Id == SeedData.StatusIds.CheckedOut);
        after.Name.Should().Be("Checked Out");
    }

    [Fact]
    public async Task The_checkout_status_cannot_be_deactivated()
    {
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Statuses.UpdateAsync(
                checkedOut.Id,
                From("Checked Out", isTerminal: false, isActive: false)));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("cannot be deactivated");

        await using var verify = fixture.CreateContext();
        var after = await verify.LoanStatuses.AsNoTracking()
            .SingleAsync(s => s.Id == SeedData.StatusIds.CheckedOut);
        after.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// The end-to-end consequence, rather than just the guard's own message: with the
    /// guard in place, checkout still works after an attempt to disable the status.
    /// </summary>
    [Fact]
    public async Task Checkout_still_works_after_an_attempt_to_disable_the_checkout_status()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using var harness = fixture.CreateHarness();

        await Assert.ThrowsAsync<DomainException>(() =>
            harness.Statuses.UpdateAsync(
                checkedOut.Id,
                From("Checked Out", isTerminal: false, isActive: false)));

        var loan = await harness.Loans.CheckoutAsync(
            new CheckoutRequest { BorrowerId = borrower.Id, ItemId = item.Id });

        loan.IsOpen.Should().BeTrue();
        loan.LoanStatusName.Should().Be("Checked Out");
    }

    [Fact]
    public async Task The_checkout_status_cannot_be_marked_terminal()
    {
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Statuses.UpdateAsync(
                checkedOut.Id,
                From("Checked Out", isTerminal: true, isActive: true)));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("cannot be marked terminal");
    }

    [Fact]
    public async Task A_status_held_by_an_open_loan_cannot_be_deactivated()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        // A second non-terminal status, so the row under test is not the protected one.
        await using var harness = fixture.CreateHarness();
        var inspection = await harness.Statuses.CreateAsync(
            From($"Out For Inspection {TestData.Unique()}", isTerminal: false, isActive: true));

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, inspection.Id);

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Statuses.UpdateAsync(
                inspection.Id,
                From(inspection.Name, isTerminal: false, isActive: false)));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("open loan");

        // The unrelated seeded status is unaffected — the guard is not a blanket refusal.
        checkedOut.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// Losing every terminal status leaves no way to close a loan at all, which in turn
    /// leaves the item permanently blocked from retire by the hard guard.
    /// </summary>
    [Fact]
    public async Task The_last_active_terminal_status_cannot_be_deactivated()
    {
        List<LoanStatusResponse> terminals;

        await using (var harness = fixture.CreateHarness())
        {
            terminals = (await harness.Statuses.ListAsync(includeInactive: false, terminalOnly: true))
                .ToList();
        }

        terminals.Should().NotBeEmpty("the product cannot close a loan without one");

        // Deactivate all but the last, which is permitted.
        foreach (var status in terminals.SkipLast(1))
        {
            await UpdateStatusAsync(status.Id, From(status.Name, isTerminal: true, isActive: false));
        }

        var last = terminals[^1];

        var thrown = await RejectedUpdateAsync(
            last.Id,
            From(last.Name, isTerminal: true, isActive: false));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("only status that can close a loan");

        // Clearing the terminal flag is the same loss by a different route.
        var alsoThrown = await RejectedUpdateAsync(
            last.Id,
            From(last.Name, isTerminal: false, isActive: true));

        alsoThrown.StatusCode.Should().Be(422);

        // Restore, so the shared container is left as the other tests expect it.
        foreach (var status in terminals.SkipLast(1))
        {
            await UpdateStatusAsync(status.Id, From(status.Name, isTerminal: true, isActive: true));
        }
    }

    [Fact]
    public async Task An_ordinary_status_edit_is_still_allowed()
    {
        LoanStatusResponse created;

        await using (var harness = fixture.CreateHarness())
        {
            created = await harness.Statuses.CreateAsync(
                From($"Returned Needs Repair {TestData.Unique()}", isTerminal: true, isActive: true));
        }

        var renamed = $"Needs Repair {TestData.Unique()}";

        var updated = await UpdateStatusAsync(
            created.Id,
            From(renamed, isTerminal: true, isActive: true, description: "Returned but unusable"));

        updated.Name.Should().Be(renamed);
        updated.Description.Should().Be("Returned but unusable");
        updated.IsTerminal.Should().BeTrue();
    }

    /// <summary>
    /// The count that lets the UI name the effect of deactivating a category before the
    /// save, rather than writing it to a log the user never sees (Compliance finding F5).
    /// </summary>
    [Fact]
    public async Task Category_list_reports_the_active_item_count_that_references_each_row()
    {
        await using var harness = fixture.CreateHarness();

        var category = await harness.Categories.CreateAsync(
            new ItemCategoryRequest { Name = $"Survey Gear {TestData.Unique()}", IsActive = true });

        var before = (await harness.Categories.ListAsync(includeInactive: true))
            .Single(c => c.Id == category.Id);
        before.ActiveItemCount.Should().Be(0);

        await using var arrange = fixture.CreateContext();
        await TestData.AddItemAsync(arrange, category.Id);
        await TestData.AddItemAsync(arrange, category.Id);

        var after = (await harness.Categories.ListAsync(includeInactive: true))
            .Single(c => c.Id == category.Id);

        after.ActiveItemCount.Should().Be(2);
    }
}
