namespace Ritocode.Shared.Contracts.Problems;

/// <summary>
/// What <see cref="IWorkspaceAllowanceLookup"/> reports about a problem version. Its own record rather
/// than the Problems module's entity (ADR 0007 §3).
/// </summary>
/// <param name="ProblemVersionId">The version this allowance belongs to.</param>
/// <param name="EditableFiles">
/// Workspace-relative paths a user may change, ordered ordinally. The manifest's globs already
/// resolved against the starter tree, so a consumer matches a path by equality and never has to know
/// the glob syntax — which is format knowledge, and belongs to the Problems module. Empty for a
/// version ingested before this was recorded: nothing is editable until the version says so.
/// </param>
/// <param name="MaxFiles">The most files a workspace tree may hold.</param>
/// <param name="MaxFileBytes">The largest one file may be, in bytes.</param>
/// <param name="MaxTotalBytes">The largest the whole tree may be, in bytes.</param>
public sealed record WorkspaceAllowance(
    Guid ProblemVersionId,
    IReadOnlyList<string> EditableFiles,
    int MaxFiles,
    int MaxFileBytes,
    int MaxTotalBytes);
