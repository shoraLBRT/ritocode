namespace Ritocode.Shared.Contracts.Users;

/// <summary>
/// Answers who some users are — username and e-mail — for the admin area naming the learner behind a
/// signal or an attempt (docs/SPEC.md §6.2).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Users module and taken by
/// Attempts. Its own interface rather than a widening of <see cref="IUserLookup"/>, whose callers have
/// no business with an e-mail address. It takes many users at once, so a page of the admin lists is
/// one query rather than one per row (ADR 0007 §6).
/// </remarks>
public interface IUserContactLookup
{
    /// <summary>Each of <paramref name="userIds"/> that exists; a missing user is absent from the answer.</summary>
    Task<IReadOnlyDictionary<Guid, UserContact>> FindManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
