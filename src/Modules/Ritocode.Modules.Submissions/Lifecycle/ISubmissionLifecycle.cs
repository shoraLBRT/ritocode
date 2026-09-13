using Ritocode.Modules.Submissions.Domain;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Submissions.Lifecycle;

/// <summary>
/// Making an attempt at a workspace, reading one back, and a user's attempt history.
/// </summary>
/// <remarks>
/// Every method takes the user explicitly rather than reading <c>ICurrentUser</c>, as the workspace
/// lifecycle does: the endpoint is the one place that knows a request is being served. Nothing here
/// runs an attempt — a submission is created <c>Queued</c> and stays so until the worker of #15 exists.
/// </remarks>
public interface ISubmissionLifecycle
{
    /// <summary>
    /// Queues an attempt at <paramref name="workspaceId"/>, freezing the workspace's tree as it is now.
    /// </summary>
    /// <returns>
    /// The queued attempt. Fails with <see cref="SubmissionLifecycle.WorkspaceNotFoundCode"/> for a
    /// workspace that does not exist or belongs to someone else, with
    /// <see cref="SubmissionLifecycle.RateLimitedCode"/> when the user has already made as many attempts
    /// as the window allows, and as unauthenticated when <paramref name="userId"/> names no user.
    /// </returns>
    Task<Result<SubmissionDetail>> SubmitAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The attempt <paramref name="submissionId"/>, when it belongs to <paramref name="userId"/>. Another
    /// user's attempt fails exactly as a missing one does, with
    /// <see cref="SubmissionLifecycle.SubmissionNotFoundCode"/>.
    /// </summary>
    Task<Result<SubmissionDetail>> GetAsync(Guid userId, Guid submissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="userId"/>'s attempts, newest first, optionally only those at
    /// <paramref name="workspaceId"/>.
    /// </summary>
    Task<Page<SubmissionDetail>> ListAsync(
        Guid userId,
        Guid? workspaceId,
        PageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>What a client is told about an attempt. The input tree's reference stays inside the API.</summary>
public sealed record SubmissionDetail(
    Guid Id,
    Guid WorkspaceId,
    SubmissionStatus Status,
    int? Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
