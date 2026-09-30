using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>How every read in this module finds a signal: by owner, inside the query, as <see cref="OwnedAttempts"/> does for attempts.</summary>
internal static class OwnedSignals
{
    /// <summary>Every signal <paramref name="userId"/> sent, untracked and unordered, for a caller to filter.</summary>
    public static IQueryable<Signal> SignalsOf(this AttemptsDbContext context, Guid userId) =>
        context.Signals
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId);
}
