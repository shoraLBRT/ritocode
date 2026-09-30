namespace Ritocode.Shared.Contracts.Content;

/// <summary>
/// Answers which class each of some cards belongs to, and the order of the classes — what the
/// Attempts module needs to group a learner's progress by class (docs/SPEC.md §4.7).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Content module. It takes many
/// cards at once, so progress is one query rather than one per card (ADR 0007 §6). Retired cards are
/// answered too: a learner met them in attempts that still count.
/// </remarks>
public interface ICardClassLookup
{
    Task<CardClasses> FindAsync(IReadOnlyCollection<string> cardSlugs, CancellationToken cancellationToken);
}
