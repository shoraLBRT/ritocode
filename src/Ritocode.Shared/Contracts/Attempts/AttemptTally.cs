namespace Ritocode.Shared.Contracts.Attempts;

/// <summary>What <see cref="IAttemptTallyLookup"/> reports about a user (ADR 0007 §3).</summary>
/// <param name="Attempts">Every attempt the user has started, submitted or not.</param>
/// <param name="TasksSolved">
/// The tasks the user has submitted at least one attempt at. The task catalogue's own statuses — new,
/// tried, solved, gave up (SPEC §4.3) — are finer since #229; the admin's count keeps this meaning.
/// </param>
public sealed record AttemptTally(int Attempts, int TasksSolved);
