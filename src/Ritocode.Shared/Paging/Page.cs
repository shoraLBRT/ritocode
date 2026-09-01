namespace Ritocode.Shared.Paging;

/// <summary>
/// The platform-wide pagination envelope. Collection endpoints return this rather than a bare
/// array, so clients always have the totals needed to render a pager.
/// </summary>
public sealed record Page<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalItems)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)((TotalItems + PageSize - 1) / PageSize);

    public bool HasNextPage => PageNumber < TotalPages;

    public bool HasPreviousPage => PageNumber > PageRequest.FirstPage;

    public static Page<T> Empty(PageRequest request) =>
        new([], request.Page, request.PageSize, 0);

    public static Page<T> From(IReadOnlyList<T> items, PageRequest request, long totalItems) =>
        new(items, request.Page, request.PageSize, totalItems);
}
