using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Domain;

namespace Ritocode.Modules.Workspaces.Persistence;

/// <summary>How every read in this module finds a workspace: by id and by owner, in one query.</summary>
/// <remarks>
/// Both lookups take the context rather than a query over it, so a caller never holds the workspace
/// set and cannot compose a lookup of its own onto it. This class and the creation in
/// <c>WorkspaceLifecycle.OpenAsync</c> are the only code in the module allowed to reach that set;
/// <c>OwnershipRuleTests</c> in the architecture tests fails on any other.
/// </remarks>
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
        this WorkspacesDbContext context,
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == workspaceId && candidate.UserId == userId,
                cancellationToken);

    /// <summary>
    /// As <see cref="FindOwnedAsync"/>, tracked, and with the row locked until the caller's transaction
    /// ends — for a writer that rewrites the workspace's snapshot.
    /// </summary>
    /// <remarks>
    /// The snapshot is one object rewritten whole, so two writers that both read it before either
    /// wrote would each put back a tree missing the other's change. Holding this lock from the read of
    /// the snapshot to the commit makes every writer start from the tree the previous one left. It
    /// only serialises writers that take it: a new writer of the snapshot has to come through here.
    /// </remarks>
    public static async Task<Workspace?> FindOwnedForUpdateAsync(
        this WorkspacesDbContext context,
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        // Not composed with any LINQ operator, so EF sends this statement as written rather than
        // wrapping it in a subquery the locking clause would then sit inside.
        var rows = await context.Workspaces
            .FromSql($"SELECT * FROM workspaces.workspaces WHERE id = {workspaceId} AND user_id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }
}
