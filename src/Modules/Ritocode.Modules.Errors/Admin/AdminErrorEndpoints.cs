using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Modules.Errors.Persistence;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Errors.Admin;

/// <summary>Which groups the list shows.</summary>
public enum ErrorGroupStatus
{
    Open,
    Resolved,
}

/// <summary>A group as the admin list shows it, with the request ids of its latest occurrences, newest first.</summary>
public sealed record AdminErrorGroupView(
    Guid Id,
    ErrorSource Source,
    string Type,
    string Message,
    string Location,
    long Count,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<string> LatestRequestIds);

/// <summary>One kept occurrence: its request id finds its lines in the API's logs.</summary>
public sealed record AdminErrorOccurrenceView(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? RequestId,
    string Message,
    string Location,
    string? Stack,
    string? Commit);

/// <summary>A group and every occurrence still kept of it (at most 50, the latest first).</summary>
public sealed record AdminErrorGroupDetail(AdminErrorGroupView Group, IReadOnlyList<AdminErrorOccurrenceView> Occurrences);

/// <summary>
/// <c>/api/v1/admin/errors</c> (ADR 0020, #294), for admins only: a signed-in non-admin gets the 404 of an
/// unknown address (<see cref="AdminPolicy"/>).
/// </summary>
internal static class AdminErrorEndpoints
{
    public const string NotFoundCode = "error_group_not_found";

    /// <summary>How many request ids a row of the list carries.</summary>
    public const int LatestRequestIds = 5;

    public static IEndpointRouteBuilder MapAdminErrorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/errors")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        admin.MapGet("", ListAsync).WithName("AdminListErrors");
        admin.MapGet("/{id}", GetAsync).WithName("AdminGetError");
        admin.MapPost("/{id}/resolve", ResolveAsync).WithName("AdminResolveError");

        return endpoints;
    }

    /// <summary><c>?status=open</c> (the default) or <c>resolved</c>; the most recently seen first.</summary>
    private static async Task<IResult> ListAsync(
        ErrorsDbContext context,
        HttpContext http,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Create(page, pageSize);
        var fields = request.IsSuccess || request.Error?.Fields is null
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(request.Error.Fields, StringComparer.Ordinal);

        var filter = ErrorGroupStatus.Open;
        if (status is not null)
        {
            var match = Enum.GetNames<ErrorGroupStatus>().FirstOrDefault(name => string.Equals(name, status, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                fields["status"] = ["Must be 'open' or 'resolved'."];
            }
            else
            {
                filter = Enum.Parse<ErrorGroupStatus>(match);
            }
        }

        if (fields.Count > 0)
        {
            return ApiProblem.ToResult(AppError.Validation("Invalid query parameters.", fields), http);
        }

        var paging = request.Value;
        var query = filter == ErrorGroupStatus.Open
            ? context.Groups.Where(group => group.ResolvedAt == null)
            : context.Groups.Where(group => group.ResolvedAt != null);

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var groups = await query
            .OrderByDescending(group => group.LastSeenAt)
            .ThenByDescending(group => group.Id)
            .Skip((int)paging.Offset)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = groups.Select(group => group.Id).ToList();
        var requestIds = (await context.Occurrences
                .Where(occurrence => ids.Contains(occurrence.GroupId) && occurrence.RequestId != null)
                .Select(occurrence => new { occurrence.GroupId, occurrence.OccurredAt, occurrence.Id, occurrence.RequestId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(occurrence => occurrence.GroupId)
            .ToDictionary(
                byGroup => byGroup.Key,
                byGroup => (IReadOnlyList<string>)[.. byGroup
                    .OrderByDescending(occurrence => occurrence.OccurredAt)
                    .ThenByDescending(occurrence => occurrence.Id)
                    .Take(LatestRequestIds)
                    .Select(occurrence => occurrence.RequestId!)]);

        var views = groups.Select(group => View(group, requestIds.GetValueOrDefault(group.Id, []))).ToList();
        return Results.Ok(Page<AdminErrorGroupView>.From(views, paging, total));
    }

    private static async Task<IResult> GetAsync(string id, ErrorsDbContext context, HttpContext http, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var groupId)
            || await context.Groups.AsNoTracking().FirstOrDefaultAsync(group => group.Id == groupId, cancellationToken).ConfigureAwait(false) is not { } group)
        {
            return ApiProblem.ToResult(NotFound(), http);
        }

        var occurrences = await context.Occurrences
            .Where(occurrence => occurrence.GroupId == groupId)
            .OrderByDescending(occurrence => occurrence.OccurredAt)
            .ThenByDescending(occurrence => occurrence.Id)
            .Select(occurrence => new AdminErrorOccurrenceView(
                occurrence.Id, occurrence.OccurredAt, occurrence.RequestId, occurrence.Message, occurrence.Location, occurrence.Stack, occurrence.Commit))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latest = occurrences.Where(occurrence => occurrence.RequestId is not null).Take(LatestRequestIds).Select(occurrence => occurrence.RequestId!).ToList();
        return Results.Ok(new AdminErrorGroupDetail(View(group, latest), occurrences));
    }

    /// <summary>200 with the group, resolved. Resolving one already resolved changes nothing; its next occurrence reopens it.</summary>
    private static async Task<IResult> ResolveAsync(
        string id,
        ErrorsDbContext context,
        TimeProvider clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var groupId)
            || await context.Groups.FirstOrDefaultAsync(group => group.Id == groupId, cancellationToken).ConfigureAwait(false) is not { } group)
        {
            return ApiProblem.ToResult(NotFound(), http);
        }

        var now = clock.GetUtcNow();
        group.Resolve(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var latest = await context.Occurrences
            .Where(occurrence => occurrence.GroupId == groupId && occurrence.RequestId != null)
            .OrderByDescending(occurrence => occurrence.OccurredAt)
            .ThenByDescending(occurrence => occurrence.Id)
            .Take(LatestRequestIds)
            .Select(occurrence => occurrence.RequestId!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(View(group, latest));
    }

    private static AdminErrorGroupView View(ErrorGroup group, IReadOnlyList<string> latestRequestIds) => new(
        group.Id,
        group.Source,
        group.Type,
        group.Message,
        group.Location,
        group.Count,
        group.FirstSeenAt,
        group.LastSeenAt,
        group.ResolvedAt,
        latestRequestIds);

    private static AppError NotFound() => AppError.NotFound(NotFoundCode, "No error group exists with this id.");
}
