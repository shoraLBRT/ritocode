using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Contracts.Workspaces;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Submissions.Lifecycle;

/// <summary>The submission lifecycle over the module's own schema. See <see cref="ISubmissionLifecycle"/>.</summary>
/// <remarks>
/// <para>
/// The constructor is the module's dependency list (ADR 0007 §1): Submissions depends on Users and on
/// Workspaces, through one question each.
/// </para>
/// <para>
/// A workspace may have any number of attempts, queued at once or not, and each freezes its own tree.
/// What a person may not do is make more than <see cref="SubmissionRateLimitOptions.MaxSubmissions"/>
/// of them inside the window — a rule about the person, counted across all their workspaces, not about
/// any one workspace.
/// </para>
/// </remarks>
public sealed class SubmissionLifecycle(
    SubmissionsDbContext context,
    IUserLookup users,
    IOwnedWorkspaceLookup workspaces,
    IObjectStore objectStore,
    IOptions<SubmissionRateLimitOptions> rateLimit,
    TimeProvider timeProvider) : ISubmissionLifecycle
{
    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string SubmissionNotFoundCode = "submission_not_found";

    /// <summary>
    /// Stable code clients branch on, per ADR 0003. The same code the Workspaces module answers for a
    /// workspace a caller cannot see, chosen here rather than handed over (ADR 0007 §2), so a client
    /// branches on one meaning whichever endpoint it asked.
    /// </summary>
    public const string WorkspaceNotFoundCode = "workspace_not_found";

    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string RateLimitedCode = "submission_rate_limited";

    private readonly SubmissionRateLimitOptions _rateLimit = rateLimit.Value;

    public async Task<Result<SubmissionDetail>> SubmitAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        // submissions.user_id has no foreign key (ADR 0004), so this question is what keeps a row from
        // naming a user who does not exist — the Workspaces module asks the identical one on open.
        if (await users.FindAsync(userId, cancellationToken) is null)
        {
            return AppError.Unauthenticated(message: "The authenticated identity does not name a user.");
        }

        var now = timeProvider.GetUtcNow();

        // Before the workspace is looked up and before anything is copied, so a refused attempt costs one
        // count over the (user_id, created_at DESC) index and nothing in the store. Counted over the rows,
        // not in memory, so a restart or a second API instance does not reset it. A burst of concurrent
        // requests can each see room and together pass the cap by the size of the burst; the cap exists to
        // stop sustained load on the runner, not to be an exact count, so that race is accepted.
        var windowStart = now - _rateLimit.Window;
        var recent = await context.OwnedBy(userId)
            .CountAsync(submission => submission.CreatedAt > windowStart, cancellationToken);

        if (recent >= _rateLimit.MaxSubmissions)
        {
            return AppError.RateLimited(
                RateLimitedCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"At most {_rateLimit.MaxSubmissions} submissions can be made in any {_rateLimit.Window:c}. Try again later."));
        }

        // The owner is inside the question, so another user's workspace is not a row this module ever
        // holds — the same absent answer as a workspace that does not exist.
        var workspace = await workspaces.FindAsync(userId, workspaceId, cancellationToken);

        if (workspace is null)
        {
            return AppError.NotFound(WorkspaceNotFoundCode, "No such workspace.");
        }

        var submission = Submission.Create(workspace.Id, userId, now);

        // Frozen before the row that points at it commits — ingest's ordering, for ingest's reason: a
        // failure in between leaves an object nothing references, keyed by an id nothing will produce
        // again, where the other order would leave a queued attempt with no tree to evaluate. The copy
        // needs no lock on the workspace: a put is atomic, so it reads the tree before a concurrent save
        // or after it, never half of one.
        if (!await objectStore.CopyAsync(workspace.SnapshotReference, submission.InputReference, cancellationToken))
        {
            // Opening writes the snapshot before the workspace row commits, so a workspace with no
            // snapshot is a broken store rather than a request the caller could have made differently.
            throw new InvalidOperationException(
                $"Workspace {workspace.Id} exists, but its snapshot '{workspace.SnapshotReference}' does not.");
        }

        context.Submissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);

        return Detail(submission);
    }

    public async Task<Result<SubmissionDetail>> GetAsync(
        Guid userId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var submission = await context.FindOwnedAsync(userId, submissionId, cancellationToken);

        return submission is null ? SubmissionNotFound() : Detail(submission);
    }

    public async Task<Page<SubmissionDetail>> ListAsync(
        Guid userId,
        Guid? workspaceId,
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var owned = context.OwnedBy(userId);

        if (workspaceId is { } workspace)
        {
            owned = owned.Where(submission => submission.WorkspaceId == workspace);
        }

        var totalItems = await owned.LongCountAsync(cancellationToken);

        if (request.Offset >= totalItems)
        {
            return Page<SubmissionDetail>.From([], request, totalItems);
        }

        var items = await owned
            // Newest first, on the (user_id, created_at DESC) index. The id breaks ties: a UUIDv7 agrees
            // with created_at and makes the order total, as it does for the catalog.
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.Id)
            .Skip((int)request.Offset)
            .Take(request.PageSize)
            .Select(submission => new SubmissionDetail(
                submission.Id,
                submission.WorkspaceId,
                submission.Status,
                submission.Score,
                submission.CreatedAt,
                submission.CompletedAt))
            .ToListAsync(cancellationToken);

        return Page<SubmissionDetail>.From(items, request, totalItems);
    }

    internal static AppError SubmissionNotFound() =>
        AppError.NotFound(SubmissionNotFoundCode, "No such submission.");

    private static SubmissionDetail Detail(Submission submission) =>
        new(
            submission.Id,
            submission.WorkspaceId,
            submission.Status,
            submission.Score,
            submission.CreatedAt,
            submission.CompletedAt);
}
