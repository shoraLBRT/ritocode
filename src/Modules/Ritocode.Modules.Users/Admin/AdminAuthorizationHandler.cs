using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Users.Admin;

/// <summary>
/// Meets <see cref="AdminRequirement"/> when the caller's address is one <see cref="AdminOptions"/>
/// names. Every user's address is one a provider verified (SPEC §6.1), so naming admins by address
/// cannot be claimed by signing in with an unverified one.
/// </summary>
/// <remarks>
/// Read from the users table on every admin request, so removing an address takes effect on the next
/// request after a restart, whatever sessions are open.
/// </remarks>
internal sealed class AdminAuthorizationHandler(UsersDbContext context, IOptions<AdminOptions> options)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext authorization, AdminRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var admins = options.Value;

        if (admins.Emails.Count == 0
            || !Guid.TryParse(authorization.User.FindFirst(RitocodeClaimTypes.UserId)?.Value, out var userId))
        {
            return;
        }

        var email = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (email is not null && admins.Names(email))
        {
            authorization.Succeed(requirement);
        }
    }
}
