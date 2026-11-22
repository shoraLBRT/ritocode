using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Ritocode.Modules.Errors.Alerts;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Modules.Errors.Persistence;
using Ritocode.Modules.Errors.Persistence.Configurations;
using Ritocode.Shared.Diagnostics;

namespace Ritocode.Modules.Errors.Recording;

/// <summary>
/// Keeps an error in its group (ADR 0020 decision 1): the first occurrence of a fingerprint opens a group and
/// raises a <see cref="ErrorAlertKind.New"/> alert; a later one counts; one of a resolved group reopens it and
/// raises <see cref="ErrorAlertKind.Reopened"/>. Every write prunes what the retention rule no longer keeps.
/// </summary>
/// <remarks>
/// A singleton with a scope of its own per error, so the context it writes through is never one a failed
/// request left half-changed. The count and the reopening are single statements, so two occurrences at once
/// count twice and reopen once: one message per change.
/// </remarks>
internal sealed partial class ErrorRecorder(
    IServiceScopeFactory scopes,
    IErrorAlerts alerts,
    IOptions<ErrorsOptions> options,
    TimeProvider clock,
    ILogger<ErrorRecorder> logger) : IServerErrorRecorder
{
    /// <summary>A database that hangs is not waited on for long: the 500 it came with is still to be written.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task RecordAsync(ServerError error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            var (report, stack) = ErrorReports.FromServer(error);
            await RecordAsync(report, stack, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            LogNotRecorded(logger, exception.GetType().Name);
        }
    }

    /// <summary>Records <paramref name="report"/>, fingerprinted over <paramref name="fingerprintStack"/>; answers its group's id.</summary>
    public async Task<Guid> RecordAsync(ErrorReport report, string? fingerprintStack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var token = timeout.Token;

        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ErrorsDbContext>();
        var now = Now();
        var fingerprint = Fingerprint.Of(report.Source, report.Type, report.Message, fingerprintStack);

        var (groupId, alert) = await CountAsync(context, fingerprint, report, now, token).ConfigureAwait(false);

        context.Occurrences.Add(ErrorOccurrence.Of(groupId, report, now));
        await context.SaveChangesAsync(token).ConfigureAwait(false);

        await PruneGroupAsync(context, groupId, token).ConfigureAwait(false);
        await PruneExpiredAsync(context, now, token).ConfigureAwait(false);

        if (alert is { } kind)
        {
            var count = await context.Groups.Where(group => group.Id == groupId).Select(group => group.Count).FirstAsync(token).ConfigureAwait(false);
            alerts.Raise(new ErrorAlert(kind, report.Source, report.Type, report.Location, count));
        }

        return groupId;
    }

    /// <summary>Deletes every occurrence older than the retention, of every group. Answers how many went.</summary>
    public async Task<int> PruneExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ErrorsDbContext>();
        return await PruneExpiredAsync(context, Now(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(Guid GroupId, ErrorAlertKind? Alert)> CountAsync(
        ErrorsDbContext context, string fingerprint, ErrorReport report, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await IdOfAsync(context, fingerprint, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            context.Groups.Add(ErrorGroup.Open(fingerprint, report.Source, report.Type, report.Message, report.Location, now));
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return (context.Groups.Local.Single().Id, ErrorAlertKind.New);
            }
            catch (DbUpdateException exception) when (IsFirstOccurrenceRace(exception))
            {
                // The same error, at the same moment, opened the group first: this one counts in it.
                context.ChangeTracker.Clear();
                existing = await IdOfAsync(context, fingerprint, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The error group that won the race is gone.", exception);
            }
        }

        var id = existing.Value;

        // Only the occurrence that finds the group resolved reopens it, so two at once send one message.
        var reopened = await context.Groups
            .Where(group => group.Id == id && group.ResolvedAt != null)
            .ExecuteUpdateAsync(set => set.SetProperty(group => group.ResolvedAt, (DateTimeOffset?)null), cancellationToken)
            .ConfigureAwait(false) == 1;

        await context.Groups
            .Where(group => group.Id == id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(group => group.Count, group => group.Count + 1)
                    .SetProperty(group => group.LastSeenAt, now)
                    .SetProperty(group => group.Message, report.Message)
                    .SetProperty(group => group.Location, report.Location),
                cancellationToken)
            .ConfigureAwait(false);

        return (id, reopened ? ErrorAlertKind.Reopened : null);
    }

    private static Task<Guid?> IdOfAsync(ErrorsDbContext context, string fingerprint, CancellationToken cancellationToken) =>
        context.Groups.Where(group => group.Fingerprint == fingerprint).Select(group => (Guid?)group.Id).FirstOrDefaultAsync(cancellationToken);

    /// <summary>All but the group's latest <see cref="ErrorsOptions.OccurrencesPerGroup"/> occurrences go.</summary>
    private Task<int> PruneGroupAsync(ErrorsDbContext context, Guid groupId, CancellationToken cancellationToken)
    {
        var kept = context.Occurrences
            .Where(occurrence => occurrence.GroupId == groupId)
            .OrderByDescending(occurrence => occurrence.OccurredAt)
            .ThenByDescending(occurrence => occurrence.Id)
            .Take(options.Value.OccurrencesPerGroup)
            .Select(occurrence => occurrence.Id);

        return context.Occurrences
            .Where(occurrence => occurrence.GroupId == groupId && !kept.Contains(occurrence.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private Task<int> PruneExpiredAsync(ErrorsDbContext context, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - options.Value.OccurrenceRetention;
        return context.Occurrences.Where(occurrence => occurrence.OccurredAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The clock, to the microsecond PostgreSQL keeps.</summary>
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static bool IsFirstOccurrenceRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ErrorGroupConfiguration.OnePerFingerprintIndex,
        };

    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "The unhandled exception could not be recorded: {Failure}")]
    private static partial void LogNotRecorded(ILogger logger, string failure);
}
