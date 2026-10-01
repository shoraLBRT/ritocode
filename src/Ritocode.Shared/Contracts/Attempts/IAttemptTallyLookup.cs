namespace Ritocode.Shared.Contracts.Attempts;

/// <summary>
/// Answers how many attempts some users have made and how many tasks they have solved, for the admin
/// area's list of users (docs/SPEC.md §6.2).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Attempts module and taken by
/// Users. It takes many users at once, so a page of the list is one query rather than one per row
/// (ADR 0007 §6).
/// </remarks>
public interface IAttemptTallyLookup
{
    /// <summary>The tally of each of <paramref name="userIds"/> that has started an attempt; a user who has not is absent.</summary>
    Task<IReadOnlyDictionary<Guid, AttemptTally>> TallyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
