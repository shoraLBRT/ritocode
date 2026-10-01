using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Shared.Contracts.Attempts;

namespace Ritocode.Modules.Attempts.Contracts;

/// <summary>The Attempts module's answer to <see cref="IAttemptTallyLookup"/>, over its own schema.</summary>
internal sealed class AttemptTallyLookup(AttemptsDbContext context) : IAttemptTallyLookup
{
    public async Task<IReadOnlyDictionary<Guid, AttemptTally>> TallyAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, AttemptTally>();
        }

        var ids = userIds.ToList();

        var tallies = await context.EveryonesAttempts()
            .Where(attempt => ids.Contains(attempt.UserId))
            .GroupBy(attempt => attempt.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Attempts = group.Count(),
                TasksSolved = group.Where(attempt => attempt.SubmittedAt != null).Select(attempt => attempt.TaskSlug).Distinct().Count(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return tallies.ToDictionary(tally => tally.UserId, tally => new AttemptTally(tally.Attempts, tally.TasksSolved));
    }
}
