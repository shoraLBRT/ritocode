using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Attempts.Lifecycle;

/// <summary>How many answers one person may submit in a window (docs/SPEC.md §9.3).</summary>
/// <remarks>
/// The previous product's submission limit, carried over in its manner: counted over the person's own
/// rows rather than in the rate-limiting middleware, so a restart or a second API instance does not
/// reset it. The defaults allow retrying a task several times in a row and stop a script. They are a
/// guess to revisit with real attempts.
/// </remarks>
public sealed class AttemptRateLimitOptions
{
    public const string SectionName = "Attempts:RateLimit";

    /// <summary>Submits one user may make inside any <see cref="Window"/>. The next one is refused.</summary>
    [Range(1, 1000)]
    public int MaxSubmissions { get; init; } = 10;

    /// <summary>The sliding window the submits are counted over.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(10);
}
