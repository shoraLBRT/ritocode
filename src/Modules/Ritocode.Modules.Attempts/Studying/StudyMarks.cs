using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Shared.Contracts.Content;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Attempts.Studying;

/// <summary>
/// A learner's marks «Изучено» on problem cards (docs/SPEC.md §4.2 › Studying, ADR 0018). Marking and
/// taking a mark back are both idempotent; a mark changes nothing else.
/// </summary>
public interface IStudyMarks
{
    /// <summary>The user's marks on the cards published now; a mark on a retired card is kept and left out.</summary>
    Task<StudiedCardsView> ListAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks a published card; a card already marked keeps its first date.</summary>
    Task<Result<StudyMarkView>> MarkAsync(Guid userId, string cardSlug, CancellationToken cancellationToken);

    /// <summary>Takes the mark on a card back, whether or not there was one.</summary>
    Task UnmarkAsync(Guid userId, string cardSlug, CancellationToken cancellationToken);
}

/// <remarks>
/// Depends on Users, to know the caller is a user, and on Content, for which cards are published.
/// </remarks>
internal sealed class StudyMarks(
    AttemptsDbContext context,
    IUserLookup users,
    IPublishedCardLookup cards,
    TimeProvider clock) : IStudyMarks
{
    /// <summary>The code for a card nobody can study: none by that slug, or one retired.</summary>
    public const string CardNotFoundCode = "card_not_found";

    public async Task<StudiedCardsView> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var marks = await context.StudyMarksOf(userId)
            .OrderBy(mark => mark.StudiedAt)
            .ThenBy(mark => mark.CardSlug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var published = await cards.PublishedAsync([.. marks.Select(mark => mark.CardSlug)], cancellationToken).ConfigureAwait(false);

        return new StudiedCardsView([.. marks.Where(mark => published.Contains(mark.CardSlug)).Select(View)]);
    }

    public async Task<Result<StudyMarkView>> MarkAsync(Guid userId, string cardSlug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardSlug);

        // study_marks.user_id has no foreign key (ADR 0004): this keeps a row from naming a user who does not exist.
        if (await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is null)
        {
            return AppError.Unauthenticated(message: "The authenticated identity does not name a user.");
        }

        var published = await cards.PublishedAsync([cardSlug], cancellationToken).ConfigureAwait(false);

        if (!published.Contains(cardSlug))
        {
            return AppError.NotFound(CardNotFoundCode, "No such card.");
        }

        var existing = await context.StudyMarkOfAsync(userId, cardSlug, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return View(existing);
        }

        var mark = StudyMark.Of(userId, cardSlug, clock.GetUtcNow());
        context.AddStudyMark(mark);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsMarkRace(exception))
        {
            // Another mark of the same card committed in the meantime; it is the one kept.
            context.Entry(mark).State = EntityState.Detached;
            var first = await context.StudyMarkOfAsync(userId, cardSlug, cancellationToken).ConfigureAwait(false);
            return View(first ?? mark);
        }

        return View(mark);
    }

    public Task UnmarkAsync(Guid userId, string cardSlug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardSlug);

        // No check against Content: a mark on a card since retired can still be taken back.
        return context.RemoveStudyMarkAsync(userId, cardSlug, cancellationToken);
    }

    private static StudyMarkView View(StudyMark mark) => new(mark.CardSlug, mark.StudiedAt);

    private static bool IsMarkRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            TableName: StudyMarkConfiguration.TableName,
        };
}
