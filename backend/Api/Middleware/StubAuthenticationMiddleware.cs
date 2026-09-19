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
    public static IApplicationBuilder UseStubAuthentication(this IApplicationBuilder app)
        => app.UseMiddleware<StubAuthenticationMiddleware>();
}
