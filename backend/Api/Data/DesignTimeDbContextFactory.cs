using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add ...</c> build an <see cref="AppDbContext"/> without booting
/// the web host or reaching a live database.
/// </summary>
/// <remarks>
/// <para>
/// The connection is never opened — migrations are GENERATED, never applied, and scaffolding
/// only needs the provider to shape the SQL. The runtime connection string comes from
/// configuration via <see cref="DataServiceCollectionExtensions.AddLoanTrackerData"/>.
/// </para>
/// <para>
/// Set <c>LOANTRACKER_DESIGNTIME_CONNECTION</c> before running <c>dotnet ef</c>. There is
/// deliberately no hardcoded fallback: the previous one embedded working local credentials in
/// C# source for no benefit, since this override already existed (Compliance finding F3).
/// </para>
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConnectionVariable = "LOANTRACKER_DESIGNTIME_CONNECTION";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException(
                $"{ConnectionVariable} is not set. `dotnet ef` needs a connection string to "
                + "shape the migration, though it never opens the connection. Set it for the "
                + "current shell, for example:\n\n"
                + $"  $env:{ConnectionVariable} = "
                + "\"Host=localhost;Port=5433;Database=loantracker_design;Username=loantracker;Password=<from .env>\"");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
