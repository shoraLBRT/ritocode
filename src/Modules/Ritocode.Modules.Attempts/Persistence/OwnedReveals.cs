using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>How every read in this module finds a reveal: by owner, inside the query, as <see cref="OwnedAttempts"/> does for attempts.</summary>
internal static class OwnedReveals
{
    /// <summary>Every task whose answer <paramref name="userId"/> has revealed, untracked and unordered, for a caller to filter.</summary>
    public static IQueryable<Reveal> RevealsOf(this AttemptsDbContext context, Guid userId) =>
        context.Reveals
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId);

    /// <summary>When <paramref name="userId"/> revealed the answer to <paramref name="taskSlug"/>, or <see langword="null"/>.</summary>
    public static Task<DateTimeOffset?> RevealedAtAsync(
        this AttemptsDbContext context,
        Guid userId,
        string taskSlug,
        CancellationToken cancellationToken) =>
        context.RevealsOf(userId)
            .Where(candidate => candidate.TaskSlug == taskSlug)
            .Select(candidate => (DateTimeOffset?)candidate.RevealedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
