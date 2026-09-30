using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Modules.Auth.Session;

/// <summary>
/// Starts, finds and ends sessions (ADR 0012). Sign-in with a provider (#7) starts one and writes its
/// cookies with <see cref="SessionCookies.Write"/>; every request finds its own; signing out ends it.
/// </summary>
public interface ISessionIssuer
{
    /// <summary>A new session for an existing user. The token is returned once and never stored.</summary>
    Task<IssuedSession> StartAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The session <paramref name="token"/> names, if it exists, is not revoked and has not expired.</summary>
    Task<UserSession?> FindActiveAsync(string token, CancellationToken cancellationToken);

    /// <summary>Revokes the session <paramref name="token"/> names; a token that names none changes nothing.</summary>
    Task EndAsync(string token, CancellationToken cancellationToken);
}

/// <remarks>Auth depends on Users here for one fact: that the user a session is for exists (ADR 0007).</remarks>
internal sealed class SessionIssuer(
    AuthDbContext context,
    IUserLookup users,
    IOptions<SessionOptions> options,
    TimeProvider clock) : ISessionIssuer
{
    public async Task<IssuedSession> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        // auth.sessions.user_id has no foreign key (ADR 0004): this is what keeps a session from naming nobody.
        if (await users.FindAsync(userId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException($"There is no user {userId} to start a session for.");
        }

        var (session, token) = UserSession.Start(userId, Now(), options.Value.Lifetime);

        context.Sessions.Add(session);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IssuedSession(token, session.CsrfToken, session.ExpiresAt);
    }

    public async Task<UserSession?> FindActiveAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var hash = UserSession.Hash(token);
        var session = await context.Sessions.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        return session is not null && session.IsActive(Now()) ? session : null;
    }

    public async Task EndAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var hash = UserSession.Hash(token);
        var session = await context.Sessions
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return;
        }

        session.Revoke(Now());
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The clock, to the microsecond PostgreSQL keeps.</summary>
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
