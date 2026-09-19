using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add ...</c> build an <see cref="AppDbContext"/> without booting
/// the web host or reaching a live database.
/// </summary>
/// <remarks>
/// The connection string below is a design-time placeholder only — migrations are GENERATED,
/// never applied, and the scaffolding process never opens this connection. The runtime
/// connection string comes from configuration via
/// <see cref="DataServiceCollectionExtensions.AddLoanTrackerData"/>.
/// <para>
/// Set <c>LOANTRACKER_DESIGNTIME_CONNECTION</c> to override it locally.
/// </para>
/// <para>No secret here: these are the local Docker Compose development credentials.</para>
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string PlaceholderConnectionString =
        "Host=localhost;Port=5433;Database=loantracker_design;Username=loantracker;Password=loantracker_dev";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("LOANTRACKER_DESIGNTIME_CONNECTION")
            ?? PlaceholderConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
