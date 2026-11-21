namespace Ritocode.Shared.Contracts.Content;

/// <summary>
/// Answers every task the product knows, published or not, with what the admin area's per-task
/// statistics (#305) set the attempts against: the title, the difficulty, the material's size and the
/// difficulty's size band (docs/SPEC.md §3.4).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Content module and taken by
/// the Attempts module's admin statistics. It names no finding: the answer key stays behind
/// <see cref="ITaskForAttemptLookup"/>.
/// </remarks>
public interface ITaskSummaryLookup
{
    /// <summary>Every task, unpublished ones included: attempts at them still count.</summary>
    Task<IReadOnlyList<TaskSummary>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>One task as <see cref="ITaskSummaryLookup"/> reports it.</summary>
/// <param name="Slug">The task's permanent identifier.</param>
/// <param name="Title">The title in the default locale, or the slug when it has none.</param>
/// <param name="Difficulty"><c>easy</c>, <c>medium</c> or <c>hard</c>, as the API names it.</param>
/// <param name="Published">False once the task has left <c>content/</c>.</param>
/// <param name="Lines">The material's total lines.</param>
/// <param name="Band">The difficulty's size band.</param>
public sealed record TaskSummary(string Slug, string Title, string Difficulty, bool Published, int Lines, SizeBand Band);

/// <summary>
/// A difficulty's size band (SPEC §3.4): the most lines a material should have, and the estimate of
/// the whole task in minutes — the figure recorded attempt times are there to calibrate.
/// </summary>
public sealed record SizeBand(int MaxLines, int MinutesFrom, int MinutesTo);
