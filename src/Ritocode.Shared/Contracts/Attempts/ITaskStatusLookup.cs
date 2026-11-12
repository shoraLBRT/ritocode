namespace Ritocode.Shared.Contracts.Attempts;

/// <summary>
/// Answers where a user stands with each task — new, tried, solved or gave up — for the task
/// catalogue (docs/SPEC.md §4.3).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Attempts module and taken by
/// Content. What each status means is the Attempts module's rule (ADR 0017); Content shows it as it
/// comes. A status is one of the names below, the same the API answers with. It takes many tasks at
/// once, so a page of the catalogue is one query rather than one per row (ADR 0007 §6).
/// </remarks>
public interface ITaskStatusLookup
{
    /// <summary>No submitted attempt.</summary>
    const string New = "new";

    /// <summary>Submitted attempts, none of them solving it, and the answer not revealed.</summary>
    const string Tried = "tried";

    /// <summary>An attempt solved it before the answer was revealed, if it ever was.</summary>
    const string Solved = "solved";

    /// <summary>The answer was revealed before any attempt solved it.</summary>
    const string GaveUp = "gaveUp";

    /// <summary>The status of each of <paramref name="taskSlugs"/>; a task with no submitted attempt is <see cref="New"/>.</summary>
    Task<IReadOnlyDictionary<string, string>> FindStatusesAsync(Guid userId, IReadOnlyCollection<string> taskSlugs, CancellationToken cancellationToken);
}
