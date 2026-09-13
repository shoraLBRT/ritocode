using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Workspaces.Lifecycle;

/// <summary>The workspace lifecycle over the module's own schema. See <see cref="IWorkspaceLifecycle"/>.</summary>
/// <remarks>
/// <para>
/// The constructor is the module's dependency list, and it is meant to be read as one (ADR 0007 §1):
/// Workspaces depends on Users and on Problems, through one question each, and on nothing else of
/// theirs.
/// </para>
/// <para>
/// A user has one workspace per problem version. Opening a version the user already has a workspace
/// on returns that workspace rather than a second, empty one — the draft a person left is the thing
/// "open" has to find again. The schema does not enforce this, and two concurrent first opens can
/// both create; the later open then returns the most recently written of them. That race costs an
/// unused row, not lost work, so it is accepted rather than locked against — see
/// docs/PROJECT_STATE.md.
/// </para>
/// </remarks>
public sealed class WorkspaceLifecycle(
    WorkspacesDbContext context,
    IUserLookup users,
    IProblemVersionLookup problemVersions,
    IObjectStore objectStore,
    TimeProvider timeProvider) : IWorkspaceLifecycle
{
    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string ProblemVersionNotFoundCode = "problem_version_not_found";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string WorkspaceNotFoundCode = "workspace_not_found";

    public async Task<Result<OpenedWorkspace>> OpenAsync(
        Guid userId,
        Guid problemVersionId,
        CancellationToken cancellationToken = default)
    {
        // No foreign key crosses into users or problems (ADR 0004), so these two questions are the
        // only thing standing between a request and a row that points at nothing. Both are queries
        // outside any transaction of ours (ADR 0007 §6): something removed between the check and the
        // insert still leaves an orphan, which is accepted and is #43's to clean.
        if (await users.FindAsync(userId, cancellationToken) is null)
        {
            // Authenticated, and naming no user: a credential for nobody. That is answered the way a
            // missing credential is, not as a missing resource the caller could correct.
            return AppError.Unauthenticated(message: "The authenticated identity does not name a user.");
        }

        var version = await problemVersions.FindAsync(problemVersionId, cancellationToken);

        // This module's rule, not Problems': a workspace opens on a published version only. A draft
        // is answered exactly like a version that does not exist — the catalog never showed it, and
        // the API should not confirm it is there.
        if (version is null || version.PublishedAt is null)
        {
            return AppError.NotFound(
                ProblemVersionNotFoundCode,
                $"No published problem version is identified by '{problemVersionId}'.");
        }

        var existing = await context.Workspaces
            .AsNoTracking()
            .Where(workspace => workspace.UserId == userId && workspace.ProblemVersionId == problemVersionId)
            .OrderByDescending(workspace => workspace.UpdatedAt)
            .ThenByDescending(workspace => workspace.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return new OpenedWorkspace(Detail(existing), Created: false);
        }

        var created = Workspace.Create(userId, problemVersionId, timeProvider.GetUtcNow());

        // The snapshot is written before the row that points at it commits — ingest's ordering, for
        // ingest's reason. A failure in between leaves an object no row references, keyed by an id
        // nothing else will produce. The other order leaves a workspace whose tree is missing, which
        // every later read would have to handle.
        await MaterialiseAsync(version, created.SnapshotReference, cancellationToken);

        context.Workspaces.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        return new OpenedWorkspace(Detail(created), Created: true);
    }

    public async Task<Result<WorkspaceDetail>> GetAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        // Ownership is part of the query rather than a check after it. Another user's workspace and a
        // missing one are then the same absent row, so they cannot drift into answering differently —
        // and a 403 would confirm the id exists (ADR 0003).
        var workspace = await context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == workspaceId && candidate.UserId == userId,
                cancellationToken);

        return workspace is null ? WorkspaceNotFound() : Detail(workspace);
    }

    internal static AppError WorkspaceNotFound() =>
        AppError.NotFound(WorkspaceNotFoundCode, "No such workspace.");

    private static WorkspaceDetail Detail(Workspace workspace) =>
        new(workspace.Id, workspace.ProblemVersionId, workspace.CreatedAt, workspace.UpdatedAt);

    /// <summary>
    /// Reads the version's bundle from the reference its row stores — never a key rebuilt from the
    /// id (docs/STORAGE_LAYOUT.md rule 3) — and writes its starter tree as the workspace's first
    /// snapshot.
    /// </summary>
    private async Task MaterialiseAsync(
        ProblemVersionSummary version,
        StorageReference snapshot,
        CancellationToken cancellationToken)
    {
        await using var bundle = TemporaryFile();

        if (!await objectStore.GetAsync(version.SnapshotReference, bundle, cancellationToken))
        {
            // Ingest writes a bundle before the row naming it commits, so a published version with
            // no bundle is a broken store, not a request the caller could have made differently.
            throw new InvalidOperationException(
                $"Problem version {version.Id} is published, but its bundle '{version.SnapshotReference}' does not exist.");
        }

        bundle.Position = 0;

        await using var tree = TemporaryFile();
        await StarterTree.WriteAsync(bundle, version.WorkspaceRoot, tree, cancellationToken);
        tree.Position = 0;

        await objectStore.PutAsync(snapshot, tree, cancellationToken);
    }

    /// <summary>
    /// A file rather than memory, as ingest does: a put is signed over a known length, and a package's
    /// limits allow a workspace of up to 100 MiB. DeleteOnClose is what leaves nothing behind when
    /// materialising fails.
    /// </summary>
    private static FileStream TemporaryFile() =>
        new(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            });
}
