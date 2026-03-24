namespace AuthManager.Core.Models;

/// <summary>
/// A paged result set.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int pageSize = 25) => new()
    {
        Items = [],
        TotalCount = 0,
        Page = 1,
        PageSize = pageSize
    };
}
