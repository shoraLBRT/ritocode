using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ritocode.Shared.Persistence;

namespace Ritocode.DbMigrator;

/// <summary>
/// Applies each module's migrations in registration order.
/// </summary>
/// <remarks>
/// <para>
/// The API host never migrates itself: several instances starting at once would race, and a failed
/// migration should take down one job rather than every serving instance. See
/// docs/adr/0004-persistence-and-migrations.md.
/// </para>
/// <para>
/// Public rather than internal because the integration test harness builds each test database with
/// it. A test database assembled by a second, parallel code path would stop being evidence that
/// the migrations CI applies are the ones the tests ran against.
/// </para>
/// </remarks>
public sealed partial class MigrationRunner(IServiceProvider services, ILogger<MigrationRunner> logger)
{
    /// <summary>Applies every pending migration. Idempotent, so it is safe to run on every deploy.</summary>
    public async Task<int> ApplyAsync(CancellationToken cancellationToken = default)
    {
        foreach (var registration in Registrations)
        {
            using var scope = services.CreateScope();
            var context = Resolve(scope.ServiceProvider, registration);

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            if (pending.Length == 0)
            {
                LogUpToDate(logger, registration.Schema);
                continue;
            }

            LogApplying(logger, pending.Length, registration.Schema);
            await context.Database.MigrateAsync(cancellationToken);
            LogApplied(logger, registration.Schema);
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Reports pending migrations without applying them. Returns <see cref="ExitCodes.Pending"/>
    /// when any exist, so a deployment check can branch on the exit code alone.
    /// </summary>
    public async Task<int> ReportStatusAsync(CancellationToken cancellationToken = default)
    {
        var pendingTotal = 0;

        foreach (var registration in Registrations)
        {
            using var scope = services.CreateScope();
            var context = Resolve(scope.ServiceProvider, registration);

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            pendingTotal += pending.Length;

            if (pending.Length == 0)
            {
                LogUpToDate(logger, registration.Schema);
                continue;
            }

            foreach (var migration in pending)
            {
                LogPending(logger, registration.Schema, migration);
            }
        }

        return pendingTotal == 0 ? ExitCodes.Success : ExitCodes.Pending;
    }

    private IEnumerable<ModuleDbContextRegistration> Registrations =>
        services.GetServices<ModuleDbContextRegistration>();

    private static ModuleDbContext Resolve(IServiceProvider provider, ModuleDbContextRegistration registration) =>
        (ModuleDbContext)provider.GetRequiredService(registration.ContextType);

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Schema {Schema} is up to date")]
    private static partial void LogUpToDate(ILogger logger, string schema);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Applying {Count} migration(s) to schema {Schema}")]
    private static partial void LogApplying(ILogger logger, int count, string schema);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Schema {Schema} migrated")]
    private static partial void LogApplied(ILogger logger, string schema);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Schema {Schema} has pending migration {Migration}")]
    private static partial void LogPending(ILogger logger, string schema, string migration);
}
