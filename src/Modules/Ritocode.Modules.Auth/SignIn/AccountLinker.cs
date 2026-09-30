using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Modules.Auth.SignIn;

/// <summary>Why a provider's identity reached no account.</summary>
internal enum LinkRefusal
{
    /// <summary>A new identity whose address the provider does not mark as verified: it is never used to link or to create.</summary>
    EmailUnverified,

    /// <summary>The verified address belongs to a user who already has another account at this provider.</summary>
    ProviderAlreadyLinked,
}

/// <summary>The user an identity signs in as, or why there is none.</summary>
internal sealed record LinkResult(Guid? UserId, LinkRefusal? Refusal);

/// <summary>
/// One user per verified e-mail address (docs/SPEC.md §6.1): an identity already linked signs in as
/// its user; a new one with a verified address joins the user who has that address, or a new user
/// made for it; a new one without a verified address reaches nobody.
/// </summary>
internal sealed class AccountLinker(AuthDbContext context, IUserAccounts users, TimeProvider clock)
{
    public async Task<LinkResult> LinkAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var linked = await context.LinkedAccounts
            .SingleOrDefaultAsync(
                account => account.Provider == identity.Provider && account.ProviderUserId == identity.ProviderUserId,
                cancellationToken)
            .ConfigureAwait(false);

        if (linked is not null)
        {
            // The login is for display and may have been renamed; the identifier is what matched.
            if (linked.ProviderLogin != Login(identity))
            {
                linked.ProviderLogin = Login(identity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return new LinkResult(linked.UserId, null);
        }

        if (!identity.EmailVerified || identity.Email is null)
        {
            return new LinkResult(null, LinkRefusal.EmailUnverified);
        }

        var userId = await users.FindByEmailAsync(identity.Email, cancellationToken).ConfigureAwait(false)
            ?? await users.CreateAsync(identity.Email, UsernameHint(identity), cancellationToken).ConfigureAwait(false);

        // At most one identity per provider per user (linked_accounts' unique index): a second GitHub
        // account claiming the same address is refused rather than silently replacing the first.
        var hasOther = await context.LinkedAccounts
            .AnyAsync(account => account.UserId == userId && account.Provider == identity.Provider, cancellationToken)
            .ConfigureAwait(false);

        if (hasOther)
        {
            return new LinkResult(null, LinkRefusal.ProviderAlreadyLinked);
        }

        context.LinkedAccounts.Add(LinkedAccount.Create(userId, identity.Provider, identity.ProviderUserId, Login(identity), clock.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new LinkResult(userId, null);
    }

    private static string Login(ExternalIdentity identity) =>
        identity.Login.Length > LinkedAccount.ProviderLoginMaxLength
            ? identity.Login[..LinkedAccount.ProviderLoginMaxLength]
            : identity.Login;

    // A GitHub login is a fine username; a Google "login" is the address, whose local part is used.
    private static string UsernameHint(ExternalIdentity identity) =>
        identity.Provider == IdentityProvider.GitHub ? identity.Login : identity.Email!.Split('@')[0];
}
