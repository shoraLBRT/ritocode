namespace Ritocode.Shared.Identity;

/// <summary>
/// The caller's identity, as established by the authentication middleware. Every endpoint that
/// writes or reads a user-owned row takes its user from here.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam <see href="https://github.com/shoraLBRT/ritocode/issues/6">#6</see> exists to
/// build, and the reason ADR 0005 may ship a seeded development identity instead of a login: the
/// implementation behind this interface changes when a real session provider arrives, and no
/// endpoint does. Taking <c>user_id</c> from a request body or query string instead is the second
/// row of that ADR's forbidden list.
/// </para>
/// <para>
/// It is deliberately one value wide. Anything else about the user — a username to render, an
/// email — is a question about a row in another module's schema, which is
/// <c>IUserLookup</c>'s job under ADR 0007, not this one's. Widening it would put a database read
/// on the authentication path of every request.
/// </para>
/// <para>
/// Resolved per request. Outside a request — a hosted service, the evaluation worker in stage 4 —
/// there is no caller and <see cref="Id"/> is <see langword="null"/>; code that runs in both places
/// must be given its user explicitly rather than reading it from here.
/// </para>
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    /// The authenticated user's identifier, or <see langword="null"/> when the request is
    /// anonymous. Use <see cref="CurrentUserExtensions.RequireId"/> where anonymous is not a case
    /// the caller can serve.
    /// </summary>
    Guid? Id { get; }
}
