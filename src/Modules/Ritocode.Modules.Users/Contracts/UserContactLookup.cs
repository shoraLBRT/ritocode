using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Modules.Users.Contracts;

/// <summary>The Users module's answer to <see cref="IUserContactLookup"/>, over its own schema.</summary>
internal sealed class UserContactLookup(UsersDbContext context) : IUserContactLookup
{
    public async Task<IReadOnlyDictionary<Guid, UserContact>> FindManyAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserContact>();
        }

        var ids = userIds.ToList();

        return await context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new UserContact(user.Id, user.Username, user.Email))
            .ToDictionaryAsync(contact => contact.Id, cancellationToken)
            .ConfigureAwait(false);
    }
}
