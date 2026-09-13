using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Modules.Submissions.Queue;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Submissions.Tests.Queue;

/// <summary>
/// The queue against a real PostgreSQL, because the two things most worth testing about it are not
/// fakeable: that <c>SKIP LOCKED</c> never hands one attempt to two workers, and that a result is
/// recorded only on the claim that still holds the attempt.
/// </summary>
public sealed class SubmissionDispatcherTests(PostgresTestServer postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(15);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClaimNext_TakesTheOldestQueuedAttempt_AndStartsItUnderANewClaim()
    {
        var database = await CreateDatabaseAsync();
        var newest = await AddAsync(database, Queued(Noon));
        var oldest = await AddAsync(database, Queued(Noon.AddMinutes(-2)));
        await AddAsync(database, Queued(Noon.AddMinutes(-1)));

        var claim = await ClaimAsync(database, Noon.AddMinutes(1));

        Assert.NotNull(claim);
        Assert.Equal(oldest.Id, claim.SubmissionId);
        Assert.Equal(oldest.WorkspaceId, claim.WorkspaceId);
        Assert.Equal(oldest.UserId, claim.UserId);
        Assert.Equal(oldest.InputReference, claim.InputReference);
        Assert.Equal(Noon.AddMinutes(1), claim.ClaimedAt);

        var row = await ReadAsync(database, oldest.Id);
        Assert.Equal(SubmissionStatus.Running, row.Status);
        Assert.Equal(Noon.AddMinutes(1), row.StartedAt);
        Assert.Equal(SubmissionStatus.Queued, (await ReadAsync(database, newest.Id)).Status);
    }

    [Fact]
    public async Task ClaimNext_WithNothingToTake_IsNull()
    {
        var database = await CreateDatabaseAsync();

        Assert.Null(await ClaimAsync(database, Noon));
    }

    [Fact]
    public async Task TerminalAttempts_AreNeverClaimed_HoweverOld()
    {
        var database = await CreateDatabaseAsync();

        var completed = Queued(Noon.AddHours(-3));
        completed.Start(Noon.AddHours(-3));
        completed.Complete(50, Noon.AddHours(-3));
        await AddAsync(database, completed);

        var failedInARun = Queued(Noon.AddHours(-3));
        failedInARun.Start(Noon.AddHours(-3));
        failedInARun.Fail(Noon.AddHours(-3));
        await AddAsync(database, failedInARun);

        var failedInTheQueue = Queued(Noon.AddHours(-3));
        failedInTheQueue.Fail(Noon.AddHours(-3));
        await AddAsync(database, failedInTheQueue);

        Assert.Null(await ClaimAsync(database, Noon));
    }

    [Fact]
    public async Task ClaimNext_PassesOverARowAnotherWorkerHoldsLocked()
    {
        var database = await CreateDatabaseAsync();
        var held = await AddAsync(database, Queued(Noon.AddMinutes(-2)));
        var free = await AddAsync(database, Queued(Noon.AddMinutes(-1)));

        // Another worker mid-claim on the oldest row: its transaction is open and the row is locked.
        await using var other = database.CreateContext();
        await using var otherTransaction = await other.Database.BeginTransactionAsync(Token);
        await other.Submissions
            .FromSql($"SELECT * FROM submissions.submissions WHERE id = {held.Id} FOR UPDATE")
            .ToListAsync(Token);

        var claim = await ClaimAsync(database, Noon);

        Assert.NotNull(claim);
        Assert.Equal(free.Id, claim.SubmissionId);
    }

    [Fact]
    public async Task ConcurrentClaims_NeverHandOutOneAttemptTwice()
    {
        var database = await CreateDatabaseAsync();

        for (var i = 0; i < 6; i++)
        {
            await AddAsync(database, Queued(Noon.AddSeconds(-60 + i)));
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => ClaimAsync(database, Noon)));
        var taken = claims.OfType<SubmissionClaim>().Select(claim => claim.SubmissionId).ToArray();

        Assert.NotEmpty(taken);
        Assert.Equal(taken.Length, taken.Distinct().Count());

        // Whatever the interleaving left, draining one at a time takes the rest, and every attempt ends
        // up claimed exactly once.
        var remaining = new List<Guid>();

        while (await ClaimAsync(database, Noon) is { } claim)
        {
            remaining.Add(claim.SubmissionId);
        }

        Assert.Equal(6, taken.Concat(remaining).Distinct().Count());
        Assert.Equal(6, taken.Length + remaining.Count);
    }

    [Fact]
    public async Task ARunningAttempt_IsLeftAlone_UntilItsClaimTimesOut()
    {
        var database = await CreateDatabaseAsync();
        await AddAsync(database, Queued(Noon));

        Assert.NotNull(await ClaimAsync(database, Noon));

        Assert.Null(await ClaimAsync(database, Noon + ClaimTimeout - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task AnAbandonedAttempt_IsReclaimed_AndTheClaimItReplacedCanNoLongerRecord()
    {
        // The worker that took the first claim died — or stalled past the timeout. Its attempt is taken
        // over, evaluated again, and only the new claim's result is recorded.
        var database = await CreateDatabaseAsync();
        var attempt = await AddAsync(database, Queued(Noon));

        var first = await ClaimAsync(database, Noon);
        var second = await ClaimAsync(database, Noon + ClaimTimeout + TimeSpan.FromSeconds(1));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(attempt.Id, second.SubmissionId);
        Assert.True(second.ClaimedAt > first.ClaimedAt);

        Assert.False(await Queue(database.CreateContext(), Noon.AddMinutes(20)).CompleteAsync(first, 40, Token));
        Assert.Equal(SubmissionStatus.Running, (await ReadAsync(database, attempt.Id)).Status);

        Assert.True(await Queue(database.CreateContext(), Noon.AddMinutes(21)).CompleteAsync(second, 90, Token));

        var row = await ReadAsync(database, attempt.Id);
        Assert.Equal(SubmissionStatus.Completed, row.Status);
        Assert.Equal(90, row.Score);
        Assert.Equal(Noon.AddMinutes(21), row.CompletedAt);
    }

    [Fact]
    public async Task Complete_OnTheClaimStillHeld_RecordsTheScoreAndTheTime()
    {
        var database = await CreateDatabaseAsync();
        var attempt = await AddAsync(database, Queued(Noon));
        var claim = await ClaimAsync(database, Noon.AddMinutes(1));

        Assert.True(await Queue(database.CreateContext(), Noon.AddMinutes(2)).CompleteAsync(claim!, 87, Token));

        var row = await ReadAsync(database, attempt.Id);
        Assert.Equal(SubmissionStatus.Completed, row.Status);
        Assert.Equal(87, row.Score);
        Assert.Equal(Noon.AddMinutes(1), row.StartedAt);
        Assert.Equal(Noon.AddMinutes(2), row.CompletedAt);
    }

    [Fact]
    public async Task Fail_OnTheClaimStillHeld_RecordsTheFailure()
    {
        var database = await CreateDatabaseAsync();
        var attempt = await AddAsync(database, Queued(Noon));
        var claim = await ClaimAsync(database, Noon.AddMinutes(1));

        Assert.True(await Queue(database.CreateContext(), Noon.AddMinutes(2)).FailAsync(claim!, Token));

        var row = await ReadAsync(database, attempt.Id);
        Assert.Equal(SubmissionStatus.Failed, row.Status);
        Assert.Null(row.Score);
        Assert.Equal(Noon.AddMinutes(2), row.CompletedAt);
    }

    [Fact]
    public async Task RecordingTwiceOnOneClaim_RecordsOnlyTheFirst()
    {
        var database = await CreateDatabaseAsync();
        var attempt = await AddAsync(database, Queued(Noon));
        var claim = await ClaimAsync(database, Noon.AddMinutes(1));

        Assert.True(await Queue(database.CreateContext(), Noon.AddMinutes(2)).CompleteAsync(claim!, 70, Token));
        Assert.False(await Queue(database.CreateContext(), Noon.AddMinutes(3)).FailAsync(claim!, Token));
        Assert.False(await Queue(database.CreateContext(), Noon.AddMinutes(3)).CompleteAsync(claim!, 10, Token));

        var row = await ReadAsync(database, attempt.Id);
        Assert.Equal(SubmissionStatus.Completed, row.Status);
        Assert.Equal(70, row.Score);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Complete_WithAScoreOutsideTheRange_IsRefusedBeforeTheDatabaseIsAsked(int score)
    {
        var database = await CreateDatabaseAsync();
        var attempt = await AddAsync(database, Queued(Noon));
        var claim = await ClaimAsync(database, Noon.AddMinutes(1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Queue(database.CreateContext(), Noon.AddMinutes(2)).CompleteAsync(claim!, score, Token));

        Assert.Equal(SubmissionStatus.Running, (await ReadAsync(database, attempt.Id)).Status);
    }

    private Task<SubmissionsDatabase> CreateDatabaseAsync() =>
        SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionDispatcherTests));

    private static Submission Queued(DateTimeOffset createdAt) =>
        Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), createdAt);

    /// <summary>A claim from a context of its own, as a separate worker would make it.</summary>
    private static async Task<SubmissionClaim?> ClaimAsync(SubmissionsDatabase database, DateTimeOffset at)
    {
        await using var context = database.CreateContext();

        return await Queue(context, at).ClaimNextAsync(Token);
    }

    private static SubmissionDispatcher Queue(SubmissionsDbContext context, DateTimeOffset at) =>
        new(context, Options.Create(new SubmissionQueueOptions { ClaimTimeout = ClaimTimeout }), new FixedClock(at));

    private static async Task<Submission> AddAsync(SubmissionsDatabase database, Submission submission)
    {
        await using var context = database.CreateContext();
        context.Submissions.Add(submission);
        await context.SaveChangesAsync(Token);

        return submission;
    }

    private static async Task<Submission> ReadAsync(SubmissionsDatabase database, Guid id)
    {
        await using var context = database.CreateContext();

        return await context.Submissions.AsNoTracking().SingleAsync(row => row.Id == id, Token);
    }
}
