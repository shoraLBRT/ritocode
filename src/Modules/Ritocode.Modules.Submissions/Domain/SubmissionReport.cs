namespace Ritocode.Modules.Submissions.Domain;

/// <summary>
/// The detailed outcome of one submission: per-validator results and a pointer to the captured
/// runner logs. One report per submission.
/// </summary>
public sealed class SubmissionReport
{
    public const int LogsReferenceMaxLength = 512;

    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    /// <summary>
    /// Per-validator outcomes as JSON. Its shape follows the validator plugin interface defined
    /// by <see href="https://github.com/shoraLBRT/ritocode/issues/18">#18</see>.
    /// </summary>
    public string ValidatorResults { get; set; } = "[]";

    /// <summary>Object storage key of the captured runner logs. Null when nothing was captured.</summary>
    public string? LogsReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Submission? Submission { get; set; }

    public static SubmissionReport Create(
        Guid submissionId,
        string validatorResults,
        string? logsReference,
        DateTimeOffset createdAt) => new()
        {
            Id = Guid.CreateVersion7(),
            SubmissionId = submissionId,
            ValidatorResults = validatorResults,
            LogsReference = logsReference,
            CreatedAt = createdAt.ToUniversalTime(),
        };
}
