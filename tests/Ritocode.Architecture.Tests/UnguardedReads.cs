using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// Never called. Each member reaches a user's rows without the owner in the query, in a different
/// shape, so <see cref="OwnershipRuleTests"/> can prove its reader sees every one of them — a rule that
/// silently stopped seeing would pass over the module code exactly as a clean one does.
/// </summary>
internal static class UnguardedReads
{
    public static IQueryable<Workspace> ThroughTheSetProperty(WorkspacesDbContext context) =>
        context.Workspaces;

    public static IQueryable<Submission> ThroughSet(SubmissionsDbContext context) =>
        context.Set<Submission>();

    public static ValueTask<Workspace?> ByKey(WorkspacesDbContext context, Guid id) =>
        context.FindAsync<Workspace>(id);

    public static async Task<Workspace?> InsideAnAsyncMethod(WorkspacesDbContext context, Guid id) =>
        await context.Workspaces.FirstOrDefaultAsync(workspace => workspace.Id == id);

    public static Func<SubmissionsDbContext, IQueryable<SubmissionReport>> InsideALambda() =>
        context => context.SubmissionReports;

    public static IQueryable<Workspace> ThroughRawSql(WorkspacesDbContext context) =>
        context.Database.SqlQuery<Workspace>($"SELECT * FROM workspaces.workspaces");
}
