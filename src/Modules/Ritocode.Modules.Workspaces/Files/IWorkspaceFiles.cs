using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>
/// Reading a workspace's working tree: which files it holds, and what one of them says.
/// </summary>
/// <remarks>
/// Every method takes the user explicitly, for the reason <c>IWorkspaceLifecycle</c> gives. Both
/// answer another user's workspace exactly as a missing one, with
/// <c>WorkspaceLifecycle.WorkspaceNotFoundCode</c>.
/// </remarks>
public interface IWorkspaceFiles
{
    /// <summary>Every file in the workspace, ordered ordinally by path.</summary>
    Task<Result<WorkspaceFileTree>> ListAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>One file of the workspace, as text.</summary>
    /// <returns>
    /// The file. Fails as a validation error on <c>path</c> when <paramref name="path"/> is not a
    /// workspace path — checked before anything is looked up; with
    /// <see cref="WorkspaceFiles.FileNotFoundCode"/> when the tree holds no such file; and with
    /// <see cref="WorkspaceFiles.FileNotTextCode"/> when its bytes are not UTF-8.
    /// </returns>
    Task<Result<WorkspaceFile>> ReadAsync(Guid userId, Guid workspaceId, string? path, CancellationToken cancellationToken = default);
}

/// <summary>
/// The whole tree, never a page of it: an editor cannot use half a file list, and a package's limits
/// bound it at 2000 entries.
/// </summary>
public sealed record WorkspaceFileTree(IReadOnlyList<WorkspaceFileEntry> Files);

/// <param name="Path">Workspace-relative, forward-slashed — the value a read takes back.</param>
/// <param name="SizeBytes">The file's length in bytes, not in characters.</param>
public sealed record WorkspaceFileEntry(string Path, long SizeBytes);

/// <param name="Content">
/// The bytes decoded as UTF-8 and nothing more: a byte-order mark and line endings survive, so the
/// text written back by an editor is the text that was read.
/// </param>
public sealed record WorkspaceFile(string Path, long SizeBytes, string Content);
