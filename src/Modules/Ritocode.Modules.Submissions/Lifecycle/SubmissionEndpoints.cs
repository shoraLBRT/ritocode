using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Paging;
using Ritocode.Shared.Validation;

namespace Ritocode.Modules.Submissions.Lifecycle;

/// <summary>
/// <c>POST /api/v1/submissions</c>, <c>GET /api/v1/submissions</c> and <c>GET /api/v1/submissions/{id}</c>.
/// </summary>
/// <remarks>
/// None says anything about authorisation, as the workspace endpoints say nothing: the host's fallback
/// policy is what closes them to an anonymous caller.
/// </remarks>
internal static class SubmissionEndpoints
{
    private const string GetSubmissionRouteName = "GetSubmission";

    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/", SubmitAsync)
            .WithName("Submit")
            .WithTags("Submissions")
            .WithValidation<SubmitRequest>();

        endpoints.MapGet("/", ListAsync)
            .WithName("ListSubmissions")
            .WithTags("Submissions");

        endpoints.MapGet("/{id}", GetAsync)
            .WithName(GetSubmissionRouteName)
            .WithTags("Submissions");

        return endpoints;
    }

    /// <summary>201 with a <c>Location</c>: every submit is a new attempt, so there is no 200 case.</summary>
    private static async Task<IResult> SubmitAsync(
        SubmitRequest request,
        ISubmissionLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // The validation filter has already refused an absent or empty id; an empty one finds nothing.
        var result = await lifecycle.SubmitAsync(
            currentUser.RequireId(),
            request.WorkspaceId.GetValueOrDefault(),
            cancellationToken);

        return result.Match(
            submission => Results.CreatedAtRoute(GetSubmissionRouteName, new { id = submission.Id }, submission),
            error => ApiProblem.ToResult(error, context));
    }

    private static async Task<IResult> ListAsync(
        ISubmissionLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        string? workspaceId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // Query parameters are not a body, so PageRequest reports its own range rules, as the catalog does.
        var request = PageRequest.Create(page, pageSize);

        if (!request.IsSuccess)
        {
            return ApiProblem.ToResult(request.Error, context);
        }

        Guid? workspace = null;

        if (workspaceId is not null)
        {
            // An id is an opaque string to a client (ADR 0003). One that is not even well formed names
            // no workspace of the caller's, so it filters to nothing — as an id of someone else's does —
            // rather than being a 400 that would tell the two apart.
            if (!Guid.TryParse(workspaceId, out var parsed))
            {
                return Results.Ok(Page<SubmissionDetail>.Empty(request.Value));
            }

            workspace = parsed;
        }

        return Results.Ok(await lifecycle.ListAsync(userId, workspace, request.Value, cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        string id,
        ISubmissionLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // As for a workspace: a malformed id is an attempt this caller does not have, in the error body.
        if (!Guid.TryParse(id, out var submissionId))
        {
            return ApiProblem.ToResult(SubmissionLifecycle.SubmissionNotFound(), context);
        }

        var result = await lifecycle.GetAsync(userId, submissionId, cancellationToken);

        return result.Match(
            submission => Results.Ok(submission),
            error => ApiProblem.ToResult(error, context));
    }
}
