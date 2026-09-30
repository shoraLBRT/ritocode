using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Contracts.Users;

namespace Ritocode.Modules.Users.Contracts;

/// <summary>The Users module's answer to <see cref="IUserAccounts"/>, over its own schema.</summary>
internal sealed class UserAccounts(UsersDbContext context, TimeProvider clock) : IUserAccounts
{
    /// <summary>How many numbered usernames are tried before a random suffix is taken.</summary>
    private const int NumberedAttempts = 20;

    public Task<Guid?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalised = email.Trim().ToLowerInvariant();
        return context.Users
            .AsNoTracking()
            .Where(user => user.Email == normalised)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(string email, string usernameHint, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        // Twice: a race lost on the e-mail answers the winner; one lost on the username is tried
        // again with a fresh one.
        for (var attempt = 0; ; attempt++)
        {
            var username = await FreeUsernameAsync(Slug(usernameHint, email), cancellationToken).ConfigureAwait(false);
            var user = User.Create(email, username, clock.GetUtcNow());
            context.Users.Add(user);

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return user.Id;
            }
            catch (DbUpdateException exception) when (attempt == 0 && IsUniqueViolation(exception))
            {
                context.Entry(user).State = EntityState.Detached;

                if (await FindByEmailAsync(email, cancellationToken).ConfigureAwait(false) is { } existing)
                {
                    return existing;
                }
            }
        }
    }

    /// <summary><paramref name="slug"/> if nobody has it, else the first free <c>slug-2</c>, <c>slug-3</c>…, else a random suffix.</summary>
    private async Task<string> FreeUsernameAsync(string slug, CancellationToken cancellationToken)
    {
        var candidates = new List<string> { slug };
        for (var number = 2; number <= NumberedAttempts; number++)
        {
            candidates.Add(WithSuffix(slug, number.ToString(CultureInfo.InvariantCulture)));
        }

        var taken = await context.Users
            .AsNoTracking()
            .Where(user => candidates.Contains(user.Username))
            .Select(user => user.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates.FirstOrDefault(candidate => !taken.Contains(candidate))
            ?? WithSuffix(slug, Guid.NewGuid().ToString("N")[..8]);
    }

    /// <summary>
    /// The hint — a provider login, or the e-mail's local part — reduced to lower-case letters, digits
    /// and single hyphens, as a GitHub login is; <c>user</c> when nothing is left.
    /// </summary>
    internal static string Slug(string? usernameHint, string email)
    {
        var source = string.IsNullOrWhiteSpace(usernameHint) ? email.Split('@')[0] : usernameHint;
        var builder = new StringBuilder();

        foreach (var character in source.Trim().ToLowerInvariant())
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > User.UsernameMaxLength)
        {
            slug = slug[..User.UsernameMaxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? "user" : slug;
    }

    private static string WithSuffix(string slug, string suffix)
    {
        var room = User.UsernameMaxLength - suffix.Length - 1;
        return $"{(slug.Length > room ? slug[..room].TrimEnd('-') : slug)}-{suffix}";
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
