using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Storage;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// Submitting a workspace over HTTP, through the real composition root: a workspace opened on a version
/// published by real ingest, the Workspaces contract resolved for real, and the tree frozen by a real
/// server-side copy in MinIO.
/// </summary>
public sealed class SubmissionEndpointsTests(WorkspaceApi api) : IClassFixture<WorkspaceApi>
{
    private static readonly Guid DeveloperId = new DevelopmentIdentityOptions().UserId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_QueuesAnAttempt_AndAnswers201WithWhereToFindIt()
    {
        var workspaceId = await OpenWorkspaceAsync();

        var response = await PostAsync(new { workspaceId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await ReadAsync(response);
        Assert.Equal(workspaceId, created.WorkspaceId);
        Assert.Equal("queued", created.Status);
        Assert.Null(created.Score);
        Assert.Null(created.CompletedAt);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        Assert.Equal($"/api/v1/submissions/{created.Id}", path);

        var fetched = await api.Client.GetAsync(new Uri(path, UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(created, await ReadAsync(fetched));
    }

    [Fact]
    public async Task TheAttempt_HoldsACopyOfTheWorkspaceTree_InEvaluationArtifacts()
    {
        var workspaceId = await OpenWorkspaceAsync();
        var created = await ReadAsync(await PostAsync(new { workspaceId }));

        await using var scope = api.Services.CreateAsyncScope();

        var submission = await scope.ServiceProvider.GetRequiredService<SubmissionsDbContext>()
            .Submissions.AsNoTracking().SingleAsync(row => row.Id == created.Id, Token);

        var workspace = await scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>()
            .Workspaces.AsNoTracking().SingleAsync(row => row.Id == workspaceId, Token);

        Assert.Equal(DeveloperId, submission.UserId);
        Assert.Equal(StorageKeys.SubmissionInputTree(submission.Id), submission.InputReference);

        var store = scope.ServiceProvider.GetRequiredService<IObjectStore>();
        Assert.Equal(await ReadBytesAsync(store, workspace.SnapshotReference), await ReadBytesAsync(store, submission.InputReference));
    }

    [Fact]
    public async Task Post_IgnoresAUserIdInTheBody()
    {
        var workspaceId = await OpenWorkspaceAsync();

        var created = await ReadAsync(await PostAsync(new { workspaceId, userId = Guid.CreateVersion7() }));

        await using var scope = api.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<SubmissionsDbContext>()
            .Submissions.AsNoTracking().SingleAsync(submission => submission.Id == created.Id, Token);

        Assert.Equal(DeveloperId, row.UserId);
    }

    [Fact]
    public async Task Post_ForAnotherUsersWorkspace_Answers404WorkspaceNotFound()
    {
        // Written straight into the table: this host has one identity, so no request could open a
        // workspace that belongs to someone else.
        var theirs = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>();
            context.Workspaces.Add(theirs);
            await context.SaveChangesAsync(Token);
        }

        await AssertErrorAsync(await PostAsync(new { workspaceId = theirs.Id }), HttpStatusCode.NotFound, "workspace_not_found");
    }

    [Fact]
    public async Task Post_ForAWorkspaceThatDoesNotExist_Answers404WorkspaceNotFound()
    {
        await AssertErrorAsync(
            await PostAsync(new { workspaceId = Guid.CreateVersion7() }),
            HttpStatusCode.NotFound,
            "workspace_not_found");
    }

    [Fact]
    public async Task Post_WithoutAWorkspace_Answers400NamingTheField()
    {
        using var document = await AssertErrorAsync(await PostAsync(new { }), HttpStatusCode.BadRequest, "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("workspaceId", out _));
    }

    [Fact]
    public async Task Get_AnotherUsersAttempt_Answers404SubmissionNotFound()
    {
        var theirs = Submission.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SubmissionsDbContext>();
            context.Submissions.Add(theirs);
            await context.SaveChangesAsync(Token);
        }

        await AssertErrorAsync(await GetAsync($"/api/v1/submissions/{theirs.Id}"), HttpStatusCode.NotFound, "submission_not_found");
    }

    [Theory]
    [InlineData("0199aa00-0000-7000-8000-00000000abcd")]
    [InlineData("not-a-submission-id")]
    public async Task Get_AnIdNothingNames_Answers404InTheUnifiedErrorBody(string id)
    {
        await AssertErrorAsync(await GetAsync($"/api/v1/submissions/{id}"), HttpStatusCode.NotFound, "submission_not_found");
    }

    [Fact]
    public async Task List_AnswersTheAttemptsAtAWorkspace_NewestFirst_InThePageEnvelope()
    {
        var workspaceId = await OpenWorkspaceAsync();
        var first = await ReadAsync(await PostAsync(new { workspaceId }));
        var second = await ReadAsync(await PostAsync(new { workspaceId }));

        var response = await GetAsync($"/api/v1/submissions?workspaceId={workspaceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<SubmissionPageResponse>(Token);
        Assert.NotNull(page);
        Assert.Equal(2L, page.TotalItems);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(new[] { second, first }, page.Items);
    }

    [Fact]
    public async Task List_FilteredByAWorkspaceIdThatIsNotAnId_IsAnEmptyPage()
    {
        var response = await GetAsync("/api/v1/submissions?workspaceId=not-a-workspace");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<SubmissionPageResponse>(Token);
        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0L, page.TotalItems);
    }

    [Fact]
    public async Task List_WithAPageSizeOutOfRange_Answers400NamingIt()
    {
        using var document = await AssertErrorAsync(
            await GetAsync("/api/v1/submissions?pageSize=1000"),
            HttpStatusCode.BadRequest,
            "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    private async Task<Guid> OpenWorkspaceAsync()
    {
        var version = await api.PublishVersionAsync();

        var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces", UriKind.Relative),
            new { problemVersionId = version.ProblemVersionId },
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(Token);
        Assert.NotNull(workspace);
        return workspace.Id;
    }

    private Task<HttpResponseMessage> PostAsync(object body) =>
        api.Client.PostAsJsonAsync(new Uri("/api/v1/submissions", UriKind.Relative), body, Token);

    private Task<HttpResponseMessage> GetAsync(string path) =>
        api.Client.GetAsync(new Uri(path, UriKind.Relative), Token);

    private static async Task<SubmissionResponse> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<SubmissionResponse>(Token);
        Assert.NotNull(body);
        return body;
    }

    private static async Task<byte[]> ReadBytesAsync(IObjectStore store, StorageReference reference)
    {
        using var destination = new MemoryStream();
        Assert.True(await store.GetAsync(reference, destination, Token), $"{reference} was not found.");
        return destination.ToArray();
    }

    private static async Task<JsonDocument> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());

        return document;
    }
}

public sealed record SubmissionResponse(
    Guid Id,
    Guid WorkspaceId,
    string Status,
    int? Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record SubmissionPageResponse(
    IReadOnlyList<SubmissionResponse> Items,
    int PageNumber,
    int PageSize,
    long TotalItems);
