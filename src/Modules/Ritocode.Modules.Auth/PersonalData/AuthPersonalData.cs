using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.SignIn;
using Ritocode.Shared.PersonalData;

namespace Ritocode.Modules.Auth.PersonalData;

/// <summary>The Auth module's part of an export: the providers linked to the account and its sessions.</summary>
public sealed record AuthExport(IReadOnlyList<LinkedAccountExport> LinkedAccounts, IReadOnlyList<SessionExport> Sessions);

/// <summary>A provider's identity linked to the account: its id and last known login there.</summary>
public sealed record LinkedAccountExport(string Provider, string ProviderUserId, string ProviderLogin, DateTimeOffset LinkedAt);

/// <summary>
/// A signed-in browser, by its dates only: the token's hash and the CSRF token authenticate, and an
/// export is a file that travels, so neither is in it.
/// </summary>
public sealed record SessionExport(DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);

/// <summary>The Auth module's part of <see cref="IPersonalData"/>: linked accounts and sessions.</summary>
internal sealed class AuthPersonalData(AuthDbContext context) : IPersonalData
{
    public string Module => "Auth";

    public bool HoldsTheAccount => false;

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var links = await context.LinkedAccounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.LinkedAt)
            .Select(account => new { account.Provider, account.ProviderUserId, account.ProviderLogin, account.LinkedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sessions = await context.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderBy(session => session.CreatedAt)
            .Select(session => new SessionExport(session.CreatedAt, session.ExpiresAt, session.RevokedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AuthExport(
            [.. links.Select(link => new LinkedAccountExport(Name(link.Provider), link.ProviderUserId, link.ProviderLogin, link.LinkedAt))],
            sessions);
    }

    public async Task<int> EraseAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Sessions first: the moment they are gone, no browser is signed in as this user.
        var sessions = await context.Sessions
            .Where(session => session.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var links = await context.LinkedAccounts
            .Where(account => account.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return sessions + links;
    }

    /// <summary>The provider as <c>/auth/login/{provider}</c> names it.</summary>
    private static string Name(IdentityProvider provider) => provider switch
    {
        IdentityProvider.GitHub => OAuthProviders.GitHub,
        IdentityProvider.Google => OAuthProviders.Google,
        _ => provider.ToString(),
    };
}
