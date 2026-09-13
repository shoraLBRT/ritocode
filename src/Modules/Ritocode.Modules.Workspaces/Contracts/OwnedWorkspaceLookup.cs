using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Contracts.Workspaces;

namespace Ritocode.Modules.Workspaces.Contracts;

/// <summary>
/// The Workspaces module's answer to <see cref="IOwnedWorkspaceLookup"/>, over its own schema.
/// </summary>
/// <remarks>
/// Through <see cref="OwnedWorkspaces.FindOwnedAsync"/>, the lookup the module's own endpoints use, so
/// another user's workspace is the same absent row here as it is there.
/// </remarks>
internal sealed class OwnedWorkspaceLookup(WorkspacesDbContext context) : IOwnedWorkspaceLookup
{
    public async Task<WorkspaceSummary?> FindAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken) =>
        await context.FindOwnedAsync(userId, workspaceId, cancellationToken) is { } workspace
            ? new WorkspaceSummary(workspace.Id, workspace.ProblemVersionId, workspace.SnapshotReference)
            : null;
}
