namespace Ritocode.Shared.Contracts.Content;

/// <summary>
/// Answers what the Attempts module needs to know about a task to start an attempt at it and to score
/// one: whether it is published, its answer key with the card weights, the cards it offers in step 1,
/// the treatment leaves, and the content revision all of it was loaded from.
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007: declared here, implemented by the Content module —
/// the only module that may read the <c>content</c> schema — and taken as a constructor parameter by
/// Attempts. It answers facts, never a policy: whether an unpublished task may still be submitted is
/// the Attempts module's rule, so this hands over <see cref="TaskForAttempt.Published"/> rather than
/// deciding. The answer key crosses the boundary here and nowhere else (docs/SPEC.md §9.1).
/// </remarks>
public interface ITaskForAttemptLookup
{
    /// <summary>The task with this slug, published or not, or <see langword="null"/> when there is none.</summary>
    Task<TaskForAttempt?> FindAsync(string taskSlug, CancellationToken cancellationToken);
}
