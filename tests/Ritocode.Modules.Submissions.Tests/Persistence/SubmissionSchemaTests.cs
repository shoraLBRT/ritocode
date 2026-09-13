using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Submissions.Tests.Persistence;

/// <summary>
/// The lifecycle as the schema enforces it: every transition the entity allows is one
/// <c>ck_submissions_completed_at_matches_status</c> accepts, and a row that breaks the rule — written
/// by anything that is not the entity — is refused.
/// </summary>
public sealed class SubmissionSchemaTests(PostgresTestServer postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private const string CompletedAtConstraint = "ck_submissions_completed_at_matches_status";

    [Fact]
    public async Task QueuedRunningCompleted_IsAcceptedAtEveryStep()
    {
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await TransitionAsync(database, submission.Id, attempt => attempt.Start());
        await TransitionAsync(database, submission.Id, attempt => attempt.Complete(87, Noon.AddMinutes(5)));

        var row = await ReadAsync(database, submission.Id);
        Assert.Equal(SubmissionStatus.Completed, row.Status);
        Assert.Equal(87, row.Score);
        Assert.Equal(Noon.AddMinutes(5), row.CompletedAt);
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
        Assert.Equal(Noon.AddMinutes(1), row.CompletedAt);
    }

    [Theory]
    [InlineData("Completed", false)]
    [InlineData("Failed", false)]
    [InlineData("Queued", true)]
    [InlineData("Running", true)]
    public async Task AStatusAndACompletionTimeThatDisagree_AreRefusedByTheSchema(string status, bool completed)
    {
        // Written around the entity on purpose: the constraint exists for the worker that crashes
        // between two statements, which is exactly the writer the entity cannot stop.
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionSchemaTests));
        var submission = await AddAsync(database, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await using var context = database.CreateContext();

        FormattableString update;

        if (completed)
        {
            update = $"UPDATE submissions.submissions SET status = {status}, completed_at = {Noon} WHERE id = {submission.Id}";
        }
        else
        {
            update = $"UPDATE submissions.submissions SET status = {status}, completed_at = NULL WHERE id = {submission.Id}";
        }

        var refused = await Assert.ThrowsAnyAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync(update, TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
        Assert.Equal(CompletedAtConstraint, refused.ConstraintName);
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
