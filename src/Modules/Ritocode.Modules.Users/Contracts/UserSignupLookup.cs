using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Modules.Users.Contracts;

/// <summary>The Users module's answer to <see cref="IUserSignupLookup"/>, over its own schema.</summary>
internal sealed class UserSignupLookup(UsersDbContext context) : IUserSignupLookup
{
    public async Task<IReadOnlyList<DateTimeOffset>> SignupsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await context.Users
            .AsNoTracking()
            .Where(user => user.CreatedAt >= from && user.CreatedAt < to)
            .Select(user => user.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
