using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Domain;

namespace Ritocode.Modules.Workspaces.Persistence;

/// <summary>How every read in this module finds a workspace: by id and by owner, in one query.</summary>
internal static class OwnedWorkspaces
{
    /// <summary>
    /// The workspace <paramref name="workspaceId"/> when it belongs to <paramref name="userId"/>, and
    /// <see langword="null"/> otherwise.
    /// </summary>
    /// <remarks>
    /// Ownership is part of the query rather than a check after it. Another user's workspace and a
    /// missing one are then the same absent row, so they cannot drift into answering differently —
    /// and a 403 would confirm the id exists (ADR 0003).
    /// </remarks>
    public static Task<Workspace?> FindOwnedAsync(
        this IQueryable<Workspace> workspaces,
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == workspaceId && candidate.UserId == userId,
                cancellationToken);
}
