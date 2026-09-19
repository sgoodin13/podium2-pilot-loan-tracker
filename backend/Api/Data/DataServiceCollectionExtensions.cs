using Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// Registers the LoanTracker data layer. This is the only entry point the composition root
/// needs — <c>Program.cs</c> calls <see cref="AddLoanTrackerData"/> and owns nothing else
/// about EF Core wiring.
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> (Npgsql + snake_case naming) and every repository.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">
    /// The Postgres connection string. Comes from configuration — never hard-coded here.
    /// </param>
    public static IServiceCollection AddLoanTrackerData(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            // Audit columns are stamped centrally on SaveChanges rather than in each
            // service, so a service cannot forget and silently insert a blank
            // created_by. Resolved from the provider because the interceptor needs
            // the request's user context.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<Middleware.AuditStampingInterceptor>());

            options
                .UseNpgsql(connectionString)
                // PascalCase entities -> snake_case tables/columns. No column is hand-named.
                .UseSnakeCaseNamingConvention();

            // Deliberately no EnableRetryOnFailure: a retrying execution strategy rejects
            // user-initiated transactions, which the checkout path may need. Local Postgres
            // has no transient-fault surface worth trading that for at pilot scale.
        });

        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
        services.AddScoped<ILoanStatusRepository, LoanStatusRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IBorrowerRepository, BorrowerRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();

        return services;
    }
}
