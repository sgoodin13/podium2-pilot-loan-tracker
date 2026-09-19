namespace Api.Data.Repositories;

/// <summary>
/// Sort arguments for a list read.
/// </summary>
/// <param name="SortBy">
/// The column key requested by the caller. Each repository maps this against its
/// own allow-list — an unrecognised key falls back to that list's default order
/// rather than being interpolated into a query.
/// </param>
/// <param name="Descending">Sort direction.</param>
public readonly record struct SortRequest(string? SortBy, bool Descending)
{
    public static SortRequest Default => new(null, false);

    /// <summary>Builds a sort request from raw query-string values.</summary>
    public static SortRequest From(string? sortBy, string? sortDir) =>
        new(sortBy, string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase));

    /// <summary>Normalised key for matching, lower-cased and trimmed.</summary>
    public string Key => SortBy?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
/// Ordering helpers shared by the repositories.
/// </summary>
/// <remarks>
/// Sort keys are matched against a hard-coded allow-list in each repository and
/// resolved to a typed expression. No caller-supplied string ever reaches SQL —
/// this is the injection guard for the sort surface.
/// </remarks>
public static class SortExtensions
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, TKey>> keySelector,
        bool descending)
        => descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, TKey>> keySelector,
        bool descending)
        => descending ? query.ThenByDescending(keySelector) : query.ThenBy(keySelector);
}
