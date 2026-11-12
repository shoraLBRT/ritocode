namespace Ritocode.Modules.Attempts.Scoring;

/// <summary>
/// What a result says without the answer (docs/SPEC.md §4.5, ADR 0017): the score and the counts, and
/// whether the attempt solved the task. It names no card and no leaf, so it is what the review shows
/// before the learner asks to see the answer.
/// </summary>
/// <param name="Present">The findings in the key.</param>
/// <param name="Found">The findings the answer picked.</param>
/// <param name="Treated">The found ones with at least one of the key's leaves among the answer's.</param>
/// <param name="Missed">The findings the answer did not pick.</param>
/// <param name="Extra">The picks the key does not list.</param>
/// <param name="Solved">Every finding found, each with a leaf the key lists, and no extra pick.</param>
public sealed record AttemptOutcome(int Total, int Maximum, int Present, int Found, int Treated, int Missed, int Extra, bool Solved)
{
    /// <summary>
    /// The outcome of <paramref name="score"/>. "Solved" is the maintainer's rule of 2026-11-12 (#229):
    /// every finding found, no extra pick, and for each finding at least one leaf the key lists — a wrong
    /// leaf beside a right one costs points but does not undo it. On a clean task it is an answer that
    /// picked nothing. It is not <see cref="DiagnosisScore.IsCorrect"/>, which a wrong leaf breaks.
    /// </summary>
    public static AttemptOutcome Of(DiagnosisScore score)
    {
        ArgumentNullException.ThrowIfNull(score);

        var findings = score.Cards.Where(line => line.Outcome != CardOutcome.Extra).ToList();
        var found = findings.Count(line => line.Outcome == CardOutcome.Found);
        var treated = findings.Count(line => line.Outcome == CardOutcome.Found && line.Treatment?.Matched == true);
        var extra = score.Cards.Count - findings.Count;

        return new AttemptOutcome(
            score.Total,
            score.Maximum,
            findings.Count,
            found,
            treated,
            findings.Count - found,
            extra,
            Solved: extra == 0 && treated == findings.Count);
    }
}

/// <summary>Where a learner stands with a task (SPEC §4.3), as the task list shows it.</summary>
public enum LearnerTaskStatus
{
    /// <summary>No submitted attempt.</summary>
    New,

    /// <summary>Submitted attempts, none of them solving it, and the answer not revealed — whatever the score.</summary>
    Tried,

    /// <summary>An attempt solved it before the answer was revealed, if it ever was.</summary>
    Solved,

    /// <summary>The answer was revealed before any attempt solved it.</summary>
    GaveUp,
}

/// <summary>One submitted attempt as the status needs it.</summary>
public sealed record SubmittedAttempt(DateTimeOffset SubmittedAt, bool Solved);

/// <summary>The status of a task for one learner: a pure function of their submitted attempts at it and the reveal.</summary>
public static class TaskStatuses
{
    /// <summary>
    /// <see cref="LearnerTaskStatus.Solved"/> when an attempt solved the task no later than the reveal —
    /// so a task solved and revealed afterwards stays solved, and one revealed first stays gave up
    /// whatever later practice scores; otherwise gave up once revealed, tried once submitted, else new.
    /// </summary>
    public static LearnerTaskStatus Of(IEnumerable<SubmittedAttempt> submitted, DateTimeOffset? revealedAt)
    {
        ArgumentNullException.ThrowIfNull(submitted);

        var attempts = submitted.ToList();

        if (attempts.Any(attempt => attempt.Solved && (revealedAt is null || attempt.SubmittedAt <= revealedAt)))
        {
            return LearnerTaskStatus.Solved;
        }

        if (revealedAt is not null)
        {
            return LearnerTaskStatus.GaveUp;
        }

        return attempts.Count > 0 ? LearnerTaskStatus.Tried : LearnerTaskStatus.New;
    }
}
