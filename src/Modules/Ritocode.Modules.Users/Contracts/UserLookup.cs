using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Modules.Users.Contracts;

/// <summary>
/// The Users module's answer to <see cref="IUserLookup"/>, over its own schema.
/// </summary>
internal sealed class UserLookup(UsersDbContext context) : IUserLookup
{
    public Task<UserSummary?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserSummary(user.Id, user.Username))
            .FirstOrDefaultAsync(cancellationToken);
}
