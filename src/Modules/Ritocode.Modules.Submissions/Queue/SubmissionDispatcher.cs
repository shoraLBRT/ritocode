using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence;

namespace Ritocode.Modules.Submissions.Queue;

/// <summary>The submission queue over the module's own table. See <see cref="ISubmissionDispatcher"/>.</summary>
/// <remarks>
/// <para>
/// The only code in the module that reads submissions by status rather than by owner: the queue serves
/// no user, so no owner applies. <c>OwnershipRuleTests</c> carries an allowance for this class that says
/// so, rather than the queue finding a way around the rule.
/// </para>
/// <para>
/// Each operation is the unit the EF execution strategy retries, never one statement of it, as a
/// workspace save is: a retried commit on its own would record a change made under a lock that no
/// longer exists.
/// </para>
/// </remarks>
public sealed class SubmissionDispatcher(
    SubmissionsDbContext context,
    IOptions<SubmissionQueueOptions> options,
    TimeProvider timeProvider) : ISubmissionDispatcher
{
    private readonly TimeSpan _claimTimeout = options.Value.ClaimTimeout;

    public Task<SubmissionClaim?> ClaimNextAsync(CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(ClaimAsync, cancellationToken);
    }

    public Task<bool> CompleteAsync(SubmissionClaim claim, int score, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentOutOfRangeException.ThrowIfLessThan(score, Submission.MinScore);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(score, Submission.MaxScore);

        return RecordAsync(claim, submission => submission.Complete(score, timeProvider.GetUtcNow()), cancellationToken);
    }

    public Task<bool> FailAsync(SubmissionClaim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return RecordAsync(claim, submission => submission.Fail(timeProvider.GetUtcNow()), cancellationToken);
    }

    private async Task<SubmissionClaim?> ClaimAsync(CancellationToken cancellationToken)
    {
        // A retried attempt starts from nothing a failed one tracked.
        context.ChangeTracker.Clear();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var abandonedBefore = now - _claimTimeout;

        // Not composed with any LINQ operator, so EF sends the locking clause as written rather than
        // wrapping it in a subquery. SKIP LOCKED is what lets several workers drain one table: each passes
        // over a row another is claiming instead of waiting on it, so an attempt is never handed out twice
        // and no worker queues behind another. Status is a literal rather than a parameter so the planner
        // can match the partial index (status, created_at) WHERE status IN ('Queued','Running').
        var rows = await context.Submissions
            .FromSql($"""
                SELECT * FROM submissions.submissions
                WHERE status = 'Queued' OR (status = 'Running' AND started_at < {abandonedBefore})
                ORDER BY created_at, id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (rows.SingleOrDefault() is not { } submission)
        {
            // Nothing to take. Disposing the transaction without committing releases nothing it held.
            return null;
        }

        if (submission.Status == SubmissionStatus.Queued)
        {
            submission.Start(now);
        }
        else
        {
            submission.Reclaim(now);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SubmissionClaim(
            submission.Id,
            submission.WorkspaceId,
            submission.UserId,
            submission.InputReference,
            submission.StartedAt!.Value);
    }

    private Task<bool> RecordAsync(SubmissionClaim claim, Action<Submission> transition, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();

                await using var transaction = await context.Database.BeginTransactionAsync(token);

                // The claim guard. The row is found only while it is still running under this claim's time,
                // and locked, so a reclaim cannot move started_at between this read and the write. An
                // attempt taken over since — or already recorded — no longer matches, and this result is
                // discarded rather than overwriting the one the current claim will record.
                var rows = await context.Submissions
                    .FromSql($"""
                        SELECT * FROM submissions.submissions
                        WHERE id = {claim.SubmissionId} AND status = 'Running' AND started_at = {claim.ClaimedAt}
                        FOR UPDATE
                        """)
                    .ToListAsync(token);

                if (rows.SingleOrDefault() is not { } submission)
                {
                    return false;
                }

                transition(submission);

                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);

                return true;
            },
            cancellationToken);
    }
}
