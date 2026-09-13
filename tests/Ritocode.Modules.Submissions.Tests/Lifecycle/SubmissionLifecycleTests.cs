using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Lifecycle;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Contracts.Workspaces;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Submissions.Tests.Lifecycle;

/// <summary>
/// Submitting, reading and listing attempts against a real PostgreSQL and a real MinIO. The two
/// contracts are answered from fixed lists; what is under test is what this module does with their
/// answers, and what it leaves in its own schema and bucket.
/// </summary>
public sealed class SubmissionLifecycleTests(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private IObjectStore _store = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var storage = await minio.CreateBucketsAsync(nameof(SubmissionLifecycleTests), Token);
        _store = minio.CreateStore(storage);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Submit_QueuesAnAttempt_AndFreezesTheTreeItWasSubmittedWith()
    {
        var scenario = await ArrangeAsync();

        var result = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(scenario.Workspace.Id, result.Value.WorkspaceId);
        Assert.Equal(SubmissionStatus.Queued, result.Value.Status);
        Assert.Null(result.Value.Score);
        Assert.Null(result.Value.CompletedAt);
        Assert.Equal(Noon, result.Value.CreatedAt);

        await using var context = scenario.Database.CreateContext();
        var row = await context.Submissions.AsNoTracking().SingleAsync(Token);

        Assert.Equal(result.Value.Id, row.Id);
        Assert.Equal(scenario.User.Id, row.UserId);
        Assert.Equal(StorageKeys.SubmissionInputTree(row.Id), row.InputReference);

        // Read through the reference the row stores, the way the orchestrator will.
        Assert.Equal("tree as submitted", await ReadTextAsync(row.InputReference));
    }

    [Fact]
    public async Task ASaveAfterSubmitting_LeavesTheFrozenTreeAsItWas()
    {
        // The determinism claim at the storage layer: re-evaluating this attempt must read these bytes,
        // whatever the person did to their workspace afterwards.
        var scenario = await ArrangeAsync();
        var submitted = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id);

        await PutTextAsync(scenario.Workspace.SnapshotReference, "tree saved later");

        Assert.Equal("tree as submitted", await ReadTextAsync(await InputOfAsync(scenario, submitted.Value.Id)));
    }

    [Fact]
    public async Task EachAttempt_FreezesTheTreeAsItWasWhenItWasMade()
    {
        var scenario = await ArrangeAsync();

        var first = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon);
        await PutTextAsync(scenario.Workspace.SnapshotReference, "tree saved later");
        var second = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1));

        Assert.NotEqual(first.Value.Id, second.Value.Id);
        Assert.Equal("tree as submitted", await ReadTextAsync(await InputOfAsync(scenario, first.Value.Id)));
        Assert.Equal("tree saved later", await ReadTextAsync(await InputOfAsync(scenario, second.Value.Id)));
        Assert.Equal(2, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task AnotherUsersWorkspace_IsAnsweredExactlyLikeOneThatDoesNotExist()
    {
        var other = new UserSummary(Guid.CreateVersion7(), "someone-else");
        var scenario = await ArrangeAsync(other);
        var theirs = await AddWorkspaceAsync(scenario.Workspaces, other.Id, "their tree");

        var result = await SubmitAsync(scenario, scenario.User.Id, theirs.Id);
        var nowhere = await SubmitAsync(scenario, scenario.User.Id, Guid.CreateVersion7());

        AssertFailure(result, ErrorType.NotFound, SubmissionLifecycle.WorkspaceNotFoundCode);
        Assert.Equal(nowhere.Error, result.Error);
        Assert.Equal(0, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task AnIdentityThatNamesNoUser_IsUnauthenticated()
    {
        var scenario = await ArrangeAsync();

        var result = await SubmitAsync(scenario, Guid.CreateVersion7(), scenario.Workspace.Id);

        AssertFailure(result, ErrorType.Unauthenticated, "unauthenticated");
        Assert.Equal(0, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task AWorkspaceWhoseSnapshotIsMissing_FailsLoudly_AndLeavesNoRow()
    {
        var scenario = await ArrangeAsync();
        var hollow = await AddWorkspaceAsync(scenario.Workspaces, scenario.User.Id, tree: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(scenario, scenario.User.Id, hollow.Id));

        Assert.Equal(0, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task Get_ReturnsTheCallersOwnAttempt()
    {
        var scenario = await ArrangeAsync();
        var submitted = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id);

        await using var context = scenario.Database.CreateContext();
        var result = await Lifecycle(scenario, context).GetAsync(scenario.User.Id, submitted.Value.Id, Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(submitted.Value, result.Value);
    }

    [Fact]
    public async Task Get_AnswersAnotherUsersAttemptExactlyLikeAMissingOne()
    {
        var scenario = await ArrangeAsync();
        var theirs = await AddRowAsync(scenario, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));

        await using var context = scenario.Database.CreateContext();
        var lifecycle = Lifecycle(scenario, context);

        var result = await lifecycle.GetAsync(scenario.User.Id, theirs.Id, Token);
        var nowhere = await lifecycle.GetAsync(scenario.User.Id, Guid.CreateVersion7(), Token);

        AssertFailure(result, ErrorType.NotFound, SubmissionLifecycle.SubmissionNotFoundCode);
        Assert.Equal(nowhere.Error, result.Error);
    }

    [Fact]
    public async Task List_IsTheCallersHistory_NewestFirst_AndNobodyElses()
    {
        var scenario = await ArrangeAsync();

        var first = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon);
        var second = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1));
        var third = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(2));

        // Someone else's attempt at the same workspace id, and newer than all of them.
        await AddRowAsync(scenario, Submission.Create(scenario.Workspace.Id, Guid.CreateVersion7(), Noon.AddMinutes(3)));

        var page = await ListAsync(scenario, workspaceId: null, PageRequest.Create(1, 20).Value);

        Assert.Equal(3L, page.TotalItems);
        Assert.Equal(new[] { third.Value.Id, second.Value.Id, first.Value.Id }, page.Items.Select(item => item.Id));
        Assert.Equal(third.Value, page.Items[0]);
    }

    [Fact]
    public async Task List_FilteredByAWorkspace_HoldsOnlyItsAttempts()
    {
        var scenario = await ArrangeAsync();
        var another = await AddWorkspaceAsync(scenario.Workspaces, scenario.User.Id, "another tree");

        var mine = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon);
        await SubmitAsync(scenario, scenario.User.Id, another.Id, Noon.AddMinutes(1));

        var page = await ListAsync(scenario, scenario.Workspace.Id, PageRequest.Create(1, 20).Value);

        Assert.Equal(1L, page.TotalItems);
        Assert.Equal(new[] { mine.Value.Id }, page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task List_PastTheLastPage_IsEmpty_AndStillCarriesTheTotal()
    {
        var scenario = await ArrangeAsync();
        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon);
        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1));

        var page = await ListAsync(scenario, workspaceId: null, PageRequest.Create(2, 2).Value);

        Assert.Empty(page.Items);
        Assert.Equal(2L, page.TotalItems);
        Assert.Equal(2, page.PageNumber);
    }

    private static readonly SubmissionRateLimitOptions TwoInTenMinutes = new()
    {
        MaxSubmissions = 2,
        Window = TimeSpan.FromMinutes(10),
    };

    [Fact]
    public async Task AtTheCap_TheNextAttemptIsRefused_AndNothingIsQueued()
    {
        var scenario = await ArrangeAsync();

        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon, TwoInTenMinutes);
        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1), TwoInTenMinutes);

        var refused = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(2), TwoInTenMinutes);

        AssertFailure(refused, ErrorType.RateLimited, SubmissionLifecycle.RateLimitedCode);
        Assert.Equal(2, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task AnAttemptStopsCounting_TheMomentTheWindowHasPassed()
    {
        var scenario = await ArrangeAsync();

        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon, TwoInTenMinutes);
        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1), TwoInTenMinutes);

        // Exactly ten minutes after the first, it has left the window; the second has not.
        var third = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(10), TwoInTenMinutes);
        var fourth = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(10), TwoInTenMinutes);

        Assert.True(third.IsSuccess);
        AssertFailure(fourth, ErrorType.RateLimited, SubmissionLifecycle.RateLimitedCode);
        Assert.Equal(3, await CountRowsAsync(scenario));
    }

    [Fact]
    public async Task TheCapCountsAttemptsAcrossEveryWorkspaceOfTheCaller()
    {
        // A rule about the person: moving to another workspace is not a way around it.
        var scenario = await ArrangeAsync();
        var another = await AddWorkspaceAsync(scenario.Workspaces, scenario.User.Id, "another tree");

        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon, TwoInTenMinutes);
        await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1), TwoInTenMinutes);

        var elsewhere = await SubmitAsync(scenario, scenario.User.Id, another.Id, Noon.AddMinutes(2), TwoInTenMinutes);

        AssertFailure(elsewhere, ErrorType.RateLimited, SubmissionLifecycle.RateLimitedCode);
    }

    [Fact]
    public async Task AnotherUsersAttempts_DoNotCountAgainstTheCaller()
    {
        var scenario = await ArrangeAsync();

        for (var i = 0; i < 3; i++)
        {
            await AddRowAsync(scenario, Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Noon));
        }

        var mine = await SubmitAsync(scenario, scenario.User.Id, scenario.Workspace.Id, Noon.AddMinutes(1), TwoInTenMinutes);

        Assert.True(mine.IsSuccess);
    }

    private sealed record Scenario(
        SubmissionsDatabase Database,
        UserSummary User,
        StubUserLookup Users,
        StubOwnedWorkspaceLookup Workspaces,
        WorkspaceSummary Workspace);

    private async Task<Scenario> ArrangeAsync(params UserSummary[] otherUsers)
    {
        var database = await SubmissionsDatabase.CreateAsync(postgres, nameof(SubmissionLifecycleTests));
        var user = new UserSummary(Guid.CreateVersion7(), "developer");
        var workspaces = new StubOwnedWorkspaceLookup();
        var workspace = await AddWorkspaceAsync(workspaces, user.Id, "tree as submitted");

        return new Scenario(database, user, new StubUserLookup([user, .. otherUsers]), workspaces, workspace);
    }

    /// <summary>A workspace owned by <paramref name="ownerId"/>, with a snapshot holding <paramref name="tree"/> — or none.</summary>
    private async Task<WorkspaceSummary> AddWorkspaceAsync(StubOwnedWorkspaceLookup workspaces, Guid ownerId, string? tree)
    {
        var id = Guid.CreateVersion7();
        var workspace = new WorkspaceSummary(id, Guid.CreateVersion7(), StorageKeys.WorkspaceSnapshot(id));

        if (tree is not null)
        {
            await PutTextAsync(workspace.SnapshotReference, tree);
        }

        workspaces.Add(ownerId, workspace);
        return workspace;
    }

    private async Task<Result<SubmissionDetail>> SubmitAsync(
        Scenario scenario,
        Guid userId,
        Guid workspaceId,
        DateTimeOffset? at = null,
        SubmissionRateLimitOptions? rateLimit = null)
    {
        await using var context = scenario.Database.CreateContext();

        return await Lifecycle(scenario, context, at, rateLimit).SubmitAsync(userId, workspaceId, Token);
    }

    private async Task<Page<SubmissionDetail>> ListAsync(Scenario scenario, Guid? workspaceId, PageRequest request)
    {
        await using var context = scenario.Database.CreateContext();

        return await Lifecycle(scenario, context).ListAsync(scenario.User.Id, workspaceId, request, Token);
    }

    private SubmissionLifecycle Lifecycle(
        Scenario scenario,
        SubmissionsDbContext context,
        DateTimeOffset? at = null,
        SubmissionRateLimitOptions? rateLimit = null) =>
        new(
            context,
            scenario.Users,
            scenario.Workspaces,
            _store,
            Options.Create(rateLimit ?? new SubmissionRateLimitOptions()),
            new FixedClock(at ?? Noon));

    private static async Task<Submission> AddRowAsync(Scenario scenario, Submission submission)
    {
        await using var context = scenario.Database.CreateContext();
        context.Submissions.Add(submission);
        await context.SaveChangesAsync(Token);

        return submission;
    }

    private static async Task<StorageReference> InputOfAsync(Scenario scenario, Guid submissionId)
    {
        await using var context = scenario.Database.CreateContext();

        var row = await context.Submissions.AsNoTracking().SingleAsync(submission => submission.Id == submissionId, Token);
        return row.InputReference;
    }

    private static async Task<int> CountRowsAsync(Scenario scenario)
    {
        await using var context = scenario.Database.CreateContext();

        return await context.Submissions.CountAsync(Token);
    }

    private async Task PutTextAsync(StorageReference reference, string text)
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await _store.PutAsync(reference, content, Token);
    }

    private async Task<string> ReadTextAsync(StorageReference reference)
    {
        using var destination = new MemoryStream();

        Assert.True(await _store.GetAsync(reference, destination, Token), $"{reference} was not found.");

        return Encoding.UTF8.GetString(destination.ToArray());
    }

    private static void AssertFailure<T>(Result<T> result, ErrorType type, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
