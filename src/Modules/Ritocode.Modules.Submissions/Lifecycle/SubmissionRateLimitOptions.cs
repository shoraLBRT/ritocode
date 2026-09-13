using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Submissions.Lifecycle;

/// <summary>How many attempts one person may make in a window (#35).</summary>
/// <remarks>
/// A submission ends in a container run, so an uncapped submit is a way for one impatient tester — or one
/// loop in a browser tab — to deny everyone else the runner, and for anyone to use it as free compute.
/// The defaults allow working the way a person actually works a task, submitting after each change,
/// while holding a runaway client to one attempt a minute sustained. They are a guess made before anyone
/// has used the slice, and the slice review is where they should be looked at again with data.
/// </remarks>
public sealed class SubmissionRateLimitOptions
{
    public const string SectionName = "Submissions:RateLimit";

    /// <summary>Attempts one user may make inside any <see cref="Window"/>. The next one is refused.</summary>
    [Range(1, 1000)]
    public int MaxSubmissions { get; set; } = 10;

    /// <summary>The sliding window the attempts are counted over.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(10);
}
