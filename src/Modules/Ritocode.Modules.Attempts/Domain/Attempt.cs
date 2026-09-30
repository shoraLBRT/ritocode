namespace Ritocode.Modules.Attempts.Domain;

/// <summary>
/// One learner's answer to one task, and its result (docs/SPEC.md §5.4). Started when the learner
/// opens the task, moved on as they reach step 2, and submitted once — after which nothing in it
/// changes again, whatever later happens to the content or to the scoring parameters.
/// </summary>
public sealed class Attempt
{
    private Attempt()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>The task's permanent slug. No foreign key: the task lives in the Content module's schema.</summary>
    public string TaskSlug { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>The furthest step reached: part of the journal that calibrates the size bands (SPEC §8).</summary>
    public AttemptStep Step { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    /// <summary>The content revision the answer was scored against. Set on submit.</summary>
    public string? ContentRevision { get; private set; }

    /// <summary>The answer as submitted, as JSON. Set on submit.</summary>
    public string? Answer { get; private set; }

    /// <summary>The score line by line, with the key revealed, as JSON. Set on submit, never rewritten.</summary>
    public string? Result { get; private set; }

    /// <summary>
    /// The author's words for the review — a note per finding and the lesson — as JSON, taken with the
    /// key on submit so the review always matches what was scored. Null on attempts submitted before
    /// it was kept.
    /// </summary>
    public string? Review { get; private set; }

    public int? Score { get; private set; }

    public int? MaxScore { get; private set; }

    /// <summary>
    /// Whether this is the user's first submitted attempt at the task, the one that counts toward
    /// progress. Every later one is practice (SPEC §5.4). False until submitted.
    /// </summary>
    public bool CountsTowardProgress { get; private set; }

    public bool IsSubmitted => SubmittedAt is not null;

    public static Attempt Start(Guid userId, string taskSlug, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskSlug);

        return new Attempt
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            TaskSlug = taskSlug,
            StartedAt = now,
            Step = AttemptStep.Diagnosis,
        };
    }

    /// <summary>Records that <paramref name="step"/> was reached. A step is never un-reached.</summary>
    public void Reach(AttemptStep step)
    {
        EnsureOpen();

        if (step > Step)
        {
            Step = step;
        }
    }

    public void Submit(
        DateTimeOffset now,
        string contentRevision,
        string answer,
        string result,
        string review,
        int score,
        int maxScore,
        bool countsTowardProgress)
    {
        EnsureOpen();

        SubmittedAt = now;
        ContentRevision = contentRevision;
        Answer = answer;
        Result = result;
        Review = review;
        Score = score;
        MaxScore = maxScore;
        CountsTowardProgress = countsTowardProgress;

        // Checking comes after both steps: a submitted attempt has been through them.
        Step = AttemptStep.Treatment;
    }

    /// <summary>Makes a submitted attempt practice, when a first one committed in the meantime already counts.</summary>
    public void CountAsPractice() => CountsTowardProgress = false;

    private void EnsureOpen()
    {
        if (IsSubmitted)
        {
            throw new InvalidOperationException($"Attempt {Id} is submitted; nothing in it changes again.");
        }
    }
}

/// <summary>The steps of solving a task (SPEC §4.4), in order.</summary>
public enum AttemptStep
{
    Diagnosis = 1,
    Treatment = 2,
}
