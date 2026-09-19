using Api.Dtos;
using Api.Services;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// The checkout and return rules, exercised through the real service over a real database.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LoanServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task CheckoutAsync_creates_an_open_loan_for_an_available_item()
    {
        var category = await fixture.CategoryAsync();

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using var harness = fixture.CreateHarness();
        var loan = await harness.Loans.CheckoutAsync(
            new CheckoutRequest { BorrowerId = borrower.Id, ItemId = item.Id });

        loan.ItemAssetTag.Should().Be(item.AssetTag);
        loan.BorrowerName.Should().Be(borrower.Name);
        loan.LoanStatusName.Should().Be("Checked Out");
        loan.LoanStatusIsTerminal.Should().BeFalse();
        loan.ReturnedAt.Should().BeNull();
        loan.IsOpen.Should().BeTrue();
    }

    /// <summary>
    /// The translation step: the database's 23505 on ux_loans_item_open becomes a
    /// DomainException carrying HTTP 409 and a detail that NAMES the item and its asset
    /// tag — "a drill" is not accountable, "Cordless Drill (PT-1001)" is (SME, BR §8).
    /// </summary>
    [Fact]
    public async Task CheckoutAsync_translates_the_unique_violation_into_a_409_naming_the_item_and_asset_tag()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var holder = await TestData.AddBorrowerAsync(arrange);
        var latecomer = await TestData.AddBorrowerAsync(arrange);
        await TestData.AddOpenLoanAsync(arrange, item.Id, holder.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.CheckoutAsync(
                new CheckoutRequest { BorrowerId = latecomer.Id, ItemId = item.Id }));

        thrown.StatusCode.Should().Be(409);
        thrown.Title.Should().Be("Conflict");
        thrown.Detail.Should().Contain(item.Name);
        thrown.Detail.Should().Contain(item.AssetTag);
        thrown.Detail.Should().Contain("Nothing was saved");

        // Nothing persisted — the insert failed atomically.
        await using var verify = fixture.CreateContext();
        var loansForItem = await verify.Loans.CountAsync(l => l.ItemId == item.Id);
        loansForItem.Should().Be(1);
    }

    /// <summary>
    /// DEFECT D2 — documented, not papered over.
    /// </summary>
    /// <remarks>
    /// The retire guard states the real reason (was DEFECT D2, now fixed).
    /// </remarks>
    /// <remarks>
    /// The guard was originally unreachable: the item was fetched with the default
    /// <c>includeInactive: false</c>, so a retired item was filtered out one line before
    /// its own guard and the caller got a misleading 404. The lookup now passes
    /// <c>includeInactive: true</c> so the 422 branch can fire.
    /// </remarks>
    [Fact]
    public async Task CheckoutAsync_rejects_a_retired_item_with_a_422_naming_the_item_as_retired()
    {
        var category = await fixture.CategoryAsync();

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id, isActive: false);
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.CheckoutAsync(
                new CheckoutRequest { BorrowerId = borrower.Id, ItemId = item.Id }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain(
            "retired",
            "a retired item must state the real reason, not a misleading 'not found'");
        thrown.Detail.Should().Contain(item.AssetTag, "the reason names the specific item instance");
    }

    /// <summary>
    /// The deactivated-borrower guard — the exact mirror of the retired-item case above.
    /// </summary>
    /// <remarks>
    /// Was DEFECT D2: "<c>{name} is deactivated and cannot borrow items.</c>" could never
    /// fire because <c>borrowers.GetByIdAsync</c> excluded inactive rows by default.
    /// </remarks>
    [Fact]
    public async Task CheckoutAsync_rejects_a_deactivated_borrower_with_a_422_naming_the_borrower()
    {
        var category = await fixture.CategoryAsync();

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange, isActive: false);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.CheckoutAsync(
                new CheckoutRequest { BorrowerId = borrower.Id, ItemId = item.Id }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain(
            "deactivated",
            "a deactivated borrower must state the real reason, not a misleading 'not found'");
        thrown.Detail.Should().Contain(borrower.Name, "the reason names the specific borrower");
    }

    [Fact]
    public async Task CheckoutAsync_answers_404_for_an_unknown_borrower()
    {
        var category = await fixture.CategoryAsync();

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.CheckoutAsync(
                new CheckoutRequest { BorrowerId = Guid.NewGuid(), ItemId = item.Id }));

        thrown.StatusCode.Should().Be(404);
        thrown.Detail.Should().Contain("borrower");
    }

    [Fact]
    public async Task CheckoutAsync_answers_404_for_an_unknown_item()
    {
        await using var arrange = fixture.CreateContext();
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.CheckoutAsync(
                new CheckoutRequest { BorrowerId = borrower.Id, ItemId = Guid.NewGuid() }));

        thrown.StatusCode.Should().Be(404);
        thrown.Detail.Should().Contain("item");
    }

    // --- Return ------------------------------------------------------------

    /// <summary>
    /// A NON-TERMINAL status cannot close a loan (BR §6). "Checked Out" is the
    /// canonical non-terminal status, so returning with it must be refused.
    /// </summary>
    [Fact]
    public async Task ReturnAsync_rejects_a_non_terminal_status_with_422_and_leaves_the_loan_open()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.ReturnAsync(loan.Id, new ReturnLoanRequest { LoanStatusId = checkedOut.Id }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("not a terminal status");

        await using var verify = fixture.CreateContext();
        var stored = await verify.Loans.SingleAsync(l => l.Id == loan.Id);
        stored.ReturnedAt.Should().BeNull("a refused return must not have closed the loan");
    }

    [Theory]
    [InlineData("Returned")]
    [InlineData("Lost")]
    [InlineData("Damaged")]
    public async Task ReturnAsync_closes_the_loan_with_any_terminal_status(string statusName)
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var terminal = await fixture.StatusAsync(statusName);

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();
        var closed = await harness.Loans.ReturnAsync(
            loan.Id,
            new ReturnLoanRequest { LoanStatusId = terminal.Id });

        closed.IsOpen.Should().BeFalse();
        closed.ReturnedAt.Should().NotBeNull();
        closed.LoanStatusName.Should().Be(statusName);
        closed.LoanStatusIsTerminal.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnAsync_refuses_to_close_an_already_closed_loan()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var returned = await fixture.StatusAsync("Returned");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);
        await TestData.CloseLoanAsync(arrange, loan.Id, returned.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.ReturnAsync(loan.Id, new ReturnLoanRequest { LoanStatusId = returned.Id }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("already closed");
    }

    [Fact]
    public async Task ReturnAsync_answers_404_for_an_unknown_status()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.ReturnAsync(loan.Id, new ReturnLoanRequest { LoanStatusId = Guid.NewGuid() }));

        thrown.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ReturnAsync_answers_404_for_an_unknown_loan()
    {
        var returned = await fixture.StatusAsync("Returned");

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Loans.ReturnAsync(Guid.NewGuid(), new ReturnLoanRequest { LoanStatusId = returned.Id }));

        thrown.StatusCode.Should().Be(404);
    }

    /// <summary>
    /// The full cycle, in service terms: check out, return, then check the SAME item
    /// out again. This is the behaviour the filtered index exists to permit.
    /// </summary>
    [Fact]
    public async Task An_item_can_be_checked_out_again_after_it_is_returned()
    {
        var category = await fixture.CategoryAsync();
        var returned = await fixture.StatusAsync("Returned");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var first = await TestData.AddBorrowerAsync(arrange);
        var second = await TestData.AddBorrowerAsync(arrange);

        await using var checkoutOne = fixture.CreateHarness();
        var loan = await checkoutOne.Loans.CheckoutAsync(
            new CheckoutRequest { BorrowerId = first.Id, ItemId = item.Id });

        await using var close = fixture.CreateHarness();
        await close.Loans.ReturnAsync(loan.Id, new ReturnLoanRequest { LoanStatusId = returned.Id });

        await using var checkoutTwo = fixture.CreateHarness();
        var second_loan = await checkoutTwo.Loans.CheckoutAsync(
            new CheckoutRequest { BorrowerId = second.Id, ItemId = item.Id });

        second_loan.IsOpen.Should().BeTrue();
        second_loan.BorrowerName.Should().Be(second.Name);
        second_loan.Id.Should().NotBe(loan.Id);
    }
}
