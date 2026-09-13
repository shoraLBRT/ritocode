using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Validation;

namespace Ritocode.Modules.Workspaces.Lifecycle;

/// <summary>
/// <c>POST /api/v1/workspaces</c> and <c>GET /api/v1/workspaces/{id}</c>.
/// </summary>
/// <remarks>
/// Neither says <c>AllowAnonymous</c>, and neither says anything else about authorisation either.
/// That is deliberate: these are the first product endpoints the host's fallback policy protects, so
/// they are closed to an anonymous caller because the host is, not because someone remembered.
/// </remarks>
internal static class WorkspaceEndpoints
{
    private const string GetWorkspaceRouteName = "GetWorkspace";

    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/", OpenAsync)
            .WithName("OpenWorkspace")
            .WithTags("Workspaces")
            .WithValidation<OpenWorkspaceRequest>();

        endpoints.MapGet("/{id}", GetAsync)
            .WithName(GetWorkspaceRouteName)
            .WithTags("Workspaces");

        return endpoints;
    }

    /// <summary>
    /// 201 with a <c>Location</c> when the workspace is new; 200 with the same body when the caller
    /// already had one on that version, so "open" is safe to repeat.
    /// </summary>
    private static async Task<IResult> OpenAsync(
        OpenWorkspaceRequest request,
        IWorkspaceLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // The validation filter has already refused an absent or empty id; the default here is only
        // what a caller bypassing it would get, and an empty id finds no version.
        var result = await lifecycle.OpenAsync(
            currentUser.RequireId(),
            request.ProblemVersionId.GetValueOrDefault(),
            cancellationToken);

        return result.Match(
            opened => opened.Created
                ? Results.CreatedAtRoute(GetWorkspaceRouteName, new { id = opened.Workspace.Id }, opened.Workspace)
                : Results.Ok(opened.Workspace),
            error => ApiProblem.ToResult(error, context));
    }

    private static async Task<IResult> GetAsync(
        string id,
        IWorkspaceLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // Ids are opaque strings to a client (ADR 0003). A route constraint would answer a malformed
        // one with an empty 404 outside the error body; parsing here answers it as what it is — a
        // workspace this caller does not have.
        if (!Guid.TryParse(id, out var workspaceId))
        {
            return ApiProblem.ToResult(WorkspaceLifecycle.WorkspaceNotFound(), context);
        }

        var result = await lifecycle.GetAsync(userId, workspaceId, cancellationToken);

        return result.Match(
            workspace => Results.Ok(workspace),
            error => ApiProblem.ToResult(error, context));
    }
}
