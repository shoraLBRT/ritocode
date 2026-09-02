namespace Ritocode.Modules.Submissions.Domain;

/// <summary>
/// Lifecycle of a submission, per docs/DOMAIN_MODEL.md. <see cref="Completed"/> and
/// <see cref="Failed"/> are terminal; the database enforces that a terminal submission has a
/// completion timestamp and a non-terminal one does not.
/// </summary>
public enum SubmissionStatus
{
    /// <summary>Accepted and waiting for a worker.</summary>
    Queued = 0,

    /// <summary>A worker is executing the validator pipeline.</summary>
    Running = 1,

    /// <summary>The pipeline ran to completion. The verdict is in the score and the report.</summary>
    Completed = 2,

    /// <summary>The pipeline could not run to completion — infrastructure, not a wrong answer.</summary>
    Failed = 3,
}
