using Ritocode.Modules.Workspaces.Contracts;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Workspaces.Tests.Contracts;

/// <summary>
/// The contract a submission asks, over a real PostgreSQL. Rows are written directly, so a workspace can
/// belong to a user no request in this assembly could be.
/// </summary>
public sealed class OwnedWorkspaceLookupTests(PostgresTestServer postgres)
{
    [Fact]
    public async Task FindsTheOwnersWorkspace_WithTheReferenceItsRowStores()
    {
        var database = await WorkspacesDatabase.CreateAsync(postgres, nameof(OwnedWorkspaceLookupTests));
        var workspace = await AddAsync(database, Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow));

        await using var context = database.CreateContext();
        var found = await new OwnedWorkspaceLookup(context).FindAsync(workspace.UserId, workspace.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(workspace.Id, found.Id);
        Assert.Equal(workspace.ProblemVersionId, found.ProblemVersionId);
        Assert.Equal(workspace.SnapshotReference, found.SnapshotReference);
    }

    [Fact]
    public async Task AnswersNullForAnotherUsersWorkspace_AsForOneThatDoesNotExist()
    {
        var database = await WorkspacesDatabase.CreateAsync(postgres, nameof(OwnedWorkspaceLookupTests));
        var theirs = await AddAsync(database, Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        var me = Guid.CreateVersion7();

        await using var context = database.CreateContext();
        var lookup = new OwnedWorkspaceLookup(context);

        Assert.Null(await lookup.FindAsync(me, theirs.Id, TestContext.Current.CancellationToken));
        Assert.Null(await lookup.FindAsync(me, Guid.CreateVersion7(), TestContext.Current.CancellationToken));
    }

    private static async Task<Workspace> AddAsync(WorkspacesDatabase database, Workspace workspace)
    {
        await using var context = database.CreateContext();
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return workspace;
    }
}
