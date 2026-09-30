using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Lifecycle;

// The shapes the attempt endpoints answer with (docs/SPEC.md §9.3). The answer key appears in exactly
// one place: the result of a submitted attempt.

/// <summary>An attempt, and once it is submitted, the answer and the result with the key revealed.</summary>
/// <param name="Practice">True for a submitted attempt that is not the first at its task (SPEC §5.4).</param>
/// <param name="ContentRevision">The content the result was scored against; null until submitted.</param>
/// <param name="Review">The author's note per finding and the lesson; null until submitted.</param>
public sealed record AttemptView(
    Guid Id,
    string Task,
    DateTimeOffset StartedAt,
    AttemptStep Step,
    DateTimeOffset? SubmittedAt,
    bool Practice,
    string? ContentRevision,
    DiagnosisAnswer? Answer,
    DiagnosisScore? Result,
    AttemptReview? Review);

/// <summary>The author's words for the review, kept with the attempt on submit.</summary>
public sealed record AttemptReview(IReadOnlyDictionary<string, string> Notes, string? Lesson);

/// <summary>One row of <c>GET /attempts</c>: the history, without the answers.</summary>
public sealed record AttemptSummaryView(
    Guid Id,
    string Task,
    DateTimeOffset StartedAt,
    AttemptStep Step,
    DateTimeOffset? SubmittedAt,
    bool Practice,
    int? Score,
    int? MaxScore);

/// <summary><c>POST /attempts</c>.</summary>
public sealed record StartAttemptRequest(string? Task);

/// <summary><c>PATCH /attempts/{id}</c>: the step reached, <c>diagnosis</c> or <c>treatment</c>.</summary>
public sealed record RecordStepRequest(string? Step);

/// <summary><c>POST /attempts/{id}/submit</c>: the picked cards, each with its leaves.</summary>
public sealed record SubmitAttemptRequest(IReadOnlyList<SubmittedPick>? Picks);

public sealed record SubmittedPick(string? Card, IReadOnlyList<string>? Leaves);
