namespace Ritocode.Shared.Contracts.Attempts;

/// <summary>
/// Answers which tasks a user has submitted an attempt at, for the task catalogue's solved flags
/// (docs/SPEC.md §4.3).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Attempts module and taken by
/// Content. It answers the fact — a submitted attempt exists — and leaves what "solved" means on the
/// catalogue to the module that shows it. It takes many tasks at once, so a page of the catalogue is
/// one query rather than one per row (ADR 0007 §6).
/// </remarks>
public interface ISubmittedTaskLookup
{
    /// <summary>The slugs among <paramref name="taskSlugs"/> the user has submitted at least one attempt at.</summary>
    Task<IReadOnlySet<string>> FindSubmittedAsync(Guid userId, IReadOnlyCollection<string> taskSlugs, CancellationToken cancellationToken);
}
