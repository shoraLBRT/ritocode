using Ritocode.Modules.Problems.Packaging;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Problems.Ingest;

/// <summary>
/// Turns a validated problem package into catalog content: a <c>Problem</c> row, a new
/// <c>ProblemVersion</c>, and the bundle those point at in object storage.
/// </summary>
/// <remarks>
/// The input is a <see cref="ProblemPackage"/>, which only <see cref="ProblemPackageLoader"/>
/// produces — so ingest cannot be handed content that has not been checked against
/// docs/PROBLEM_PACKAGE_SPEC.md, and this interface has no "is it valid" answer to give.
/// </remarks>
public interface IProblemIngest
{
    /// <summary>
    /// Ingests <paramref name="package"/> as a new published version of its slug's problem,
    /// creating the problem when nothing carries that slug yet.
    /// </summary>
    /// <exception cref="ObjectStoreException">The bundle could not be written.</exception>
    Task<IngestedProblemVersion> IngestAsync(ProblemPackage package, CancellationToken cancellationToken = default);
}

/// <param name="Bundle">Where the bundle was written, as stored in <c>snapshot_reference</c>.</param>
public sealed record IngestedProblemVersion(
    Guid ProblemId,
    Guid ProblemVersionId,
    string Slug,
    int Version,
    StorageReference Bundle);
