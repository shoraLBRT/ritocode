using System.Text;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>Reads and saves a workspace's tree in its snapshot. See <see cref="IWorkspaceFiles"/>.</summary>
/// <remarks>
/// <para>
/// Each call downloads the whole snapshot, from the reference the row stores and never from a key
/// rebuilt from the id (docs/STORAGE_LAYOUT.md rule 3). For the trees the slice's packages produce
/// — kilobytes — that is cheaper than any cache would be to keep correct.
/// </para>
/// <para>
/// A save rewrites that whole snapshot, so two saves that overlapped would each put back a tree
/// missing the other's change — even two saves of different files. Saves to one workspace therefore
/// hold a lock on its row from before the snapshot is read until the commit, and the second waits for
/// the first. Separately, each save names the revision of the file it changed, and a file that moved
/// since is refused: the lock stops saves losing each other's trees, the revision stops a person
/// overwriting a change they never saw.
/// </para>
/// <para>
/// What a file may be saved to comes from the Problems module through
/// <see cref="IWorkspaceAllowanceLookup"/> — the version's resolved editable files and its limits —
/// and what a save that breaks them is answered with is decided here (ADR 0007 §2).
/// </para>
/// </remarks>
public sealed class WorkspaceFiles(
    WorkspacesDbContext context,
    IWorkspaceAllowanceLookup allowances,
    IObjectStore objectStore,
    TimeProvider timeProvider) : IWorkspaceFiles
{
    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string FileNotFoundCode = "workspace_file_not_found";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string FileNotTextCode = "workspace_file_not_text";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string FileReadOnlyCode = "workspace_file_read_only";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string FileChangedCode = "workspace_file_changed";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string LimitExceededCode = "workspace_limit_exceeded";

    /// <summary>
    /// Strict both ways: bytes that are not UTF-8 fail rather than becoming U+FFFD, and a string that
    /// has no UTF-8 form — a lone surrogate — fails rather than being written as a replacement.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<Result<WorkspaceFileTree>> ListAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await context.Workspaces.FindOwnedAsync(userId, workspaceId, cancellationToken);

        if (workspace is null)
        {
            return WorkspaceLifecycle.WorkspaceNotFound();
        }

        await using var snapshot = await DownloadAsync(workspace, cancellationToken);
        var files = await SnapshotArchive.ListAsync(snapshot, cancellationToken);

        var allowance = await RequireAllowanceAsync(workspace, cancellationToken);
        var editable = allowance.EditableFiles.ToHashSet(StringComparer.Ordinal);

        return new WorkspaceFileTree(
            [.. files.Select(file => new WorkspaceFileEntry(file.Path, file.SizeBytes, editable.Contains(file.Path)))]);
    }

    public async Task<Result<WorkspaceFile>> ReadAsync(
        Guid userId,
        Guid workspaceId,
        string? path,
        CancellationToken cancellationToken = default)
    {
        // First, and before the database: a malformed path is wrong whatever the workspace holds, and
        // answering it without a lookup means it cannot tell the caller anything about the workspace.
        if (!WorkspacePath.IsConfined(path))
        {
            return InvalidPath(path);
        }

        var workspace = await context.Workspaces.FindOwnedAsync(userId, workspaceId, cancellationToken);

        if (workspace is null)
        {
            return WorkspaceLifecycle.WorkspaceNotFound();
        }

        await using var snapshot = await DownloadAsync(workspace, cancellationToken);

        if (await SnapshotArchive.ReadAsync(snapshot, path, cancellationToken) is not { } bytes)
        {
            return FileNotFound(path);
        }

        try
        {
            return new WorkspaceFile(path, bytes.LongLength, StrictUtf8.GetString(bytes), FileRevision.Of(bytes));
        }
        catch (DecoderFallbackException)
        {
            // Nothing in the package format promises text, and an editor is the only reader there is.
            // Refusing is honest; decoding leniently would hand back a file that saves as different bytes.
            return AppError.Conflict(FileNotTextCode, $"The file at '{path}' is not UTF-8 text.");
        }
    }

    public async Task<Result<SavedWorkspaceFile>> WriteAsync(
        Guid userId,
        Guid workspaceId,
        string? path,
        string content,
        string baseRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(baseRevision);

        // As for a read, and for the same reason: the path is refused before anything is looked up.
        if (!WorkspacePath.IsConfined(path))
        {
            return InvalidPath(path);
        }

        byte[] bytes;

        try
        {
            bytes = StrictUtf8.GetBytes(content);
        }
        catch (EncoderFallbackException)
        {
            return InvalidContent("Must be text: it holds a character with no UTF-8 encoding.");
        }

        string confinedPath = path;

        // The whole save is the unit a retry repeats, never one statement of it: a retried commit on
        // its own would record a write whose snapshot was read under a lock that no longer exists.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            token => WriteLockedAsync(userId, workspaceId, confinedPath, bytes, baseRevision, token),
            cancellationToken);
    }

    private async Task<Result<SavedWorkspaceFile>> WriteLockedAsync(
        Guid userId,
        Guid workspaceId,
        string path,
        byte[] content,
        string baseRevision,
        CancellationToken cancellationToken)
    {
        // A retried attempt starts from nothing a failed one tracked.
        context.ChangeTracker.Clear();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var workspace = await context.FindOwnedForUpdateAsync(userId, workspaceId, cancellationToken);

        if (workspace is null)
        {
            return WorkspaceLifecycle.WorkspaceNotFound();
        }

        await using var snapshot = await DownloadAsync(workspace, cancellationToken);
        await using var rewritten = ScratchFile.Create();

        var replacement = await SnapshotArchive.ReplaceAsync(snapshot, path, content, rewritten, cancellationToken);

        if (replacement.Previous is null)
        {
            return FileNotFound(path);
        }

        var allowance = await RequireAllowanceAsync(workspace, cancellationToken);

        if (!allowance.EditableFiles.Contains(path, StringComparer.Ordinal))
        {
            // The file exists and the user can read it, so this is not a 404: the resource is visible,
            // and the action on it is what is refused.
            return AppError.Forbidden(FileReadOnlyCode, $"The file at '{path}' cannot be changed in this problem.");
        }

        if (!string.Equals(FileRevision.Of(replacement.Previous), baseRevision, StringComparison.Ordinal))
        {
            return AppError.PreconditionFailed(
                FileChangedCode,
                $"The file at '{path}' has changed since that copy of it was read.");
        }

        var revision = FileRevision.Of(content);

        if (string.Equals(revision, baseRevision, StringComparison.Ordinal))
        {
            // Saving what is already stored writes nothing and leaves updatedAt where it was. Disposing
            // the transaction without committing releases the lock.
            return new SavedWorkspaceFile(path, content.LongLength, revision);
        }

        if (content.LongLength > allowance.MaxFileBytes)
        {
            return InvalidContent(
                $"Must be at most {allowance.MaxFileBytes} bytes as UTF-8; this is {content.LongLength}.");
        }

        if (replacement.TotalBytes > allowance.MaxTotalBytes || replacement.FileCount > allowance.MaxFiles)
        {
            // Not the content's fault alone: the total depends on every other file in the tree, so this
            // is a conflict with the workspace's state rather than a malformed request.
            return AppError.Conflict(
                LimitExceededCode,
                $"Saving this would make the workspace {replacement.TotalBytes} bytes in {replacement.FileCount} files; "
                + $"it may hold at most {allowance.MaxTotalBytes} bytes in {allowance.MaxFiles} files.");
        }

        rewritten.Position = 0;

        // The snapshot is put before the row commits, and under the lock. If the commit then fails, the
        // tree holds the save and updated_at does not — and the revision a read reports is still
        // right, because it is derived from what is stored.
        await objectStore.PutAsync(workspace.SnapshotReference, rewritten, cancellationToken);

        workspace.RecordWrite(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SavedWorkspaceFile(path, content.LongLength, revision);
    }

    private static AppError FileNotFound(string path) =>
        AppError.NotFound(FileNotFoundCode, $"The workspace holds no file at '{path}'.");

    private static AppError InvalidPath(string? path) =>
        AppError.Validation(
            "The path is not a workspace file path.",
            new Dictionary<string, string[]>
            {
                ["path"] =
                [
                    string.IsNullOrEmpty(path)
                        ? "A path is required."
                        : "Must be relative and use '/', with no empty, '.' or '..' segment.",
                ],
            });

    private static AppError InvalidContent(string message) =>
        AppError.Validation(
            "The content cannot be saved.",
            new Dictionary<string, string[]> { ["content"] = [message] });

    /// <summary>
    /// The allowance of the version the workspace was opened on. A workspace outliving its version is a
    /// broken store rather than something a caller could have asked differently: nothing deletes a
    /// version, and #43 is where that would change.
    /// </summary>
    private async Task<WorkspaceAllowance> RequireAllowanceAsync(Workspace workspace, CancellationToken cancellationToken) =>
        await allowances.FindAsync(workspace.ProblemVersionId, cancellationToken)
        ?? throw new InvalidOperationException(
            $"Workspace {workspace.Id} was opened on problem version {workspace.ProblemVersionId}, which does not exist.");

    private async Task<FileStream> DownloadAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var snapshot = ScratchFile.Create();

        try
        {
            if (!await objectStore.GetAsync(workspace.SnapshotReference, snapshot, cancellationToken))
            {
                // Opening writes the snapshot before the row naming it commits, so a row with no
                // snapshot is a broken store, not something the caller could have asked differently.
                throw new InvalidOperationException(
                    $"Workspace {workspace.Id} exists, but its snapshot '{workspace.SnapshotReference}' does not.");
            }

            snapshot.Position = 0;
            return snapshot;
        }
        catch
        {
            await snapshot.DisposeAsync();
            throw;
        }
    }
}
