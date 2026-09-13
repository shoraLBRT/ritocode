using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Workspaces.Tests.Lifecycle;

/// <summary>
/// Opening and reading workspaces against a real PostgreSQL and a real MinIO. The two contracts are
/// answered from fixed lists; what is under test is the rule this module applies to their answers,
/// and what it leaves in its own schema and bucket.
/// </summary>
public sealed class WorkspaceLifecycleTests(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private CountingObjectStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        var storage = await minio.CreateBucketsAsync(nameof(WorkspaceLifecycleTests), TestContext.Current.CancellationToken);
        _store = new CountingObjectStore(minio.CreateStore(storage));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Open_CreatesTheRow_AndASnapshotHoldingTheStarterTree()
    {
        var scenario = await ArrangeAsync();

        var result = await OpenAsync(scenario, scenario.User.Id, scenario.Published.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);

        await using var context = scenario.Database.CreateContext();
        var row = await context.Workspaces.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(result.Value.Workspace.Id, row.Id);
        Assert.Equal(scenario.User.Id, row.UserId);
        Assert.Equal(scenario.Published.Id, row.ProblemVersionId);
        Assert.Equal(Noon, row.CreatedAt);
        Assert.Equal(Noon, row.UpdatedAt);
        Assert.Equal(StorageKeys.WorkspaceSnapshot(row.Id), row.SnapshotReference);

        // Read through the reference the row stores, the way every later reader will.
        using var snapshot = new MemoryStream();
        Assert.True(await _store.GetAsync(row.SnapshotReference, snapshot, TestContext.Current.CancellationToken));
        Assert.Equal(Archives.TypicalStarterPaths, (await Archives.ReadAsync(snapshot)).Select(entry => entry.Path));
    }

    [Fact]
    public async Task OpeningAgain_FindsTheSameWorkspace_AndWritesNothing()
    {
        // The draft a person left is what "open" has to find. A second, fresh workspace would put
        // their work one click away from looking lost.
        var scenario = await ArrangeAsync();

        var first = await OpenAsync(scenario, scenario.User.Id, scenario.Published.Id);
        var puts = _store.Puts;
        var second = await OpenAsync(scenario, scenario.User.Id, scenario.Published.Id);

        Assert.False(second.Value.Created);
        Assert.Equal(first.Value.Workspace, second.Value.Workspace);
        Assert.Equal(puts, _store.Puts);
        Assert.Equal(1, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task TwoUsersOnOneVersion_GetAWorkspaceEach()
    {
        var other = new UserSummary(Guid.CreateVersion7(), "someone-else");
        var scenario = await ArrangeAsync(other);

        var mine = await OpenAsync(scenario, scenario.User.Id, scenario.Published.Id);
        var theirs = await OpenAsync(scenario, other.Id, scenario.Published.Id);

        Assert.True(theirs.Value.Created);
        Assert.NotEqual(mine.Value.Workspace.Id, theirs.Value.Workspace.Id);
        Assert.Equal(2, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task ADraftVersion_IsAnsweredAsIfItDidNotExist()
    {
        // The draft has a bundle, so the refusal is the rule and not a missing object.
        var scenario = await ArrangeAsync();

        var result = await OpenAsync(scenario, scenario.User.Id, scenario.Draft.Id);

        AssertFailure(result, ErrorType.NotFound, WorkspaceLifecycle.ProblemVersionNotFoundCode);
        Assert.Equal(0, await CountRowsAsync(scenario));
        Assert.Equal(0, _store.Puts - scenario.BundlePuts);
    }

    [Fact]
    public async Task AVersionWithNoRow_IsNotFound()
    {
        var scenario = await ArrangeAsync();

        var result = await OpenAsync(scenario, scenario.User.Id, Guid.CreateVersion7());

        AssertFailure(result, ErrorType.NotFound, WorkspaceLifecycle.ProblemVersionNotFoundCode);
        Assert.Equal(0, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task AnIdentityThatNamesNoUser_IsUnauthenticated()
    {
        // workspaces.user_id has no foreign key, so this check is all that keeps a row from pointing
        // at a user who does not exist.
        var scenario = await ArrangeAsync();

        var result = await OpenAsync(scenario, Guid.CreateVersion7(), scenario.Published.Id);

        AssertFailure(result, ErrorType.Unauthenticated, "unauthenticated");
        Assert.Equal(0, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task APublishedVersionWhoseBundleIsMissing_FailsLoudly_AndLeavesNoRow()
    {
        var scenario = await ArrangeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OpenAsync(scenario, scenario.User.Id, scenario.Unbundled.Id));

        Assert.Equal(0, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task Get_ReturnsTheCallersOwnWorkspace()
    {
        var scenario = await ArrangeAsync();
        var opened = await OpenAsync(scenario, scenario.User.Id, scenario.Published.Id);

        await using var context = scenario.Database.CreateContext();
        var result = await Lifecycle(scenario, context).GetAsync(
            scenario.User.Id,
            opened.Value.Workspace.Id,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(opened.Value.Workspace, result.Value);
    }

    [Fact]
    public async Task Get_AnswersAnotherUsersWorkspaceExactlyLikeAMissingOne()
    {
        var other = new UserSummary(Guid.CreateVersion7(), "someone-else");
        var scenario = await ArrangeAsync(other);
        var opened = await OpenAsync(scenario, other.Id, scenario.Published.Id);

        await using var context = scenario.Database.CreateContext();
        var lifecycle = Lifecycle(scenario, context);

        var theirs = await lifecycle.GetAsync(scenario.User.Id, opened.Value.Workspace.Id, TestContext.Current.CancellationToken);
        var nobodys = await lifecycle.GetAsync(scenario.User.Id, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        AssertFailure(theirs, ErrorType.NotFound, WorkspaceLifecycle.WorkspaceNotFoundCode);
        Assert.Equal(nobodys.Error, theirs.Error);
    }

    private sealed record Scenario(
        WorkspacesDatabase Database,
        UserSummary User,
        IUserLookup Users,
        IProblemVersionLookup Versions,
        ProblemVersionSummary Published,
        ProblemVersionSummary Draft,
        ProblemVersionSummary Unbundled,
        int BundlePuts);

    private async Task<Scenario> ArrangeAsync(params UserSummary[] otherUsers)
    {
        var database = await WorkspacesDatabase.CreateAsync(postgres, nameof(WorkspaceLifecycleTests));
        var user = new UserSummary(Guid.CreateVersion7(), "developer");

        var published = await BundledVersionAsync(publishedAt: Noon);
        var draft = await BundledVersionAsync(publishedAt: null);
        var unbundledId = Guid.CreateVersion7();
        var unbundled = new ProblemVersionSummary(
            unbundledId, Guid.CreateVersion7(), "no-bundle", 1, Noon, StorageKeys.ProblemBundle(unbundledId), "starter");

        return new Scenario(
            database,
            user,
            new StubUserLookup([user, .. otherUsers]),
            new StubProblemVersionLookup(published, draft, unbundled),
            published,
            draft,
            unbundled,
            _store.Puts);
    }

    private async Task<ProblemVersionSummary> BundledVersionAsync(DateTimeOffset? publishedAt)
    {
        var id = Guid.CreateVersion7();
        var bundle = StorageKeys.ProblemBundle(id);

        using var archive = Archives.TypicalBundle();
        await _store.PutAsync(bundle, archive, TestContext.Current.CancellationToken);

        return new ProblemVersionSummary(id, Guid.CreateVersion7(), "split-the-invoice", 1, publishedAt, bundle, "starter");
    }

    private async Task<Result<OpenedWorkspace>> OpenAsync(Scenario scenario, Guid userId, Guid problemVersionId)
    {
        await using var context = scenario.Database.CreateContext();

        return await Lifecycle(scenario, context).OpenAsync(userId, problemVersionId, TestContext.Current.CancellationToken);
    }

    private WorkspaceLifecycle Lifecycle(Scenario scenario, Persistence.WorkspacesDbContext context) =>
        new(context, scenario.Users, scenario.Versions, _store, new FixedClock(Noon));

    private static async Task<int> CountRowsAsync(Scenario scenario)
    {
        await using var context = scenario.Database.CreateContext();

        return await context.Workspaces.CountAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertFailure<T>(Result<T> result, ErrorType type, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
