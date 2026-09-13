using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Submissions.Tests.Persistence;

/// <summary>
/// The lifecycle as the schema enforces it: every transition the entity allows is one both timestamp
/// constraints accept, and a row that breaks either — written by anything that is not the entity — is
/// refused.
/// </summary>
public sealed class SubmissionSchemaTests(PostgresTestServer postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private const string CompletedAtConstraint = "ck_submissions_completed_at_matches_status";
    private const string StartedAtConstraint = "ck_submissions_started_at_matches_status";

    [Fact]
    public async Task QueuedRunningCompleted_IsAcceptedAtEveryStep()
    {
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await TransitionAsync(database, submission.Id, attempt => attempt.Start(Noon.AddMinutes(1)));
        await TransitionAsync(database, submission.Id, attempt => attempt.Reclaim(Noon.AddMinutes(20)));
        await TransitionAsync(database, submission.Id, attempt => attempt.Complete(87, Noon.AddMinutes(25)));

        var row = await ReadAsync(database, submission.Id);
        Assert.Equal(SubmissionStatus.Completed, row.Status);
        Assert.Equal(87, row.Score);
        Assert.Equal(Noon.AddMinutes(20), row.StartedAt);
        Assert.Equal(Noon.AddMinutes(25), row.CompletedAt);
        Assert.Equal(submission.InputReference, row.InputReference);
    }

    [Fact]
    public async Task AFailureStraightFromTheQueue_IsAccepted()
    {
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await TransitionAsync(database, submission.Id, attempt => attempt.Fail(Noon.AddMinutes(1)));

        var row = await ReadAsync(database, submission.Id);
        Assert.Equal(SubmissionStatus.Failed, row.Status);
        Assert.Null(row.StartedAt);
        Assert.Equal(Noon.AddMinutes(1), row.CompletedAt);
    }

    [Fact]
    public async Task AFailureDuringARun_IsAccepted()
    {
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await TransitionAsync(database, submission.Id, attempt => attempt.Start(Noon.AddMinutes(1)));
        await TransitionAsync(database, submission.Id, attempt => attempt.Fail(Noon.AddMinutes(2)));

        var row = await ReadAsync(database, submission.Id);
        Assert.Equal(SubmissionStatus.Failed, row.Status);
        Assert.Equal(Noon.AddMinutes(1), row.StartedAt);
    }

    [Theory]
    // Each row breaks the completion-time rule and only that one, so the constraint named is the cause.
    [InlineData("Completed", false, true)]
    [InlineData("Failed", false, false)]
    [InlineData("Queued", true, false)]
    [InlineData("Running", true, true)]
    public async Task AStatusAndACompletionTimeThatDisagree_AreRefusedByTheSchema(string status, bool completed, bool started)
    {
        // Written around the entity on purpose: the constraints exist for the worker that crashes
        // between two statements, which is exactly the writer the entity cannot stop.
        var refused = await UpdateAroundTheEntityAsync(status, completed, started);

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
        Assert.Equal(CompletedAtConstraint, refused.ConstraintName);
    }

    [Theory]
    // Each row breaks the claim-time rule and only that one.
    [InlineData("Queued", false, true)]
    [InlineData("Running", false, false)]
    [InlineData("Completed", true, false)]
    public async Task AStatusAndAClaimTimeThatDisagree_AreRefusedByTheSchema(string status, bool completed, bool started)
    {
        var refused = await UpdateAroundTheEntityAsync(status, completed, started);

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
        Assert.Equal(StartedAtConstraint, refused.ConstraintName);
    }

    [Fact]
    public async Task ARowWithNoInputReference_IsRefusedByTheSchema()
    {
        // An attempt with no frozen tree could never be evaluated; the column is required, not defaulted.
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await using var context = database.CreateContext();

        var refused = await Assert.ThrowsAnyAsync<PostgresException>(() => context.Database.ExecuteSqlAsync(
            $"UPDATE submissions.submissions SET input_reference = NULL WHERE id = {submission.Id}",
            TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, refused.SqlState);
    }

    private async Task<PostgresException> UpdateAroundTheEntityAsync(string status, bool completed, bool started)
    {
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await using var context = database.CreateContext();

        // Typed parameters, because a null has no type EF can infer a store mapping from.
        NpgsqlParameter[] parameters =
        [
            new("status", NpgsqlDbType.Text) { Value = status },
            new("completed_at", NpgsqlDbType.TimestampTz) { Value = completed ? Noon.AddMinutes(5) : DBNull.Value },
            new("started_at", NpgsqlDbType.TimestampTz) { Value = started ? Noon.AddMinutes(1) : DBNull.Value },
            new("id", NpgsqlDbType.Uuid) { Value = submission.Id },
        ];

        return await Assert.ThrowsAnyAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE submissions.submissions SET status = @status, completed_at = @completed_at, started_at = @started_at WHERE id = @id",
            parameters,
            TestContext.Current.CancellationToken));
    }

    private static async Task<Submission> AddAsync(SubmissionsDatabase database, Submission submission)
    {
        await using var context = database.CreateContext();
        context.Submissions.Add(submission);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return submission;
    }

    private static async Task TransitionAsync(SubmissionsDatabase database, Guid id, Action<Submission> transition)
    {
        await using var context = database.CreateContext();
        var submission = await context.Submissions.SingleAsync(row => row.Id == id, TestContext.Current.CancellationToken);

        transition(submission);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Submission> ReadAsync(SubmissionsDatabase database, Guid id)
    {
        await using var context = database.CreateContext();

        return await context.Submissions.AsNoTracking().SingleAsync(row => row.Id == id, TestContext.Current.CancellationToken);
    }
}
