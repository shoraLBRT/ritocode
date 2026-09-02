namespace Ritocode.Modules.Submissions.Domain;

/// <summary>One evaluation attempt against a workspace. Owned by the Submissions module.</summary>
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

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set exactly when <see cref="Status"/> becomes terminal.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public static Submission Create(Guid workspaceId, Guid userId, DateTimeOffset createdAt) => new()
    {
        Id = Guid.CreateVersion7(),
        WorkspaceId = workspaceId,
        UserId = userId,
        Status = SubmissionStatus.Queued,
        Score = null,
        CreatedAt = createdAt.ToUniversalTime(),
        CompletedAt = null,
    };
}
