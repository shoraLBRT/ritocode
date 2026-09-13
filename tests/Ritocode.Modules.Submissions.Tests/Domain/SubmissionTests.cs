using Ritocode.Modules.Submissions.Domain;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Submissions.Tests.Domain;

/// <summary>The lifecycle as the entity enforces it. No database: the schema's half is in SubmissionSchemaTests.</summary>
public sealed class SubmissionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_IsQueued_WithItsInputTreeKeyedByItsOwnId()
    {
        var workspaceId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var submission = Submission.Create(workspaceId, userId, Noon);

        Assert.Equal(SubmissionStatus.Queued, submission.Status);
        Assert.Equal(workspaceId, submission.WorkspaceId);
        Assert.Equal(userId, submission.UserId);
        Assert.Null(submission.Score);
        Assert.Null(submission.StartedAt);
        Assert.Null(submission.CompletedAt);
        Assert.Equal(Noon, submission.CreatedAt);
        Assert.Equal(StorageKeys.SubmissionInputTree(submission.Id), submission.InputReference);
    }

    [Fact]
    public void Create_KeepsOnlyTheMicrosecondsAPostgresTimestampHolds()
    {
        var submission = Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon.AddTicks(12_345));

        Assert.Equal(Noon.AddTicks(12_340), submission.CreatedAt);
    }

    [Fact]
    public void Create_RefusesAnEmptyWorkspaceOrOwner()
    {
        Assert.Throws<ArgumentException>(() => Submission.Create(Guid.Empty, Guid.CreateVersion7(), Noon));
        Assert.Throws<ArgumentException>(() => Submission.Create(Guid.CreateVersion7(), Guid.Empty, Noon));
    }

    [Fact]
    public void Start_TakesAQueuedAttemptToRunning_AndRecordsTheClaim()
    {
        var submission = Queued();

        submission.Start(Noon.AddMinutes(1));

        Assert.Equal(SubmissionStatus.Running, submission.Status);
        Assert.Equal(Noon.AddMinutes(1), submission.StartedAt);
        Assert.Null(submission.CompletedAt);
    }

    [Fact]
    public void Start_KeepsTheClaimTimeToTheMicrosecond_SoASqlComparisonMatchesIt()
    {
        var submission = Queued();

        submission.Start(Noon.AddMinutes(1).AddTicks(7));

        Assert.Equal(Noon.AddMinutes(1), submission.StartedAt);
    }

    [Fact]
    public void Reclaim_MovesTheClaimOfARunningAttemptForward()
    {
        var submission = Running();

        submission.Reclaim(Noon.AddMinutes(20));

        Assert.Equal(SubmissionStatus.Running, submission.Status);
        Assert.Equal(Noon.AddMinutes(20), submission.StartedAt);
    }

    [Fact]
    public void Reclaim_ToATimeNoLaterThanTheClaimItReplaces_IsRefused()
    {
        // A claim that did not move could not be told apart from the one it took over, and the worker
        // that held the old one could still record.
        var submission = Running();

        Assert.Throws<InvalidOperationException>(() => submission.Reclaim(Noon.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => submission.Reclaim(Noon));
        Assert.Equal(Noon.AddMinutes(1), submission.StartedAt);
    }

    [Fact]
    public void Reclaim_OfAnAttemptThatIsNotRunning_IsRefused()
    {
        var queued = Queued();
        var completed = Running();
        completed.Complete(50, Noon.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => queued.Reclaim(Noon.AddMinutes(5)));
        Assert.Throws<InvalidOperationException>(() => completed.Reclaim(Noon.AddMinutes(5)));
    }

    [Fact]
    public void Complete_RecordsTheScoreAndTheTime_Together()
    {
        var submission = Running();

        submission.Complete(87, Noon.AddMinutes(5));

        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        Assert.Equal(87, submission.Score);
        Assert.Equal(Noon.AddMinutes(5), submission.CompletedAt);
    }

    [Fact]
    public void Complete_OfAnAttemptThatNeverStarted_IsRefused()
    {
        var submission = Queued();

        Assert.Throws<InvalidOperationException>(() => submission.Complete(100, Noon));
        Assert.Equal(SubmissionStatus.Queued, submission.Status);
    }

    [Theory]
    [InlineData(Submission.MinScore - 1)]
    [InlineData(Submission.MaxScore + 1)]
    public void Complete_WithAScoreOutsideTheRange_IsRefused_AndChangesNothing(int score)
    {
        var submission = Running();

        Assert.Throws<ArgumentOutOfRangeException>(() => submission.Complete(score, Noon));
        Assert.Equal(SubmissionStatus.Running, submission.Status);
        Assert.Null(submission.CompletedAt);
    }

    [Fact]
    public void Fail_IsReachableFromTheQueue_WithoutEverRunning()
    {
        var submission = Queued();

        submission.Fail(Noon.AddMinutes(1));

        Assert.Equal(SubmissionStatus.Failed, submission.Status);
        Assert.Null(submission.Score);
        Assert.Null(submission.StartedAt);
        Assert.Equal(Noon.AddMinutes(1), submission.CompletedAt);
    }

    [Fact]
    public void Fail_IsReachableFromARun_AndKeepsTheClaimTime()
    {
        var submission = Running();

        submission.Fail(Noon.AddMinutes(3));

        Assert.Equal(SubmissionStatus.Failed, submission.Status);
        Assert.Equal(Noon.AddMinutes(1), submission.StartedAt);
    }

    [Fact]
    public void ACompletedAttempt_CannotMoveAgain()
    {
        var submission = Running();
        submission.Complete(50, Noon.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => submission.Start(Noon.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => submission.Complete(60, Noon.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => submission.Fail(Noon.AddMinutes(3)));

        Assert.Equal(50, submission.Score);
        Assert.Equal(Noon.AddMinutes(2), submission.CompletedAt);
    }

    [Fact]
    public void AFailedAttempt_CannotMoveAgain()
    {
        var submission = Queued();
        submission.Fail(Noon.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => submission.Start(Noon.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => submission.Fail(Noon.AddMinutes(2)));
    }

    [Fact]
    public void ARunningAttempt_CannotStartAgain()
    {
        var submission = Running();

        Assert.Throws<InvalidOperationException>(() => submission.Start(Noon.AddMinutes(2)));
    }

    [Fact]
    public void ATimeBeforeTheAttemptWasMade_IsRecordedAsWhenItWasMade()
    {
        var submission = Queued();

        submission.Start(Noon.AddMinutes(-5));
        submission.Complete(10, Noon.AddMinutes(-3));

        Assert.Equal(Noon, submission.StartedAt);
        Assert.Equal(Noon, submission.CompletedAt);
    }

    private static Submission Queued() => Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon);

    /// <summary>Claimed at a minute past noon.</summary>
    private static Submission Running()
    {
        var submission = Queued();
        submission.Start(Noon.AddMinutes(1));
        return submission;
    }
}
