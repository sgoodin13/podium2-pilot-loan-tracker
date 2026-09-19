namespace Api.Data.Repositories;

/// <summary>
/// Paging arguments for every list read. Every list endpoint paginates
/// (Podium2 Standards Guide #3) — there is no unbounded list method on any repository.
/// </summary>
/// <param name="Page">1-based page number. Values below 1 are clamped to 1.</param>
/// <param name="PageSize">Rows per page. Clamped to [1, <see cref="MaxPageSize"/>].</param>
public readonly record struct PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public static PageRequest Default => new(1, DefaultPageSize);

    /// <summary>Page number after clamping.</summary>
    public int SafePage => Page < 1 ? 1 : Page;

    /// <summary>Page size after clamping.</summary>
    public int SafePageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };

    /// <summary>Rows to skip for this page.</summary>
    public int Skip => (SafePage - 1) * SafePageSize;
}
