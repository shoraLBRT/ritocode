namespace Ritocode.Shared.Contracts.Content;

/// <summary>
/// Answers which of some cards the catalogue publishes now — what the Attempts module needs to accept
/// a study mark on a card and to leave out the marks on a card since retired (ADR 0018).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Content module. It takes many
/// cards at once, so the caller's marks are checked in one query (ADR 0007 §6). A retired card and a
/// slug that names no card are both absent from the answer: neither can be studied.
/// </remarks>
public interface IPublishedCardLookup
{
    /// <summary>The slugs among <paramref name="cardSlugs"/> of the cards that are published.</summary>
    Task<IReadOnlySet<string>> PublishedAsync(IReadOnlyCollection<string> cardSlugs, CancellationToken cancellationToken);
}
