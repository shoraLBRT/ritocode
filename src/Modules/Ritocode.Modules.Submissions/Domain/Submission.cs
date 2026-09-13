using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Submissions.Domain;

/// <summary>One evaluation attempt against a workspace. Owned by the Submissions module.</summary>
/// <remarks>
/// The lifecycle is <c>Queued</c> → <c>Running</c> → <c>Completed</c> or <c>Failed</c>, and a queued
/// attempt may also fail without ever running. <c>Completed</c> and <c>Failed</c> are terminal. The
/// transitions are methods rather than status assignments so the rule lives in one place and the
/// completion time moves with the status: <c>ck_submissions_completed_at_matches_status</c> refuses a
/// terminal row without one, and a transition that set one without the other would learn that at
/// commit, inside a worker.
/// </remarks>
public sealed class Submission
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

    public Guid Id { get; set; }

    /// <summary>
    /// The workspace evaluated. Not a foreign key: <c>workspaces</c> belongs to another module.
    /// See docs/adr/0004-persistence-and-migrations.md.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>Denormalised from the workspace so attempt history is a single-table query.</summary>
    public Guid UserId { get; set; }

    public SubmissionStatus Status { get; set; }

    /// <summary>Null until the pipeline completes. 0-100, aggregated from validator results.</summary>
    public int? Score { get; set; }

    /// <summary>
    /// The frozen copy of the workspace tree this attempt is evaluated from — never the live workspace
    /// snapshot, which every save overwrites (docs/STORAGE_LAYOUT.md). Built from <see cref="Id"/> once,
    /// at creation; every later reader uses this stored value rather than rebuilding the key.
    /// </summary>
    public StorageReference InputReference { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set exactly when <see cref="Status"/> becomes terminal.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>A queued attempt at <paramref name="workspaceId"/>, whose input tree is still to be copied.</summary>
    public static Submission Create(Guid workspaceId, Guid userId, DateTimeOffset createdAt)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A submission is made against a workspace.", nameof(workspaceId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A submission needs an owner.", nameof(userId));
        }

        var id = Guid.CreateVersion7();

        return new Submission
        {
            Id = id,
            WorkspaceId = workspaceId,
            UserId = userId,
            Status = SubmissionStatus.Queued,
            Score = null,
            InputReference = StorageKeys.SubmissionInputTree(id),
            CreatedAt = ToStoredPrecision(createdAt),
            CompletedAt = null,
        };
    }

    /// <summary>A worker has taken the attempt off the queue.</summary>
    /// <exception cref="InvalidOperationException">The attempt is not queued.</exception>
    public void Start()
    {
        if (Status != SubmissionStatus.Queued)
        {
            throw CannotBecome(SubmissionStatus.Running);
        }

        Status = SubmissionStatus.Running;
    }

    /// <summary>The pipeline ran to the end and produced <paramref name="score"/>.</summary>
    /// <exception cref="InvalidOperationException">The attempt is not running.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="score"/> is outside 0-100.</exception>
    public void Complete(int score, DateTimeOffset completedAt)
    {
        if (Status != SubmissionStatus.Running)
        {
            throw CannotBecome(SubmissionStatus.Completed);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(score, MinScore);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(score, MaxScore);

        Status = SubmissionStatus.Completed;
        Score = score;
        CompletedAt = NotBeforeCreation(completedAt);
    }

    /// <summary>
    /// The attempt could not be graded. Allowed from the queue as well as from a run: an attempt whose
    /// input cannot be evaluated at all fails without ever starting.
    /// </summary>
    /// <exception cref="InvalidOperationException">The attempt is already terminal.</exception>
    public void Fail(DateTimeOffset failedAt)
    {
        if (Status is not (SubmissionStatus.Queued or SubmissionStatus.Running))
        {
            throw CannotBecome(SubmissionStatus.Failed);
        }

        Status = SubmissionStatus.Failed;
        Score = null;
        CompletedAt = NotBeforeCreation(failedAt);
    }

    private InvalidOperationException CannotBecome(SubmissionStatus target) =>
        new($"Submission {Id} is {Status} and cannot become {target}.");

    /// <summary>
    /// A clock that stepped back would otherwise record an attempt finishing before it was made, which
    /// no report can explain to a person reading it.
    /// </summary>
    private DateTimeOffset NotBeforeCreation(DateTimeOffset value)
    {
        var timestamp = ToStoredPrecision(value);

        return timestamp < CreatedAt ? CreatedAt : timestamp;
    }

    /// <summary>
    /// UTC, truncated to the microsecond a <c>timestamptz</c> holds, so the attempt a submit answers with
    /// carries the same <c>createdAt</c> as every later read of it — the lesson of <c>Workspace.Create</c>.
    /// </summary>
    private static DateTimeOffset ToStoredPrecision(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();

        return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);
    }
}
