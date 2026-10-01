using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.SignIn;
using Ritocode.Shared.Contracts.Auth;

namespace Ritocode.Modules.Auth.Contracts;

/// <summary>The Auth module's answer to <see cref="ILinkedProviderLookup"/>, over its own schema.</summary>
internal sealed class LinkedProviderLookup(AuthDbContext context) : ILinkedProviderLookup
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> FindProvidersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var ids = userIds.ToList();

        var links = await context.LinkedAccounts
            .AsNoTracking()
            .Where(account => ids.Contains(account.UserId))
            .Select(account => new { account.UserId, account.Provider })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return links
            .GroupBy(link => link.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(link => link.Provider).Order().Select(Name)]);
    }

    /// <summary>The provider as <c>/auth/login/{provider}</c> names it.</summary>
    private static string Name(IdentityProvider provider) => provider switch
    {
        IdentityProvider.GitHub => OAuthProviders.GitHub,
        IdentityProvider.Google => OAuthProviders.Google,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "A provider with no sign-in address."),
    };
}
