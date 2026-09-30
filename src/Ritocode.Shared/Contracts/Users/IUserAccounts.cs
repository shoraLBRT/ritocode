namespace Ritocode.Shared.Contracts.Users;

/// <summary>
/// Finds or creates the account behind a verified e-mail address, for the Auth module signing someone
/// in with a provider (docs/SPEC.md §6.1).
/// </summary>
/// <remarks>
/// A cross-module contract in the sense of ADR 0007: declared here, implemented by the Users module,
/// which owns <c>users.users</c>. Its own interface rather than a widening of
/// <see cref="IUserLookup"/>, which answers a different question for different callers. Only an
/// address the provider marks as verified is ever passed in; the caller is the one that knows.
/// </remarks>
public interface IUserAccounts
{
    /// <summary>The user with this e-mail address, compared lower-cased, or <see langword="null"/>.</summary>
    Task<Guid?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// A new user with this e-mail address and a username derived from <paramref name="usernameHint"/>,
    /// made unique. If a user with the address already exists — another sign-in won the race — that
    /// user is answered instead.
    /// </summary>
    Task<Guid> CreateAsync(string email, string usernameHint, CancellationToken cancellationToken);
}
