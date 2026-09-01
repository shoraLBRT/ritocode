using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Paging;

/// <summary>
/// The platform-wide pagination input: 1-based page numbers with a bounded page size.
/// Every collection endpoint accepts <c>?page=</c> and <c>?pageSize=</c> and nothing else,
/// so clients can page any list the same way.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int FirstPage = 1;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Rows to skip for this page; safe to widen to a SQL OFFSET.</summary>
    public long Offset => (long)(Page - FirstPage) * PageSize;

    /// <summary>
    /// Validates raw query values. Absent values fall back to defaults; out-of-range values are
    /// rejected rather than clamped, so a client asking for 1000 rows learns it cannot have them.
    /// </summary>
    public static Result<PageRequest> Create(int? page, int? pageSize)
    {
        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var resolvedPage = page ?? FirstPage;
        if (resolvedPage < FirstPage)
        {
            fields["page"] = [$"Must be {FirstPage} or greater."];
        }

        var resolvedPageSize = pageSize ?? DefaultPageSize;
        if (resolvedPageSize is < 1 or > MaxPageSize)
        {
            fields["pageSize"] = [$"Must be between 1 and {MaxPageSize}."];
        }

        return fields.Count > 0
            ? Result<PageRequest>.Failure(AppError.Validation("Invalid pagination parameters.", fields))
            : Result<PageRequest>.Success(new PageRequest(resolvedPage, resolvedPageSize));
    }
}
