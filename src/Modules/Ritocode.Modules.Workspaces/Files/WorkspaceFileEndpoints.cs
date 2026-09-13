using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>
/// <c>GET /api/v1/workspaces/{id}/files</c> and <c>GET /api/v1/workspaces/{id}/files/content?path=</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file is addressed by a query parameter, not by the rest of the URL path. The server removes
/// <c>.</c> and <c>..</c> segments from a request path before routing, so a path carried there is not
/// the path the client sent: <c>files/../problem.yaml</c> would arrive as some other route entirely,
/// and the rule that refuses it could never be exercised, only trusted. A query value arrives verbatim,
/// is refused by name as <c>errors.path</c>, and needs no per-segment escaping from a client.
/// </para>
/// <para>
/// Like the lifecycle endpoints, neither says anything about authorisation: the host's fallback
/// policy closes them.
/// </para>
/// </remarks>
internal static class WorkspaceFileEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id}/files", ListAsync)
            .WithName("ListWorkspaceFiles")
            .WithTags("Workspaces");

        endpoints.MapGet("/{id}/files/content", ReadAsync)
            .WithName("ReadWorkspaceFile")
            .WithTags("Workspaces");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string id,
        IWorkspaceFiles files,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // As GET /{id}: an id that is not a GUID is a workspace this caller does not have.
        if (!Guid.TryParse(id, out var workspaceId))
        {
            return ApiProblem.ToResult(WorkspaceLifecycle.WorkspaceNotFound(), context);
        }

        var result = await files.ListAsync(userId, workspaceId, cancellationToken);

        return result.Match(
            tree => Results.Ok(tree),
            error => ApiProblem.ToResult(error, context));
    }

    private static async Task<IResult> ReadAsync(
        string id,
        string? path,
        IWorkspaceFiles files,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        if (!Guid.TryParse(id, out var workspaceId))
        {
            return ApiProblem.ToResult(WorkspaceLifecycle.WorkspaceNotFound(), context);
        }

        var result = await files.ReadAsync(userId, workspaceId, path, cancellationToken);

        return result.Match(
            file => Results.Ok(file),
            error => ApiProblem.ToResult(error, context));
    }
}
