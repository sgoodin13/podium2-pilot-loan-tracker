using System.Security.Claims;

namespace Api.Middleware;

/// <summary>
/// Stub authentication. Every request is treated as an authenticated Staff user.
/// </summary>
/// <remarks>
/// This is a deliberate, approved scope decision — trigger spec §4
/// <c>[RULING: auth]</c> and BR §7: "Authentication/authorization beyond a stub"
/// is an explicit deferral, not an oversight. There is no login flow, no identity
/// provider, no JWT or session validation, and exactly one role.
///
/// The principal it establishes is what populates the <c>created_by</c> /
/// <c>modified_by</c> audit columns, which are plain text precisely because there
/// is no identity store to foreign-key against.
///
/// SECURITY NOTE: this grants every caller full access. It is safe only because
/// this pilot is scoped to the Local environment (CLAUDE.md Environments —
/// Shared Validation / UAT / Prod are out of scope). Promoting this build beyond
/// Local without replacing this middleware would be a genuine vulnerability.
/// </remarks>
public class StubAuthenticationMiddleware(RequestDelegate next)
{
    /// <summary>The single role this pilot recognises.</summary>
    public const string StaffRole = "Staff";

    /// <summary>The label written to the audit columns.</summary>
    public const string StaffUserName = "staff";

    public async Task InvokeAsync(HttpContext context)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, StaffUserName),
                new Claim(ClaimTypes.Role, StaffRole),
            ],
            authenticationType: "StubAuth");

        context.User = new ClaimsPrincipal(identity);

        await next(context);
    }
}

/// <summary>Registration helper for <see cref="StubAuthenticationMiddleware"/>.</summary>
public static class StubAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Registers the stub, and refuses to start outside Development.
    /// </summary>
    /// <remarks>
    /// The middleware's remarks say promoting this build beyond Local would be a
    /// genuine vulnerability. This is the code that makes that true rather than
    /// merely stated: outside Development the application fails to boot instead of
    /// serving every anonymous caller as Staff (Compliance finding F1).
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the host environment is anything other than Development.
    /// </exception>
    public static IApplicationBuilder UseStubAuthentication(this IApplicationBuilder app)
    {
        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Stub authentication is Development-only: it treats every request as an "
                + $"authenticated {StubAuthenticationMiddleware.StaffRole} user with full access. "
                + $"The host environment is '{environment.EnvironmentName}'. Replace this middleware "
                + "with real authentication before running anywhere but Local "
                + "(trigger spec §4 [RULING: auth]).");
        }

        // Loud on every Development boot, so the deferral is visible at runtime and
        // not only in a code comment.
        app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<StubAuthenticationMiddleware>()
            .LogWarning(
                "Stub authentication is ENABLED — every request is treated as an authenticated "
                + "{Role} user with no credential check. Development only; never promote this build "
                + "beyond Local without replacing it.",
                StubAuthenticationMiddleware.StaffRole);

        return app.UseMiddleware<StubAuthenticationMiddleware>();
    }
}
