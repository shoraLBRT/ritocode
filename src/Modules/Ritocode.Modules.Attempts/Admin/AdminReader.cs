using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Shared.Contracts.Content;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Attempts.Admin;

/// <summary>
/// The admin area's signals and attempts (docs/SPEC.md §6.2): every user's, which is why its endpoints
/// are mapped behind the admin policy and nowhere else.
/// </summary>
public interface IAdminReader
{
    Task<Page<AdminSignalView>> ListSignalsAsync(SignalStatus status, PageRequest page, CancellationToken cancellationToken);

    Task<Result<AdminSignalView>> ResolveSignalAsync(Guid signalId, CancellationToken cancellationToken);

    Task<Page<AdminAttemptView>> ListAttemptsAsync(AttemptStatus status, Guid? userId, PageRequest page, CancellationToken cancellationToken);
}

internal sealed class AdminReader(
    AttemptsDbContext context,
    IUserContactLookup users,
    ICardClassLookup cards,
    TimeProvider clock) : IAdminReader
{
    public const string SignalNotFoundCode = "signal_not_found";

    public static AppError SignalNotFound() => AppError.NotFound(SignalNotFoundCode, "No signal exists with this id.");

    public async Task<Page<AdminSignalView>> ListSignalsAsync(SignalStatus status, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var query = status == SignalStatus.Open
            ? context.EveryonesSignals().Where(signal => signal.ResolvedAt == null)
            : context.EveryonesSignals().Where(signal => signal.ResolvedAt != null);

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);

        // Newest first; the id breaks a tie, so a page never repeats or skips a row.
        var rows = await query
            .OrderByDescending(signal => signal.CreatedAt)
            .ThenByDescending(signal => signal.Id)
            .Skip((int)page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Page<AdminSignalView>.From(await ViewsAsync(rows, cancellationToken).ConfigureAwait(false), page, total);
    }

    public async Task<Result<AdminSignalView>> ResolveSignalAsync(Guid signalId, CancellationToken cancellationToken)
    {
        var signal = await context.FindAnySignalAsync(signalId, cancellationToken).ConfigureAwait(false);

        if (signal is null)
        {
            return SignalNotFound();
        }

        // To the microsecond PostgreSQL keeps, as every timestamp in this module.
        var now = clock.GetUtcNow();
        signal.Resolve(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await ViewsAsync([signal], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<Page<AdminAttemptView>> ListAttemptsAsync(
        AttemptStatus status,
        Guid? userId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var query = status switch
        {
            AttemptStatus.Open => context.EveryonesAttempts().Where(attempt => attempt.SubmittedAt == null),
            AttemptStatus.Submitted => context.EveryonesAttempts().Where(attempt => attempt.SubmittedAt != null),
            _ => context.EveryonesAttempts(),
        };

        if (userId is { } user)
        {
            query = query.Where(attempt => attempt.UserId == user);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(attempt => attempt.StartedAt)
            .ThenByDescending(attempt => attempt.Id)
            .Skip((int)page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var learners = await LearnersAsync(rows.Select(attempt => attempt.UserId), cancellationToken).ConfigureAwait(false);

        var items = rows
            .Select(attempt => new AdminAttemptView(
                attempt.Id,
                attempt.TaskSlug,
                learners[attempt.UserId],
                attempt.StartedAt,
                attempt.Step,
                attempt.SubmittedAt,
                attempt.IsSubmitted && !attempt.CountsTowardProgress,
                attempt.SubmittedAt is { } submitted ? (long)(submitted - attempt.StartedAt).TotalSeconds : null,
                attempt.Score,
                attempt.MaxScore))
            .ToList();

        return Page<AdminAttemptView>.From(items, page, total);
    }

    private async Task<IReadOnlyList<AdminSignalView>> ViewsAsync(IReadOnlyList<Signal> signals, CancellationToken cancellationToken)
    {
        var learners = await LearnersAsync(signals.Select(signal => signal.UserId), cancellationToken).ConfigureAwait(false);

        var slugs = signals.Select(signal => signal.Card).Distinct(StringComparer.Ordinal).ToList();
        var names = slugs.Count == 0
            ? new Dictionary<string, string>()
            : (await cards.FindAsync(slugs, cancellationToken).ConfigureAwait(false)).CardNames;

        return
        [
            .. signals.Select(signal => new AdminSignalView(
                signal.Id,
                signal.AttemptId,
                signal.TaskSlug,
                signal.Card,
                names.GetValueOrDefault(signal.Card) ?? signal.Card,
                learners[signal.UserId],
                signal.Comment,
                signal.CreatedAt,
                signal.ResolvedAt)),
        ];
    }

    /// <summary>Every user among <paramref name="userIds"/>, in one call to the Users module; a missing one has no name.</summary>
    private async Task<Dictionary<Guid, AdminLearnerView>> LearnersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        var contacts = await users.FindManyAsync(ids, cancellationToken).ConfigureAwait(false);

        return ids.ToDictionary(
            id => id,
            id => contacts.TryGetValue(id, out var contact)
                ? new AdminLearnerView(id, contact.Username, contact.Email)
                : new AdminLearnerView(id, null, null));
    }
}
