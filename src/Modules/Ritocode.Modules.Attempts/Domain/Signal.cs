namespace Ritocode.Modules.Attempts.Domain;

/// <summary>
/// A learner saying "this card is really here, the answer key missed it" (docs/SPEC.md §4.8): sent
/// from an extra pick of their own submitted attempt, it goes to the author and changes nothing about
/// the attempt's score.
/// </summary>
public sealed class Signal
{
    /// <summary>The longest comment a learner can add.</summary>
    public const int CommentMaxLength = 500;

    private Signal()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid AttemptId { get; private set; }

    /// <summary>The attempt's task, kept so the author's list reads it without the attempt.</summary>
    public string TaskSlug { get; private set; } = string.Empty;

    /// <summary>The card the learner is sure is present. No foreign key: cards live in the Content module's schema.</summary>
    public string Card { get; private set; } = string.Empty;

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the author marked it resolved (SPEC §6.2); null while it is open.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>A signal from <paramref name="attempt"/>, which the caller has checked is submitted and picked <paramref name="card"/> as extra.</summary>
    public static Signal Send(Attempt attempt, string card, string? comment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentException.ThrowIfNullOrWhiteSpace(card);

        if (!attempt.IsSubmitted)
        {
            throw new InvalidOperationException($"Attempt {attempt.Id} is not submitted; there is no extra pick to signal.");
        }

        var text = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        if (text is { Length: > CommentMaxLength })
        {
            throw new ArgumentException($"A comment is at most {CommentMaxLength} characters.", nameof(comment));
        }

        return new Signal
        {
            Id = Guid.CreateVersion7(now),
            UserId = attempt.UserId,
            AttemptId = attempt.Id,
            TaskSlug = attempt.TaskSlug,
            Card = card,
            Comment = text,
            CreatedAt = now,
        };
    }
}
