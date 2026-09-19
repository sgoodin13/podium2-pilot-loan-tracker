using Api.Data;
using Api.Data.Seed;
using Api.Middleware;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// LoanTracker API — composition root.
//
// SHARED CORE FILE (LoanTracker_Stack_Rules.md [SHARED_CORE_FILES]).
// Never modify without Orchestrator approval.

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---------------------------------------------------------
// The local database name is run-scoped (loantracker_dev_<branch>) rather than a
// fixed shared name — CLAUDE.md Incremental Session Patterns. The connection
// string comes from configuration or user-secrets; a real one is never committed
// (LoanTracker_Stack_Rules.md [STACK_RULES]).
var connectionString =
    builder.Configuration.GetConnectionString("LoanTracker")
    ?? throw new InvalidOperationException(
        "Connection string 'LoanTracker' is not configured. "
        + "Set it in appsettings.Development.json or via `dotnet user-secrets`.");

// --- Services --------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddLoanTrackerData(connectionString);
builder.Services.AddSingleton<AuditStampingInterceptor>();
builder.Services.AddLoanTrackerServices();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Model-validation failures leave as RFC 7807 too, so the frontend has a
        // single error shape to render inline (Technical Architecture §6).
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Instance = context.HttpContext.Request.Path,
            };
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            // Written explicitly rather than through content negotiation, so this
            // path and the exception middleware emit the same media type
            // (QA defect D3 — see ProblemJsonResult for why ContentTypes alone
            // was not enough).
            return new ProblemJsonResult(problem);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
// Exception handling wraps everything below it, so any failure downstream still
// leaves as problem details rather than a raw stack trace.
app.UseLoanTrackerExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Stub auth: every request becomes an authenticated Staff user. See the
// middleware's own remarks for why this is safe only on Local.
app.UseStubAuthentication();

app.MapControllers();

// --- Local bootstrap -------------------------------------------------------
// Development only, and hard-guarded: applies the generated migrations and seeds
// the run-scoped local database. CLAUDE.md asks for exactly this — "prefer an
// idempotent, migration-applying bootstrap over create-if-absent" — while its
// "never apply migrations directly" rule governs shared environments, where
// promotion happens by PR and migrations are generated, never auto-applied.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Bootstrap");

    logger.LogInformation("Applying migrations to the local development database");
    await db.Database.MigrateAsync();

    // One seed per environment (Standards Guide #2) — idempotent, so a restart
    // does not re-seed. All seeded data is synthetic.
    await SeedData.SeedAsync(db, logger);
}

await app.RunAsync();

/// <summary>Exposed so the integration tests can drive the real pipeline.</summary>
public partial class Program;
