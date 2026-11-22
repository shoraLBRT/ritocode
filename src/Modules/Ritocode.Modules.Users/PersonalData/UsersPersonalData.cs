using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.PersonalData;

namespace Ritocode.Modules.Users.PersonalData;

/// <summary>The Users module's part of an export: the account row, as sign-in made it.</summary>
public sealed record UsersExport(UserExport? Account);

/// <summary>The account row: the e-mail address a provider gave, the username made from it, and the join date.</summary>
public sealed record UserExport(Guid Id, string Email, string Username, DateTimeOffset CreatedAt);

/// <summary>The Users module's part of <see cref="IPersonalData"/>: the account row itself.</summary>
internal sealed class UsersPersonalData(UsersDbContext context) : IPersonalData
{
    public string Module => "Users";

    public bool HoldsTheAccount => true;

    public async Task<object> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserExport(user.Id, user.Email, user.Username, user.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new UsersExport(account);
    }

    public Task<int> EraseAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);
}
