using Api.Dtos;
using Api.Services;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Borrower rules: the deactivate hard block (mirror of the item retire guard) and
/// name normalisation.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BorrowerServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task DeactivateAsync_is_blocked_while_the_borrower_holds_an_open_loan_and_names_the_items()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Borrowers.DeactivateAsync(borrower.Id));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain(borrower.Name);
        thrown.Detail.Should().Contain(item.Name, "a count alone is not actionable — the items must be named");
        thrown.Detail.Should().Contain(item.AssetTag);

        await using var verify = fixture.CreateContext();
        var stored = await verify.Borrowers.SingleAsync(b => b.Id == borrower.Id);
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_succeeds_once_every_loan_is_returned()
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
        var deactivated = await harness.Borrowers.DeactivateAsync(borrower.Id);

        deactivated.IsActive.Should().BeFalse();

        // Soft delete: the history survives.
        await using var verify = fixture.CreateContext();
        (await verify.Loans.CountAsync(l => l.BorrowerId == borrower.Id)).Should().Be(1);
    }

    /// <summary>The guard's message pluralises and enumerates every held item.</summary>
    [Fact]
    public async Task DeactivateAsync_names_every_held_item_when_more_than_one_loan_is_open()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var first = await TestData.AddItemAsync(arrange, category.Id);
        var second = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        await TestData.AddOpenLoanAsync(arrange, first.Id, borrower.Id, checkedOut.Id);
        await TestData.AddOpenLoanAsync(arrange, second.Id, borrower.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Borrowers.DeactivateAsync(borrower.Id));

        thrown.Detail.Should().Contain("2 open loans");
        thrown.Detail.Should().Contain(first.AssetTag);
        thrown.Detail.Should().Contain(second.AssetTag);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData("\t\t")]
    public async Task CreateAsync_rejects_a_whitespace_only_name(string name)
    {
        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Borrowers.CreateAsync(new CreateBorrowerRequest { Name = name }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("Name cannot be blank");
    }

    [Fact]
    public async Task CreateAsync_normalises_optional_contact_fields_to_null_when_blank()
    {
        await using var harness = fixture.CreateHarness();

        var created = await harness.Borrowers.CreateAsync(new CreateBorrowerRequest
        {
            Name = $"  QA Blank Contact {TestData.Unique()}  ",
            ContactEmail = null,
            ContactPhone = null,
            Department = null,
        });

        created.Name.Should().NotStartWith(" ");
        created.ContactEmail.Should().BeNull();
        created.OpenLoanCount.Should().Be(0);
    }

    /// <summary>
    /// The "Open loans" column on the borrower list is derived per page in one query;
    /// it must report the real open count, not a stored one.
    /// </summary>
    [Fact]
    public async Task GetAsync_reports_the_live_open_loan_count()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var returned = await fixture.StatusAsync("Returned");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using (var before = fixture.CreateHarness())
        {
            (await before.Borrowers.GetAsync(borrower.Id)).OpenLoanCount.Should().Be(0);
        }

        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using (var during = fixture.CreateHarness())
        {
            (await during.Borrowers.GetAsync(borrower.Id)).OpenLoanCount.Should().Be(1);
        }

        await TestData.CloseLoanAsync(arrange, loan.Id, returned.Id);

        await using var after = fixture.CreateHarness();
        (await after.Borrowers.GetAsync(borrower.Id)).OpenLoanCount.Should().Be(0);
    }

    [Fact]
    public async Task ListActiveAsync_excludes_deactivated_borrowers()
    {
        await using var arrange = fixture.CreateContext();
        var active = await TestData.AddBorrowerAsync(arrange);
        var inactive = await TestData.AddBorrowerAsync(arrange, isActive: false);

        await using var harness = fixture.CreateHarness();
        var rows = await harness.Borrowers.ListActiveAsync(null);

        rows.Should().Contain(b => b.Id == active.Id);
        rows.Should().NotContain(b => b.Id == inactive.Id);
    }
}
