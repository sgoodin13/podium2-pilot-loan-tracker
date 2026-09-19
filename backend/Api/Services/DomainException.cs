namespace Api.Services;

/// <summary>
/// A business-rule violation that the caller can act on — mapped by the global
/// exception handler to an RFC 7807 problem-details response with a specific,
/// user-facing reason, never a generic 500.
/// </summary>
/// <param name="statusCode">The HTTP status this violation maps to.</param>
/// <param name="title">Short, stable summary.</param>
/// <param name="detail">The user-facing reason. Safe to render in the UI.</param>
public class DomainException(int statusCode, string title, string detail)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string Detail { get; } = detail;

    /// <summary>404 — the record does not exist (or is soft-deleted and not requested).</summary>
    public static DomainException NotFound(string what) =>
        new(StatusCodes.Status404NotFound, "Not found", $"{what} was not found.");

    /// <summary>422 — the request was well-formed but violates a business rule.</summary>
    public static DomainException Unprocessable(string detail) =>
        new(StatusCodes.Status422UnprocessableEntity, "Rule violation", detail);

    /// <summary>409 — the request conflicts with current state (uniqueness, BR-1).</summary>
    public static DomainException Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, "Conflict", detail);
}
