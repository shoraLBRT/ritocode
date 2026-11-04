namespace Ritocode.Shared.Contracts.Attempts;

/// <summary>What <see cref="IAttemptTallyLookup"/> reports about a user (ADR 0007 §3).</summary>
/// <param name="Attempts">Every attempt the user has started, submitted or not.</param>
/// <param name="TasksSolved">
/// The tasks the user has submitted at least one attempt at — what "solved" means on the task
/// catalogue (SPEC §4.3).
/// </param>
public sealed record AttemptTally(int Attempts, int TasksSolved);
