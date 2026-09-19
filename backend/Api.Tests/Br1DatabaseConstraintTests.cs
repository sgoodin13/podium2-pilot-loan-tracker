using Api.Data;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Tests;

/// <summary>
/// BR-1 at the database — the single most important test in this suite.
/// </summary>
/// <remarks>
/// "An item already on loan cannot be loaned again" is enforced by a filtered unique
/// index, not by application code. These tests therefore assert the DATABASE's verdict
/// against a real PostgreSQL instance. EF Core InMemory cannot honour a filtered unique
/// index at all, so running this on InMemory would pass on a build with the rule removed.
/// </remarks>
[Collection(PostgresCollection.Name)]
public class Br1DatabaseConstraintTests(PostgresFixture fixture)
{
    /// <summary>
    /// The physical index, not merely the behaviour.
    /// </summary>
    /// <remarks>
    /// The Database Engineer found that two single-argument <c>HasIndex</c> calls on the
    /// same property silently rename the first and ERASE its filter — producing a plain
    /// unique index on item_id that would block a legitimate second loan after return.
    /// Behaviour alone would not catch the opposite regression (filter dropped ⇒ a
    /// re-loan after return starts failing), so the index definition itself is asserted.
    /// </remarks>
    [Fact]
    public async Task Ux_loans_item_open_exists_as_a_unique_index_filtered_on_returned_at_is_null()
    {
        await using var db = fixture.CreateContext();

        var definitions = await db.Database
            .SqlQuery<string>(
                $"""SELECT indexdef AS "Value" FROM pg_indexes WHERE indexname = {DatabaseConstraintNames.OpenLoanPerItem}""")
            .ToListAsync();

        definitions.Should().ContainSingle(
            "the BR-1 index must exist exactly once on the loans table");

        var indexdef = definitions[0];

        indexdef.Should().Contain("CREATE UNIQUE INDEX", "a non-unique index would not block anything");
        indexdef.Should().Contain("item_id");
        indexdef.Should().Contain(
            "WHERE (returned_at IS NULL)",
            "without the filter, an item could never be loaned a second time after being returned");
    }

    /// <summary>
    /// The blocked path, at the engine: a second OPEN loan on the same item is refused.
    /// </summary>
    [Fact]
    public async Task Second_open_loan_for_the_same_item_is_refused_with_23505_on_ux_loans_item_open()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");

        await using var db = fixture.CreateContext();
        var item = await TestData.AddItemAsync(db, category.Id);
        var first = await TestData.AddBorrowerAsync(db);
        var second = await TestData.AddBorrowerAsync(db);

        await TestData.AddOpenLoanAsync(db, item.Id, first.Id, checkedOut.Id);

        // A separate context so the failed insert does not poison the tracker above.
        await using var conflicting = fixture.CreateContext();
        conflicting.Loans.Add(TestData.NewLoan(item.Id, second.Id, checkedOut.Id));

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => conflicting.SaveChangesAsync());

        var postgres = thrown.InnerException as PostgresException;
        postgres.Should().NotBeNull("the failure must be a Postgres constraint violation, not an app-level error");
        postgres!.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.SqlState.Should().Be("23505");
        postgres.ConstraintName.Should().Be(DatabaseConstraintNames.OpenLoanPerItem);

        // Nothing partial was persisted.
        await using var verify = fixture.CreateContext();
        var openCount = await verify.Loans.CountAsync(l => l.ItemId == item.Id && l.ReturnedAt == null);
        openCount.Should().Be(1, "the rejected insert must leave exactly the one original open loan");
    }

    /// <summary>
    /// The filter is what makes a re-loan legal: once returned_at is set, the row leaves
    /// the index's predicate and a fresh open loan on the same item is allowed.
    /// </summary>
    [Fact]
    public async Task Second_open_loan_succeeds_once_the_first_loan_has_been_returned()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var returned = await fixture.StatusAsync("Returned");

        await using var db = fixture.CreateContext();
        var item = await TestData.AddItemAsync(db, category.Id);
        var first = await TestData.AddBorrowerAsync(db);
        var second = await TestData.AddBorrowerAsync(db);

        var loan = await TestData.AddOpenLoanAsync(db, item.Id, first.Id, checkedOut.Id);
        await TestData.CloseLoanAsync(db, loan.Id, returned.Id);

        await using var again = fixture.CreateContext();
        again.Loans.Add(TestData.NewLoan(item.Id, second.Id, checkedOut.Id));

        var act = async () => await again.SaveChangesAsync();
        await act.Should().NotThrowAsync("the filtered index only constrains loans where returned_at IS NULL");

        await using var verify = fixture.CreateContext();
        var loans = await verify.Loans.Where(l => l.ItemId == item.Id).ToListAsync();
        loans.Should().HaveCount(2);
        loans.Count(l => l.ReturnedAt == null).Should().Be(1);
    }

    /// <summary>
    /// Two CLOSED loans on the same item are also legal — the item's whole history
    /// lives in this table, so only the open row is constrained.
    /// </summary>
    [Fact]
    public async Task Many_closed_loans_for_the_same_item_are_permitted()
    {
        var category = await fixture.CategoryAsync();
        var checkedOut = await fixture.StatusAsync("Checked Out");
        var returned = await fixture.StatusAsync("Returned");

        await using var db = fixture.CreateContext();
        var item = await TestData.AddItemAsync(db, category.Id);
        var borrower = await TestData.AddBorrowerAsync(db);

        for (var i = 0; i < 3; i++)
        {
            var loan = await TestData.AddOpenLoanAsync(db, item.Id, borrower.Id, checkedOut.Id);
            await TestData.CloseLoanAsync(db, loan.Id, returned.Id);
        }

        await using var verify = fixture.CreateContext();
        var history = await verify.Loans.CountAsync(l => l.ItemId == item.Id);
        history.Should().Be(3);
    }
}
