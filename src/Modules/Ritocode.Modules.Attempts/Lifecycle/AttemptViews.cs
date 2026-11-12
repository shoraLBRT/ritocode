using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Lifecycle;

// The shapes the attempt endpoints answer with (docs/SPEC.md §9.3). The answer key appears in exactly
// one place: the result of a submitted attempt at a task whose answer the learner has revealed
// (ADR 0017). Before that, a submitted attempt carries its outcome — the score and the counts — and
// nothing that names a card or a leaf.

/// <summary>An attempt; once submitted, its outcome; once the answer is revealed, the answer and the result with the key.</summary>
/// <param name="Practice">True for a submitted attempt that is not the first at its task (SPEC §5.4).</param>
/// <param name="ContentRevision">The content the result was scored against; null until submitted.</param>
/// <param name="Outcome">The score and the counts, naming nothing; null until submitted.</param>
/// <param name="Revealed">Whether the learner has revealed the answer to the task — for good, for every attempt at it.</param>
/// <param name="TaskStatus">Where the learner stands with the task (SPEC §4.3), as the task list shows it.</param>
/// <param name="Answer">The answer as submitted; null until submitted and revealed.</param>
/// <param name="Result">The score line by line with the key; null until submitted and revealed.</param>
/// <param name="Review">The author's note per finding and the lesson; null until submitted and revealed.</param>
/// <param name="SignalledCards">The extra picks the learner has sent a signal for (SPEC §4.8); empty until revealed.</param>
public sealed record AttemptView(
    Guid Id,
    string Task,
    DateTimeOffset StartedAt,
    AttemptStep Step,
    DateTimeOffset? SubmittedAt,
    bool Practice,
    string? ContentRevision,
    AttemptOutcome? Outcome,
    bool Revealed,
    LearnerTaskStatus TaskStatus,
    DiagnosisAnswer? Answer,
    DiagnosisScore? Result,
    AttemptReview? Review,
    IReadOnlyList<string> SignalledCards);

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
