using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>How every read in this module finds an attempt: by owner, inside the query.</summary>
/// <remarks>
/// Another user's attempt and a missing one are then the same absent row, so they cannot drift into
/// answering differently, and a 403 would confirm the id exists (ADR 0003). This class and the
/// creation in <c>AttemptLifecycle.StartAsync</c> are the only code in the module allowed to reach the
/// attempt set; <c>OwnershipRuleTests</c> in the architecture tests fails on any other.
/// </remarks>
internal static class OwnedAttempts
{
    /// <summary>
    /// The attempt <paramref name="attemptId"/>, tracked, when it belongs to <paramref name="userId"/>,
    /// and <see langword="null"/> otherwise.
    /// </summary>
    public static Task<Attempt?> FindOwnedAsync(
        this AttemptsDbContext context,
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        context.Attempts.FirstOrDefaultAsync(
            candidate => candidate.Id == attemptId && candidate.UserId == userId,
            cancellationToken);

    /// <summary>
    /// Every attempt of <paramref name="userId"/>, untracked and unordered, for a caller to filter and
    /// page. The owner is already in the query, so nothing composed onto it can widen it to another
    /// user's rows.
    /// </summary>
    public static IQueryable<Attempt> OwnedBy(this AttemptsDbContext context, Guid userId) =>
        context.Attempts
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId);
}
