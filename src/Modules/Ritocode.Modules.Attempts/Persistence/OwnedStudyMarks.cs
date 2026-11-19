using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>
/// How every read and write in this module reaches a study mark: by owner, inside the query, as
/// <see cref="OwnedAttempts"/> does for attempts.
/// </summary>
internal static class OwnedStudyMarks
{
    /// <summary>Every card <paramref name="userId"/> has marked, retired ones included, untracked and unordered.</summary>
    public static IQueryable<StudyMark> StudyMarksOf(this AttemptsDbContext context, Guid userId) =>
        context.StudyMarks
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId);

    /// <summary><paramref name="userId"/>'s mark on <paramref name="cardSlug"/>, or <see langword="null"/>.</summary>
    public static Task<StudyMark?> StudyMarkOfAsync(
        this AttemptsDbContext context,
        Guid userId,
        string cardSlug,
        CancellationToken cancellationToken) =>
        context.StudyMarksOf(userId)
            .FirstOrDefaultAsync(candidate => candidate.CardSlug == cardSlug, cancellationToken);

    /// <summary>Adds the mark its caller made for themselves; saving it is the caller's.</summary>
    public static void AddStudyMark(this AttemptsDbContext context, StudyMark mark) => context.StudyMarks.Add(mark);

    /// <summary>Removes <paramref name="userId"/>'s mark on <paramref name="cardSlug"/>, if there is one.</summary>
    public static Task<int> RemoveStudyMarkAsync(
        this AttemptsDbContext context,
        Guid userId,
        string cardSlug,
        CancellationToken cancellationToken) =>
        context.StudyMarks
            .Where(candidate => candidate.UserId == userId && candidate.CardSlug == cardSlug)
            .ExecuteDeleteAsync(cancellationToken);
}
