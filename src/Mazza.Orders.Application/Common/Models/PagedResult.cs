namespace Mazza.Orders.Application.Common.Models;

/// <summary>
/// Pagination envelope returned by list queries. Carries enough metadata for a
/// client to render a pager without issuing a second "how many are there" request.
/// </summary>
/// <param name="Items">The rows on this page.</param>
/// <param name="Page">1-based page number that was requested.</param>
/// <param name="PageSize">Maximum rows per page.</param>
/// <param name="TotalCount">Total rows available across all pages.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
