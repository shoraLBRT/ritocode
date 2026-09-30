using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Shared.Contracts.Attempts;

namespace Ritocode.Modules.Attempts.Contracts;

/// <summary>The Attempts module's answer to <see cref="ISubmittedTaskLookup"/>, over its own schema.</summary>
internal sealed class SubmittedTaskLookup(AttemptsDbContext context) : ISubmittedTaskLookup
{
    public async Task<IReadOnlySet<string>> FindSubmittedAsync(
        Guid userId,
        IReadOnlyCollection<string> taskSlugs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskSlugs);

        if (taskSlugs.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var slugs = taskSlugs.ToList();

        var submitted = await context.OwnedBy(userId)
            .Where(attempt => attempt.SubmittedAt != null && slugs.Contains(attempt.TaskSlug))
            .Select(attempt => attempt.TaskSlug)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return submitted.ToHashSet(StringComparer.Ordinal);
    }
}
