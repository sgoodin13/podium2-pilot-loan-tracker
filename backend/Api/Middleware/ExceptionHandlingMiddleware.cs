using System.Text.Json;
using Api.Data;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Api.Middleware;

/// <summary>
/// Global exception handler. Every failure leaves the API as an RFC 7807
/// problem-details response (Technical Architecture §6) — never a raw stack trace,
/// never a generic 500 where a specific reason exists.
/// </summary>
/// <remarks>
/// Logging discipline (CLAUDE.md Logging &amp; Observability): structured logging with
/// named parameters, never string concatenation. Exception messages are not echoed
/// to the client for unexpected failures — a database error can carry connection or
/// schema detail, and leaking it is exactly what the Compliance/Security audit looks
/// for. The trace identifier is returned instead so a report can be tied back to the
/// server log.
/// </remarks>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            // An expected business-rule violation. Warning, not Error — it is a
            // handled condition, and the blocked-checkout path is a normal outcome.
            //
            // The Detail is deliberately NOT logged at Warning: service code builds
            // it from entity names for the user's benefit ("Dana Whitfield is
            // deactivated and cannot borrow items"), which would put borrower PII
            // into the log through a second, less obvious path than the direct one
            // (CLAUDE.md §Logging; Compliance finding F7). Title plus the route is
            // enough to diagnose from logs alone; Detail is available at Debug for
            // a developer who has deliberately turned it on.
            logger.LogWarning(
                "Business rule rejected {Method} {Path}: {Title} ({StatusCode})",
                context.Request.Method,
                context.Request.Path,
                ex.Title,
                ex.StatusCode);

            logger.LogDebug(
                "Rejection detail for {Method} {Path}: {Detail}",
                context.Request.Method,
                context.Request.Path,
                ex.Detail);

            await WriteProblemAsync(context, ex.StatusCode, ex.Title, ex.Detail);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // A uniqueness conflict that reached here rather than being translated
            // in a service. BR-1 is handled in LoanService, so this is the safety
            // net for the remaining unique indexes.
            var (title, detail) = TranslateUniqueViolation(ex.ConstraintName);

            logger.LogWarning(
                "Unique constraint {ConstraintName} rejected {Method} {Path}",
                ex.ConstraintName,
                context.Request.Method,
                context.Request.Path);

            await WriteProblemAsync(context, StatusCodes.Status409Conflict, title, detail);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception on {Method} {Path} (trace {TraceId})",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "Something went wrong handling this request. The trace id identifies it in the server log.");
        }
    }

    private static (string Title, string Detail) TranslateUniqueViolation(string? constraintName) =>
        constraintName switch
        {
            DatabaseConstraintNames.OpenLoanPerItem =>
                ("Item already on loan",
                 "This item already has an open loan and cannot be checked out again."),
            DatabaseConstraintNames.ItemAssetTag =>
                ("Duplicate asset tag",
                 "An item with that asset tag already exists. Asset tags must be unique."),
            DatabaseConstraintNames.ItemCategoryName =>
                ("Duplicate category name", "A category with that name already exists."),
            DatabaseConstraintNames.LoanStatusName =>
                ("Duplicate status name", "A loan status with that name already exists."),
            _ => ("Conflict", "That change conflicts with an existing record."),
        };

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        // ASP.NET Core's built-in trace identifier is the correlation id for this
        // product — CLAUDE.md states it is sufficient at this scale, so no custom
        // correlation middleware is built.
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}

/// <summary>Registration helper for <see cref="ExceptionHandlingMiddleware"/>.</summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseLoanTrackerExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
