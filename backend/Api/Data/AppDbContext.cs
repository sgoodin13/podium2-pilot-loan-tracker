using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// The single EF Core context for LoanTracker.
/// </summary>
/// <remarks>
/// SHARED CORE FILE — do not modify without Orchestrator approval.
/// <para>
/// Gate 1 rulings this context embodies:
/// <list type="bullet">
///   <item>uuid primary key on every table, Postgres default <c>gen_random_uuid()</c>.</item>
///   <item>Single-tenant: one schema (<c>public</c>), no tenant column, no global query filter.</item>
///   <item>Soft delete only: <c>is_active</c> on every table; nothing is ever hard-deleted.</item>
///   <item>Audit columns on every table.</item>
///   <item>snake_case physical names derived by <c>UseSnakeCaseNamingConvention()</c> —
///         no column is hand-named.</item>
/// </list>
/// </para>
/// <para>
/// No global soft-delete query filter is configured on purpose: <c>is_active</c> filtering is
/// applied explicitly in the repositories, so an administrative "include inactive" read stays
/// possible without <c>IgnoreQueryFilters()</c> leaking through the whole stack.
/// </para>
/// <para>
/// Lazy loading is not enabled (no proxies package is referenced). Reads eager-load with
/// <c>Include()</c> in the repositories to avoid N+1.
/// </para>
/// <para>
/// Audit-column stamping (<c>CreatedBy</c> / <c>ModifiedBy</c> / <c>ModifiedAt</c>) is the
/// service layer's responsibility — it needs the request's user context, which the data layer
/// does not have.
/// </para>
/// </remarks>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary><c>item_categories</c></summary>
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    /// <summary><c>loan_statuses</c></summary>
    public DbSet<LoanStatus> LoanStatuses => Set<LoanStatus>();

    /// <summary><c>items</c></summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary><c>borrowers</c></summary>
    public DbSet<Borrower> Borrowers => Set<Borrower>();

    /// <summary><c>loans</c></summary>
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Single-tenant: one schema, no discriminator.
        modelBuilder.HasDefaultSchema("public");

        // Every IEntityTypeConfiguration<T> in Api.Data.Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
