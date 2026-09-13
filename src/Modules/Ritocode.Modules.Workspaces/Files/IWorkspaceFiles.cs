using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>
/// A workspace's working tree: which files it holds, what one of them says, and saving one.
/// </summary>
/// <remarks>
/// Every method takes the user explicitly, for the reason <c>IWorkspaceLifecycle</c> gives. All three
/// answer another user's workspace exactly as a missing one, with
/// <c>WorkspaceLifecycle.WorkspaceNotFoundCode</c>, and a path that could leave the tree as a
/// validation error on <c>path</c> before anything is looked up.
/// </remarks>
public interface IWorkspaceFiles
{
    /// <summary>Every file in the workspace, ordered ordinally by path, each saying whether it may be saved.</summary>
    Task<Result<WorkspaceFileTree>> ListAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>One file of the workspace, as text, with the revision a save of it has to name.</summary>
    /// <returns>
    /// The file. Fails with <see cref="WorkspaceFiles.FileNotFoundCode"/> when the tree holds no such
    /// file, and with <see cref="WorkspaceFiles.FileNotTextCode"/> when its bytes are not UTF-8.
    /// </returns>
    Task<Result<WorkspaceFile>> ReadAsync(Guid userId, Guid workspaceId, string? path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an editable file's content with <paramref name="content"/>, encoded as UTF-8 exactly as
    /// given — a byte-order mark in the string is written, and none is added.
    /// </summary>
    /// <param name="baseRevision">
    /// The <see cref="WorkspaceFile.Revision"/> of the copy the change was made to. A save over any
    /// other revision is refused rather than applied, so a change nobody saw is never overwritten.
    /// </param>
    /// <returns>
    /// The file's new size and revision. Fails with <see cref="WorkspaceFiles.FileNotFoundCode"/> for
    /// a path the tree does not hold — a save never creates a file; with
    /// <see cref="WorkspaceFiles.FileReadOnlyCode"/> for a file the version does not let the user
    /// change; with <see cref="WorkspaceFiles.FileChangedCode"/> when the file is no longer at
    /// <paramref name="baseRevision"/>; as a validation error on <c>content</c> when the file would
    /// exceed the version's per-file limit or is not text; and with
    /// <see cref="WorkspaceFiles.LimitExceededCode"/> when the tree would exceed its limits.
    /// </returns>
    Task<Result<SavedWorkspaceFile>> WriteAsync(
        Guid userId,
        Guid workspaceId,
        string? path,
        string content,
        string baseRevision,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The whole tree, never a page of it: an editor cannot use half a file list, and a package's limits
/// bound it at 2000 entries.
/// </summary>
public sealed record WorkspaceFileTree(IReadOnlyList<WorkspaceFileEntry> Files);

/// <param name="Path">Workspace-relative, forward-slashed — the value a read takes back.</param>
/// <param name="SizeBytes">The file's length in bytes, not in characters.</param>
/// <param name="Editable">
/// Whether the problem version lets the user change this file. A save of any other file is refused.
/// </param>
public sealed record WorkspaceFileEntry(string Path, long SizeBytes, bool Editable);

/// <param name="Content">
/// The bytes decoded as UTF-8 and nothing more: a byte-order mark and line endings survive, so the
/// text written back by an editor is the text that was read.
/// </param>
/// <param name="Revision">
/// The SHA-256 of the file's bytes, as lower-case hex. What a save of this copy sends back.
/// </param>
public sealed record WorkspaceFile(string Path, long SizeBytes, string Content, string Revision);

/// <param name="Revision">The saved file's new revision: what the next save of it sends back.</param>
public sealed record SavedWorkspaceFile(string Path, long SizeBytes, string Revision);
