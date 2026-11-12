using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Lifecycle;

/// <summary>A submitted attempt's stored result, read back for the outcome and the status.</summary>
internal static class SubmittedResults
{
    /// <summary>The stored result of a submitted attempt.</summary>
    public static DiagnosisScore ResultOf(this Attempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return attempt.Result is { } result
            ? AttemptsJson.Read<DiagnosisScore>(result)
            : throw new InvalidOperationException($"Attempt {attempt.Id} is open; it has no result.");
    }

    /// <summary>A submitted attempt as the task's status needs it: when, and whether it solved the task.</summary>
    public static SubmittedAttempt AsSubmitted(this Attempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return new SubmittedAttempt(
            attempt.SubmittedAt ?? throw new InvalidOperationException($"Attempt {attempt.Id} is open."),
            AttemptOutcome.Of(attempt.ResultOf()).Solved);
    }
}
