using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Contracts.Problems;

/// <summary>
/// What <see cref="IProblemVersionLookup"/> reports about a problem version. Its own record rather
/// than the Problems module's entity (ADR 0007 §3).
/// </summary>
/// <param name="Id">The version's identifier, as stored in <c>problems.problem_versions.id</c>.</param>
/// <param name="ProblemId">The problem this is a revision of.</param>
/// <param name="Slug">The problem's catalog address.</param>
/// <param name="Version">Monotonic per problem, starting at 1.</param>
/// <param name="PublishedAt"><see langword="null"/> while the version is a draft.</param>
/// <param name="SnapshotReference">
/// Where the version's bundle lives. Carried here because a workspace is materialised from it, and
/// docs/STORAGE_LAYOUT.md requires a key to be read back from the row that stores it rather than
/// rebuilt from an identifier — which a consumer outside the Problems module could only do by
/// rebuilding it.
/// </param>
public sealed record ProblemVersionSummary(
    Guid Id,
    Guid ProblemId,
    string Slug,
    int Version,
    DateTimeOffset? PublishedAt,
    StorageReference SnapshotReference);
