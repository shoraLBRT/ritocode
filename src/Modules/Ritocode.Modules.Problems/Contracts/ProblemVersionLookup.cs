using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Contracts.Problems;

namespace Ritocode.Modules.Problems.Contracts;

/// <summary>
/// The Problems module's answer to <see cref="IProblemVersionLookup"/>, over its own schema.
/// </summary>
/// <remarks>
/// Deliberately not the catalog's query. The catalog resolves a problem's highest published
/// version; this reports the version it was asked about, draft or not, because a workspace is
/// pinned to the version it was created from and the consumer owns the rule about drafts.
/// </remarks>
internal sealed class ProblemVersionLookup(ProblemsDbContext context) : IProblemVersionLookup
{
    public Task<ProblemVersionSummary?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.ProblemVersions
            .AsNoTracking()
            .Where(version => version.Id == id)
            .Select(version => new ProblemVersionSummary(
                version.Id,
                version.ProblemId,
                version.Problem!.Slug,
                version.Version,
                version.PublishedAt,
                version.SnapshotReference))
            .FirstOrDefaultAsync(cancellationToken);
}
