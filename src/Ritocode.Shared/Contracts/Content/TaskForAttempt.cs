namespace Ritocode.Shared.Contracts.Content;

/// <summary>What <see cref="ITaskForAttemptLookup"/> reports about a task (ADR 0007 §3).</summary>
/// <param name="Slug">The task's permanent identifier.</param>
/// <param name="Published">False once the task has left <c>content/</c>; its row, and its key, remain.</param>
/// <param name="ContentRevision">The commit the task was loaded from, recorded on an attempt scored against it.</param>
/// <param name="Findings">The answer key, each finding with its card's weight, in the author's order.</param>
/// <param name="OfferedCards">The cards step 1 offers: the shortlist for an easy task, every live card otherwise.</param>
/// <param name="Leaves">Every leaf of the treatment tree, as <c>branch.leaf</c>.</param>
public sealed record TaskForAttempt(
    string Slug,
    bool Published,
    string ContentRevision,
    IReadOnlyList<TaskFinding> Findings,
    IReadOnlyList<string> OfferedCards,
    IReadOnlyList<string> Leaves);

/// <summary>One finding of an answer key: a card, the card's weight, and the leaves right for it, any of them.</summary>
public sealed record TaskFinding(string Card, int Weight, IReadOnlyList<string> Leaves);
