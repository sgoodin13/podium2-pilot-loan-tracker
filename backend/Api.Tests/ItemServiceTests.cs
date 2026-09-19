using Api.Data.Repositories;
using Api.Dtos;
using Api.Services;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// Item rules: asset-tag uniqueness, whitespace rejection, and the retire hard block.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ItemServiceTests(PostgresFixture fixture)
{
    // --- Retire hard block (BR §8, SME-resolved) ----------------------------

    /// <summary>
    /// A HARD BLOCK, not a warning: while an open loan exists the item cannot be
    /// retired, and the reason names the item, its asset tag and the holder.
    /// </summary>
    [Fact]
    public async Task RetireAsync_is_blocked_while_the_item_has_an_open_loan()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() => harness.Items.RetireAsync(item.Id));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain(item.Name);
        thrown.Detail.Should().Contain(item.AssetTag);
        thrown.Detail.Should().Contain(borrower.Name, "the reason must say who is holding it");
        thrown.Detail.Should().Contain("Return it first");

        await using var verify = fixture.CreateContext();
        var stored = await verify.Items.SingleAsync(i => i.Id == item.Id);
        stored.IsActive.Should().BeTrue("a blocked retire must not have soft-deleted the row");
    }

    /// <summary>The door out of the block: close the loan, then retire succeeds.</summary>
    [Fact]
    public async Task RetireAsync_succeeds_once_the_open_loan_is_returned()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var lost = await fixture.StatusAsync("Lost");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);
        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);
        await TestData.CloseLoanAsync(arrange, loan.Id, lost.Id);

        await using var harness = fixture.CreateHarness();
        var retired = await harness.Items.RetireAsync(item.Id);

        retired.IsActive.Should().BeFalse();

        // Soft delete only: the row and its loan history survive.
        await using var verify = fixture.CreateContext();
        (await verify.Items.AnyAsync(i => i.Id == item.Id)).Should().BeTrue();
        (await verify.Loans.CountAsync(l => l.ItemId == item.Id)).Should().Be(1);
    }

    // --- Asset-tag uniqueness (BR §5.1) ------------------------------------

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_asset_tag_with_409()
    {
        var category = await fixture.CategoryAsync();
        var tag = $"QA-DUP-{TestData.Unique()}";

        await using var first = fixture.CreateHarness();
        await first.Items.CreateAsync(new CreateItemRequest
        {
            Name = "QA Original",
            AssetTag = tag,
            ItemCategoryId = category.Id,
        });

        await using var second = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            second.Items.CreateAsync(new CreateItemRequest
            {
                Name = "QA Duplicate",
                AssetTag = tag,
                ItemCategoryId = category.Id,
            }));

        thrown.StatusCode.Should().Be(409);
        thrown.Detail.Should().Contain(tag);
        thrown.Detail.Should().Contain("unique");

        await using var verify = fixture.CreateContext();
        (await verify.Items.CountAsync(i => i.AssetTag == tag)).Should().Be(1);
    }

    /// <summary>Uniqueness is case-insensitive — "qa-1" and "QA-1" are the same tag.</summary>
    [Fact]
    public async Task CreateAsync_treats_asset_tags_as_case_insensitive()
    {
        var category = await fixture.CategoryAsync();
        var tag = $"qa-case-{TestData.Unique()}";

        await using var first = fixture.CreateHarness();
        await first.Items.CreateAsync(new CreateItemRequest
        {
            Name = "QA Case Original",
            AssetTag = tag,
            ItemCategoryId = category.Id,
        });

        await using var second = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            second.Items.CreateAsync(new CreateItemRequest
            {
                Name = "QA Case Duplicate",
                AssetTag = tag.ToUpperInvariant(),
                ItemCategoryId = category.Id,
            }));

        thrown.StatusCode.Should().Be(409);
    }

    // --- Whitespace / boundary ---------------------------------------------

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("")]
    public async Task CreateAsync_rejects_a_whitespace_only_name(string name)
    {
        var category = await fixture.CategoryAsync();

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Items.CreateAsync(new CreateItemRequest
            {
                Name = name,
                AssetTag = $"QA-WS-{TestData.Unique()}",
                ItemCategoryId = category.Id,
            }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("Name cannot be blank");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task CreateAsync_rejects_a_whitespace_only_asset_tag(string assetTag)
    {
        var category = await fixture.CategoryAsync();

        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Items.CreateAsync(new CreateItemRequest
            {
                Name = "QA Whitespace Tag",
                AssetTag = assetTag,
                ItemCategoryId = category.Id,
            }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("Asset tag cannot be blank");
    }

    [Fact]
    public async Task CreateAsync_trims_surrounding_whitespace_rather_than_storing_it()
    {
        var category = await fixture.CategoryAsync();
        var tag = $"QA-TRIM-{TestData.Unique()}";

        await using var harness = fixture.CreateHarness();
        var created = await harness.Items.CreateAsync(new CreateItemRequest
        {
            Name = "  QA Padded Name  ",
            AssetTag = $"  {tag}  ",
            ItemCategoryId = category.Id,
        });

        created.Name.Should().Be("QA Padded Name");
        created.AssetTag.Should().Be(tag);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_category()
    {
        await using var harness = fixture.CreateHarness();

        var thrown = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Items.CreateAsync(new CreateItemRequest
            {
                Name = "QA Orphan",
                AssetTag = $"QA-ORPH-{TestData.Unique()}",
                ItemCategoryId = Guid.NewGuid(),
            }));

        thrown.StatusCode.Should().Be(422);
        thrown.Detail.Should().Contain("category");
    }

    // --- Derived availability ----------------------------------------------

    /// <summary>
    /// Availability is DERIVED from open-loan state, never stored. The list projection
    /// and the single read must agree.
    /// </summary>
    [Fact]
    public async Task Availability_is_derived_from_open_loan_state_on_both_read_paths()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var returned = await fixture.StatusAsync("Returned");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using (var before = fixture.CreateHarness())
        {
            (await before.Items.GetAsync(item.Id)).IsOnLoan.Should().BeFalse();
        }

        var loan = await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using (var during = fixture.CreateHarness())
        {
            var single = await during.Items.GetAsync(item.Id);
            single.IsOnLoan.Should().BeTrue();
            single.CurrentBorrowerName.Should().Be(borrower.Name);

            var page = await during.Items.ListAsync(
                new PageRequest(1, 200),
                search: item.AssetTag,
                categoryId: null,
                availability: "onloan",
                ct: default);

            page.Items.Should().ContainSingle(i => i.Id == item.Id && i.IsOnLoan);
        }

        await TestData.CloseLoanAsync(arrange, loan.Id, returned.Id);

        await using var after = fixture.CreateHarness();
        var reread = await after.Items.GetAsync(item.Id);
        reread.IsOnLoan.Should().BeFalse();
        reread.CurrentBorrowerName.Should().BeNull();
    }

    /// <summary>The checkout wizard's step-2 source must exclude items already out.</summary>
    [Fact]
    public async Task ListAvailableAsync_excludes_items_with_an_open_loan()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var arrange = fixture.CreateContext();
        var item = await TestData.AddItemAsync(arrange, category.Id);
        var borrower = await TestData.AddBorrowerAsync(arrange);

        await using (var before = fixture.CreateHarness())
        {
            var available = await before.Items.ListAvailableAsync(item.AssetTag);
            available.Should().ContainSingle(i => i.Id == item.Id);
        }

        await TestData.AddOpenLoanAsync(arrange, item.Id, borrower.Id, checkedOut.Id);

        await using var after = fixture.CreateHarness();
        var stillAvailable = await after.Items.ListAvailableAsync(item.AssetTag);
        stillAvailable.Should().NotContain(i => i.Id == item.Id);
    }
}
