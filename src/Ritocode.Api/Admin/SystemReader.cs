using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Api.Feedback;
using Ritocode.Shared.Contracts.Content;
using Ritocode.Shared.Persistence;

namespace Ritocode.Api.Admin;

/// <summary>
/// Makes the <see cref="SystemReport"/>. The host composes it (ADR 0002): migrations through each module's own
/// context, the content revision through the Content module's contract, the rest from the host itself. A part
/// that fails is reported as failing and does not fail the report.
/// </summary>
internal sealed partial class SystemReader(
    IServiceProvider services,
    IEnumerable<ModuleDbContextRegistration> registrations,
    IContentRevisionLookup contentRevision,
    FeedbackCredentialCheck feedbackCredential,
    IHttpClientFactory httpClients,
    IOptions<SystemOptions> options,
    TimeProvider clock,
    ILogger<SystemReader> logger)
{
    public const string UmamiClient = "system-umami";

    private static readonly BuildInfo Build = BuildInfo.Parse(
        typeof(SystemReader).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public async Task<SystemReport> ReadAsync(CancellationToken cancellationToken)
    {
        // The two calls out of the process run while the database is read.
        var umami = CheckUmamiAsync(cancellationToken);
        var feedback = feedbackCredential.CheckAsync(cancellationToken);

        var migrations = new List<ModuleMigrations>();
        foreach (var registration in registrations.OrderBy(registration => registration.Schema, StringComparer.Ordinal))
        {
            migrations.Add(await ReadMigrationsAsync(registration, cancellationToken).ConfigureAwait(false));
        }

        var database = await ReadDatabaseAsync(cancellationToken).ConfigureAwait(false);
        var content = await ReadContentAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DependencyInfo> dependencies =
        [
            new("postgresql", database.Status),
            new("umami", await umami.ConfigureAwait(false)),
            new("feedbackGitHubApp", await feedback.ConfigureAwait(false)),
        ];

        var backup = await ReadBackupAsync(cancellationToken).ConfigureAwait(false);
        return new SystemReport(Build, content, database, migrations, dependencies, backup, clock.GetUtcNow());
    }

    private async Task<ModuleMigrations> ReadMigrationsAsync(ModuleDbContextRegistration registration, CancellationToken cancellationToken)
    {
        var context = (DbContext)services.GetRequiredService(registration.ContextType);
        try
        {
            var known = context.Database.GetMigrations().ToList();
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var pending = known.Except(applied, StringComparer.Ordinal).ToList();

            return new ModuleMigrations(
                registration.Schema,
                pending.Count == 0 ? MigrationStatus.UpToDate : MigrationStatus.Pending,
                applied.Count,
                applied.Count == 0 ? null : applied[^1],
                pending);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, $"the {registration.Schema} migrations", exception);
            return new ModuleMigrations(registration.Schema, MigrationStatus.Failing, null, null, []);
        }
    }

    private async Task<DatabaseInfo> ReadDatabaseAsync(CancellationToken cancellationToken)
    {
        var registration = registrations.FirstOrDefault();
        if (registration is null)
        {
            return new DatabaseInfo(DependencyStatus.NotConfigured, null);
        }

        var context = (DbContext)services.GetRequiredService(registration.ContextType);
        try
        {
            var size = await context.Database
                .SqlQuery<long>($"SELECT pg_database_size(current_database()) AS \"Value\"")
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);

            return new DatabaseInfo(DependencyStatus.Ok, size);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, "the database's size", exception);
            return new DatabaseInfo(DependencyStatus.Failing, null);
        }
    }

    private async Task<ContentIngestInfo?> ReadContentAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await contentRevision.LastAsync(cancellationToken).ConfigureAwait(false) is { } last
                ? new ContentIngestInfo(last.Revision, last.IngestedAt)
                : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, "the content revision", exception);
            return null;
        }
    }

    private async Task<DependencyStatus> CheckUmamiAsync(CancellationToken cancellationToken)
    {
        if (options.Value.UmamiUrl is not { } umami)
        {
            return DependencyStatus.NotConfigured;
        }

        try
        {
            var http = httpClients.CreateClient(UmamiClient);
            using var response = await http.GetAsync(new Uri(umami, "/api/heartbeat"), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? DependencyStatus.Ok : DependencyStatus.Failing;
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, "Umami", exception);
            return DependencyStatus.Failing;
        }
    }

    private async Task<BackupInfo?> ReadBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await BackupHeartbeat.ReadAsync(options.Value.BackupHeartbeatPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogFailed(logger, "the backup's heartbeat", exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The system page could not read {Part}.")]
    private static partial void LogFailed(ILogger logger, string part, Exception exception);
}
