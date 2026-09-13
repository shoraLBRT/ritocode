using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Contracts.Problems;

namespace Ritocode.Modules.Problems.Contracts;

/// <summary>
/// The Problems module's answer to <see cref="IWorkspaceAllowanceLookup"/>, over its own schema.
/// </summary>
/// <remarks>
/// Reports the version asked about, draft or not, for the reason <see cref="ProblemVersionLookup"/>
/// gives: a workspace is pinned to its version, and the rule about drafts is the consumer's.
/// </remarks>
internal sealed class WorkspaceAllowanceLookup(ProblemsDbContext context) : IWorkspaceAllowanceLookup
{
    public Task<WorkspaceAllowance?> FindAsync(Guid problemVersionId, CancellationToken cancellationToken) =>
        context.ProblemVersions
            .AsNoTracking()
            .Where(version => version.Id == problemVersionId)
            .Select(version => new WorkspaceAllowance(
                version.Id,
                version.EditableFiles,
                version.MaxFiles,
                version.MaxFileBytes,
                version.MaxTotalBytes))
            .FirstOrDefaultAsync(cancellationToken);
}
