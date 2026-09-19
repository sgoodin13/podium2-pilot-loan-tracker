using Api.Data;
using Api.Data.Entities;
using Api.Data.Seed;
using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Api.Tests.Infrastructure;

/// <summary>
/// A real PostgreSQL 17 instance for the whole test run.
/// </summary>
/// <remarks>
/// Deliberately NOT EF Core InMemory. The rule this pilot exists to prove (BR-1) is a
/// FILTERED UNIQUE INDEX — <c>ux_loans_item_open ON loans(item_id) WHERE returned_at IS NULL</c>.
/// InMemory has no such concept, so a broken build would pass silently. Every test that
/// touches BR-1, a guard, or a list query runs against the real engine with the real
/// migrations applied.
/// </remarks>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("loantracker_qa")
        .WithUsername("loantracker_qa")
        .WithPassword("loantracker_qa_test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();

        // The generated migrations are the schema under test — never an
        // EnsureCreated() model snapshot, which would skip the filter clause the
        // migration actually writes.
        await db.Database.MigrateAsync();

        // Reference data (loan statuses, item categories) is required for the
        // product to function. All of it is synthetic.
        await SeedData.SeedAsync(db);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A fresh context with the same wiring the API composes.</summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditStampingInterceptor(new HttpContextAccessor()))
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>A context plus the real repositories and services wired over it.</summary>
    public ServiceHarness CreateHarness() => new(CreateContext());

    /// <summary>A seeded loan status, by name.</summary>
    public async Task<LoanStatus> StatusAsync(string name)
    {
        await using var db = CreateContext();
        return await db.LoanStatuses.AsNoTracking().SingleAsync(s => s.Name == name);
    }

    /// <summary>A seeded item category, by name.</summary>
    public async Task<ItemCategory> CategoryAsync(string name = "Power Tools")
    {
        await using var db = CreateContext();
        return await db.ItemCategories.AsNoTracking().SingleAsync(c => c.Name == name);
    }
}

/// <summary>
/// One collection for every database-touching class, so they run sequentially
/// against the single container rather than racing each other.
/// </summary>
[CollectionDefinition(PostgresCollection.Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
