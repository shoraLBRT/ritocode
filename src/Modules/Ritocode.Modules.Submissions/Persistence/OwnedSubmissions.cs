using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Submissions.Domain;

namespace Ritocode.Modules.Submissions.Persistence;

/// <summary>How every read in this module finds a submission: by owner, inside the query.</summary>
/// <remarks>
/// The Submissions twin of the Workspaces module's <c>OwnedWorkspaces</c>, for the same reason: another
/// user's submission and a missing one are the same absent row, so they cannot drift into answering
/// differently, and a 403 would confirm the id exists (ADR 0003). This class and the creation in
/// <c>SubmissionLifecycle.SubmitAsync</c> are the only code in the module allowed to reach the
/// submission set; <c>OwnershipRuleTests</c> fails on any other. The queue worker of #15 reads by status
/// and serves no user, so it will need an allowance of its own that says so.
/// </remarks>
internal static class OwnedSubmissions
{
    /// <summary>
    /// The submission <paramref name="submissionId"/> when it belongs to <paramref name="userId"/>, and
    /// <see langword="null"/> otherwise.
    /// </summary>
    public static Task<Submission?> FindOwnedAsync(
        this SubmissionsDbContext context,
        Guid userId,
        Guid submissionId,
        CancellationToken cancellationToken) =>
        context.Submissions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == submissionId && candidate.UserId == userId,
                cancellationToken);

    /// <summary>
    /// Every submission of <paramref name="userId"/>, unordered, for a caller to filter and page. The
    /// owner is already in the query, so nothing composed onto it can widen it to another user's rows.
    /// </summary>
    public static IQueryable<Submission> OwnedBy(this SubmissionsDbContext context, Guid userId) =>
        context.Submissions
            .AsNoTracking()
            .Where(candidate => candidate.UserId == userId);
}
