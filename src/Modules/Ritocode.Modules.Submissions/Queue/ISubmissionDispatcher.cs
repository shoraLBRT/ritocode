using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Submissions.Queue;

/// <summary>
/// The queue of ADR 0005's reduction table: the <c>submissions</c> table itself, drained with
/// <c>SKIP LOCKED</c>, and the only place an attempt starts or finishes (ADR 0009 §1).
/// </summary>
/// <remarks>
/// <para>
/// A worker claims an attempt, evaluates it with no transaction open, and records the outcome on the
/// claim it holds. Claiming and recording are each one short transaction of their own; nothing here
/// holds a row or a connection across an evaluation.
/// </para>
/// <para>
/// Nothing calls it yet. The loop that claims, evaluates and records arrives with #17, together with
/// the evaluator a claimed attempt needs — a loop before it would leave every attempt it claimed
/// <c>Running</c> with nobody to finish it. A broker would be another implementation of this
/// interface, not a change to anything that calls it.
/// </para>
/// <para>
/// Named a dispatcher rather than a queue because it is not a collection — a type ending in
/// <c>Queue</c> promises one, which is what CA1711 guards — and because ADR 0005's reduction table
/// already calls this seam "one dispatch interface". The namespace and its options keep the word queue,
/// which is what the table is.
/// </para>
/// </remarks>
public interface ISubmissionDispatcher
{
    /// <summary>
    /// Claims the oldest attempt that is queued, or still running under a claim older than
    /// <see cref="SubmissionQueueOptions.ClaimTimeout"/>, and marks it running under a new claim.
    /// </summary>
    /// <returns>The claim, or <see langword="null"/> when there is nothing to take.</returns>
    Task<SubmissionClaim?> ClaimNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the run under <paramref name="claim"/> finished with <paramref name="score"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/>, having recorded nothing, when the claim is no longer held — the attempt
    /// was reclaimed after its claim timed out, or already recorded.
    /// </returns>
    Task<bool> CompleteAsync(SubmissionClaim claim, int score, CancellationToken cancellationToken = default);

    /// <summary>Records that the run under <paramref name="claim"/> could not finish.</summary>
    /// <returns>As for <see cref="CompleteAsync"/>.</returns>
    Task<bool> FailAsync(SubmissionClaim claim, CancellationToken cancellationToken = default);
}

/// <summary>What a worker holds for one claimed attempt.</summary>
/// <param name="SubmissionId">The attempt.</param>
/// <param name="WorkspaceId">Its workspace, with <paramref name="UserId"/> the owner a workspace lookup needs.</param>
/// <param name="UserId">The attempt's owner.</param>
/// <param name="InputReference">The frozen tree to evaluate, as the row stores it — never rebuilt from the id.</param>
/// <param name="ClaimedAt">
/// The claim's identity: the attempt's <c>started_at</c> as this claim set it. Recording compares it,
/// so a claim that was taken over can no longer record.
/// </param>
public sealed record SubmissionClaim(
    Guid SubmissionId,
    Guid WorkspaceId,
    Guid UserId,
    StorageReference InputReference,
    DateTimeOffset ClaimedAt);
