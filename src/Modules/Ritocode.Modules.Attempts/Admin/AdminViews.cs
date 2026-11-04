using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Admin;

// The shapes the admin area's signal and attempt endpoints answer with (docs/SPEC.md §6.2).

/// <summary>The learner behind a row. Username and e-mail are null if the user no longer exists.</summary>
public sealed record AdminLearnerView(Guid Id, string? Username, string? Email);

/// <summary>One signal, as the author reads it.</summary>
/// <param name="CardName">The card's name in the catalogue, or its slug if the catalogue does not know it.</param>
/// <param name="ResolvedAt">When it was marked resolved; null while it is open.</param>
public sealed record AdminSignalView(
    Guid Id,
    Guid Attempt,
    string Task,
    string Card,
    string CardName,
    AdminLearnerView Learner,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>One attempt, as the admin reads it: where a learner got to, and how it went.</summary>
/// <param name="Step">The furthest step reached — for an attempt never submitted, where the learner stopped.</param>
/// <param name="Practice">True for a submitted attempt that is not the first at its task; false for the first, and for an open one.</param>
/// <param name="TimeTakenSeconds">From start to submit, in whole seconds; null while open.</param>
public sealed record AdminAttemptView(
    Guid Id,
    string Task,
    AdminLearnerView Learner,
    DateTimeOffset StartedAt,
    AttemptStep Step,
    DateTimeOffset? SubmittedAt,
    bool Practice,
    long? TimeTakenSeconds,
    int? Score,
    int? MaxScore);

/// <summary>Which signals the list shows (SPEC §6.2: open or resolved).</summary>
public enum SignalStatus
{
    Open,
    Resolved,
}

/// <summary>Which attempts the list shows.</summary>
public enum AttemptStatus
{
    All,
    Open,
    Submitted,
}
