using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Submissions.Queue;

/// <summary>How the submission queue treats an attempt whose worker stopped answering.</summary>
public sealed class SubmissionQueueOptions
{
    public const string SectionName = "Submissions:Queue";

    /// <summary>
    /// How long a claim is trusted. An attempt still <c>Running</c> this long after it was claimed is
    /// presumed abandoned by a process that died, and the next claim takes it over.
    /// </summary>
    /// <remarks>
    /// Has to outlast the longest evaluation, or a live worker's attempt is taken from under it. That
    /// costs work rather than correctness — the second worker evaluates the same frozen input again and
    /// the first worker's result is refused by the claim guard — but it is still waste, so the default is
    /// far above the ~8.5 s per submission ADR 0006 measured, with room for a deadline-length run.
    /// </remarks>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(15);
}
