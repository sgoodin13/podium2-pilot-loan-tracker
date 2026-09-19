using Api.Data;
using Api.Data.Repositories;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// The real repositories and services over one real context — no mocks of the
/// data layer, because the rule under test lives in the database.
/// </summary>
public sealed class ServiceHarness : IAsyncDisposable
{
    public ServiceHarness(AppDbContext db)
    {
        Db = db;

        var items = new ItemRepository(db);
        var borrowers = new BorrowerRepository(db);
        var loans = new LoanRepository(db);
        var categories = new ItemCategoryRepository(db);
        var statuses = new LoanStatusRepository(db);

        Items = new ItemService(items, loans, categories, NullLogger<ItemService>.Instance);
        Borrowers = new BorrowerService(borrowers, loans, NullLogger<BorrowerService>.Instance);
        Loans = new LoanService(loans, items, borrowers, statuses, NullLogger<LoanService>.Instance);
        Categories = new ItemCategoryService(categories, NullLogger<ItemCategoryService>.Instance);
        Statuses = new LoanStatusService(statuses, NullLogger<LoanStatusService>.Instance);
    }

    public AppDbContext Db { get; }

    public IItemService Items { get; }

    public IBorrowerService Borrowers { get; }

    public ILoanService Loans { get; }

    public IItemCategoryService Categories { get; }

    public ILoanStatusService Statuses { get; }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
