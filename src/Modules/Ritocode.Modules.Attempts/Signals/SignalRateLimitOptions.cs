using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Attempts.Signals;

/// <summary>How many signals one person may send in a window (docs/SPEC.md §9.3).</summary>
/// <remarks>
/// In the manner of the submission limit (<see cref="Lifecycle.AttemptRateLimitOptions"/>): counted over
/// the person's own rows. A learner signals from the extra picks of answers already capped, so the
/// default allows every extra pick of a few reviews and stops a script.
/// </remarks>
public sealed class SignalRateLimitOptions
{
    public const string SectionName = "Attempts:SignalRateLimit";

    /// <summary>Signals one user may send inside any <see cref="Window"/>. The next one is refused.</summary>
    [Range(1, 1000)]
    public int MaxSignals { get; init; } = 10;

    /// <summary>The sliding window the signals are counted over.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(10);
}
