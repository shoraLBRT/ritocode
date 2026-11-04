namespace Ritocode.Shared.Contracts.Auth;

/// <summary>
/// Answers which providers some users have signed in with, for the admin area's list of users
/// (docs/SPEC.md §6.2).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007, implemented by the Auth module, which owns the
/// linked accounts, and taken by Users. It takes many users at once (ADR 0007 §6).
/// </remarks>
public interface ILinkedProviderLookup
{
    /// <summary>
    /// The providers each of <paramref name="userIds"/> is linked to, as the sign-in addresses name them
    /// (<c>github</c>, <c>google</c>), in that order; a user linked to none — the development identity —
    /// is absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> FindProvidersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
