namespace Ritocode.Shared.Contracts.Users;

/// <summary>
/// Answers whether a user exists, for a module about to store a reference to one.
/// </summary>
/// <remarks>
/// <para>
/// A cross-module contract in the sense of ADR 0007: declared here, implemented by the Users module
/// — the only module that may read <c>users.users</c> — and taken as a constructor parameter by the
/// module that asks. <c>workspaces.user_id</c> and <c>submissions.user_id</c> carry no foreign key
/// (ADR 0004), so the module creating such a row validates the reference through this rather than
/// through the database.
/// </para>
/// <para>
/// It answers a fact, never a policy. Absence is <see langword="null"/>, and the consumer chooses the
/// error code a client sees — this module does not get to.
/// </para>
/// <para>
/// Not the same thing as <c>ICurrentUser</c>, which reports who is calling and has no owning module.
/// A consumer that needs different fields about a user — a profile screen wanting an email — asks
/// its own question through its own interface rather than widening this one.
/// </para>
/// </remarks>
public interface IUserLookup
{
    /// <summary>The user with this identifier, or <see langword="null"/> when there is no such row.</summary>
    Task<UserSummary?> FindAsync(Guid id, CancellationToken cancellationToken);
}
