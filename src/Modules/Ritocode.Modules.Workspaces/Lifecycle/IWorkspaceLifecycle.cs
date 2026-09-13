using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Workspaces.Lifecycle;

/// <summary>
/// Opening a workspace on a problem version, and reading one back.
/// </summary>
/// <remarks>
/// Every method takes the user explicitly rather than reading <c>ICurrentUser</c>: the endpoint is
/// the one place that knows a request is being served, and a service that read the caller for
/// itself could not be called from anywhere that has no request.
/// </remarks>
public interface IWorkspaceLifecycle
{
    /// <summary>
    /// Opens <paramref name="userId"/>'s workspace on <paramref name="problemVersionId"/>, creating
    /// it — row and starter snapshot — when the user has none on that version yet.
    /// </summary>
    /// <returns>
    /// The workspace, and whether this call created it. Fails with
    /// <see cref="WorkspaceLifecycle.ProblemVersionNotFoundCode"/> for a version that does not exist
    /// or is not published, and as unauthenticated when <paramref name="userId"/> names no user.
    /// </returns>
    Task<Result<OpenedWorkspace>> OpenAsync(Guid userId, Guid problemVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The workspace <paramref name="workspaceId"/>, when it belongs to <paramref name="userId"/>.
    /// Another user's workspace fails exactly as a missing one does, with
    /// <see cref="WorkspaceLifecycle.WorkspaceNotFoundCode"/>.
    /// </summary>
    Task<Result<WorkspaceDetail>> GetAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);
}

/// <summary>What a client is told about a workspace. The snapshot reference stays inside the API.</summary>
public sealed record WorkspaceDetail(
    Guid Id,
    Guid ProblemVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <param name="Created">True when this call created the workspace rather than finding it.</param>
public sealed record OpenedWorkspace(WorkspaceDetail Workspace, bool Created);
