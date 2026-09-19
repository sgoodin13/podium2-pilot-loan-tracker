using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Infrastructure;

/// <summary>
/// The real API pipeline — real middleware, real controllers, real EF Core —
/// pointed at the Testcontainers database instead of the developer's local one.
/// </summary>
/// <remarks>
/// <c>Program</c> is public-partial precisely so these tests drive the composition
/// root rather than a reassembled imitation of it. The Development environment is
/// selected deliberately: that is the branch where migrations are applied and the
/// reference data is seeded, and the contract under test includes that reference data.
/// </remarks>
public class LoanTrackerApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting lands above appsettings.Development.json in the configuration
        // stack, so the committed local connection string is never touched.
        builder.UseSetting("ConnectionStrings:LoanTracker", connectionString);
    }
}
