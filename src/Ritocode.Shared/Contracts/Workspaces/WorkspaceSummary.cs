using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Contracts.Workspaces;

/// <summary>What a consumer outside the Workspaces module may know about a workspace.</summary>
/// <param name="Id">The workspace.</param>
/// <param name="ProblemVersionId">The version it was opened on, which is the version a submission of it is graded against.</param>
/// <param name="SnapshotReference">
/// Where its current tree is, read back from the row rather than rebuilt from the id
/// (docs/STORAGE_LAYOUT.md rule 3). A submission copies this object; nothing outside Workspaces writes it.
/// </param>
public sealed record WorkspaceSummary(
    Guid Id,
    Guid ProblemVersionId,
    StorageReference SnapshotReference);
