using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Users.Identity;

/// <summary>
/// Keeps a <c>users.users</c> row matching the seeded development identity, so the identity the
/// authentication scheme asserts names a user that actually exists.
/// </summary>
/// <remarks>
/// <para>
/// The Auth module asserts the identity and this module owns the row, because no module reads or
/// writes another module's schema. The two agree because they read the same configured identifier,
/// not because either calls the other.
/// </para>
/// <para>
/// The row is not optional decoration. <c>workspaces.user_id</c> and <c>submissions.user_id</c> are
/// required and carry no foreign key, so the module creating such a row validates the reference
/// itself (ADR 0004, ADR 0007). A development identity with no row would pass authentication and
/// then fail at the first workspace, several layers from the cause.
/// </para>
/// <para>
/// An <see cref="IHostedService"/> rather than a <see cref="BackgroundService"/>, unlike the problem
/// content seeder: this completes before the host serves its first request, so an endpoint never
/// races the row it depends on. Failure is logged rather than thrown — a host that will not start
/// because a database is down is a worse answer than one that starts unready, and
/// <c>/health/ready</c> is already the place that reports it.
/// </para>
/// </remarks>
internal sealed partial class DevelopmentIdentitySeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<DevelopmentIdentityOptions> options,
    IHostEnvironment environment,
    ILogger<DevelopmentIdentitySeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var identity = options.Value;

        if (!identity.Enabled)
        {
            return;
        }

        if (!environment.IsDevelopment())
        {
            LogEnabledOutsideDevelopment(logger, environment.EnvironmentName);
        }

        try
        {
            await EnsureUserAsync(identity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSeedFailed(logger, identity.UserId, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureUserAsync(DevelopmentIdentityOptions identity, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var existing = await context.Users
            .FirstOrDefaultAsync(user => user.Id == identity.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            LogAlreadySeeded(logger, existing.Id, existing.Username);
            return;
        }

        // User.Create allocates its own identifier, and this row's identifier is the configured one
        // — it is what the claim carries. The rest of the invariants are still the domain's.
        var user = User.Create(identity.Email, identity.Username, DateTimeOffset.UtcNow);
        user.Id = identity.UserId;

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogSeeded(logger, user.Id, user.Username);
    }

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Seeded the development identity as user {UserId} ({Username})")]
    private static partial void LogSeeded(ILogger logger, Guid userId, string username);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "The development identity already exists as user {UserId} ({Username})")]
    private static partial void LogAlreadySeeded(ILogger logger, Guid userId, string username);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "The development identity is enabled in the {Environment} environment. "
                  + "Every request is authenticated as one fixed user and no credential is checked")]
    private static partial void LogEnabledOutsideDevelopment(ILogger logger, string environment);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Error,
        Message = "Seeding the development identity as user {UserId} failed; "
                  + "requests will authenticate as a user that has no row")]
    private static partial void LogSeedFailed(ILogger logger, Guid userId, Exception exception);
}
