namespace Ritocode.Modules.Attempts.Domain;

/// <summary>
/// A learner asking to see the answer to a task (docs/SPEC.md §4.5, ADR 0017). Until then the review
/// of their attempts at it shows the score and the counts only; from then on it shows everything, for
/// every attempt at the task, earlier and later. One per learner and task, kept for good: an answer
/// once seen cannot be unseen.
/// </summary>
public sealed class Reveal
{
    private Reveal()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>The task's permanent slug. No foreign key: the task lives in the Content module's schema.</summary>
    public string TaskSlug { get; private set; } = string.Empty;

    /// <summary>When the answer was first revealed; an attempt solved before it still solved the task.</summary>
    public DateTimeOffset RevealedAt { get; private set; }

    /// <summary>The reveal of <paramref name="attempt"/>'s task, which the caller has checked is submitted.</summary>
    public static Reveal Of(Attempt attempt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (!attempt.IsSubmitted)
        {
            throw new InvalidOperationException($"Attempt {attempt.Id} is open; the answer is revealed from a submitted one.");
        }

        return new Reveal
        {
            UserId = attempt.UserId,
            TaskSlug = attempt.TaskSlug,
            RevealedAt = now,
        };
    }
}
