using System.Text;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>Reads a workspace's tree from its snapshot. See <see cref="IWorkspaceFiles"/>.</summary>
/// <remarks>
/// Each call downloads the whole snapshot, from the reference the row stores and never from a key
/// rebuilt from the id (docs/STORAGE_LAYOUT.md rule 3). For the trees the slice's packages produce
/// — kilobytes — that is cheaper than any cache would be to keep correct once #12 starts writing.
/// </remarks>
public sealed class WorkspaceFiles(WorkspacesDbContext context, IObjectStore objectStore) : IWorkspaceFiles
{
    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string FileNotFoundCode = "workspace_file_not_found";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string FileNotTextCode = "workspace_file_not_text";

    /// <summary>Strict: a byte sequence that is not UTF-8 fails rather than becoming U+FFFD.</summary>
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

        return new WorkspaceFileTree(await SnapshotArchive.ListAsync(snapshot, cancellationToken));
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
            return AppError.NotFound(FileNotFoundCode, $"The workspace holds no file at '{path}'.");
        }

        try
        {
            return new WorkspaceFile(path, bytes.LongLength, StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            // Nothing in the package format promises text, and an editor is the only reader there is.
            // Refusing is honest; decoding leniently would hand back a file that saves as different bytes.
            return AppError.Conflict(FileNotTextCode, $"The file at '{path}' is not UTF-8 text.");
        }
    }

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
