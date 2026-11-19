namespace Ritocode.Shared.Contracts.Attempts;

/// <summary>
/// Answers what learners did in a window of time, for the admin area's usage over time (#304).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Attempts module and taken by
/// the host's admin activity endpoint, which buckets the moments it returns.
/// </remarks>
public interface ILearnerActivityLookup
{
    /// <summary>Every moment from <paramref name="from"/> (included) to <paramref name="to"/> (excluded), whoever it belongs to.</summary>
    Task<LearnerActivity> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

/// <summary>One learner's act at one moment.</summary>
public sealed record LearnerMoment(Guid UserId, DateTimeOffset At);

/// <summary>
/// The moments of a window, by kind. A learner is active at a submitted attempt or a study mark, as the
/// streak counts since #281.
/// </summary>
public sealed record LearnerActivity(
    IReadOnlyList<LearnerMoment> AttemptsStarted,
    IReadOnlyList<LearnerMoment> AttemptsSubmitted,
    IReadOnlyList<LearnerMoment> AnswersRevealed,
    IReadOnlyList<LearnerMoment> SignalsSent,
    IReadOnlyList<LearnerMoment> CardsStudied);
