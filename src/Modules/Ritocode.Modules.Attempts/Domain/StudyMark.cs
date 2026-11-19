namespace Ritocode.Modules.Attempts.Domain;

/// <summary>
/// A learner's mark «Изучено» on a problem card (docs/SPEC.md §4.2 › Studying, ADR 0018): they read the
/// card and say so. One per learner and card; taking it back removes it. A mark changes no task's
/// result, and a card found in tasks is not marked by it.
/// </summary>
public sealed class StudyMark
{
    private StudyMark()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>The card's permanent slug. No foreign key: the card lives in the Content module's schema.</summary>
    public string CardSlug { get; private set; } = string.Empty;

    /// <summary>When the card was marked; marking it again keeps this.</summary>
    public DateTimeOffset StudiedAt { get; private set; }

    /// <summary>The mark of <paramref name="cardSlug"/>, which the caller has checked is a published card.</summary>
    public static StudyMark Of(Guid userId, string cardSlug, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardSlug);

        return new StudyMark
        {
            UserId = userId,
            CardSlug = cardSlug,
            // To the microsecond PostgreSQL keeps, so the date answered on marking is the one read back later.
            StudiedAt = now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000))),
        };
    }
}
