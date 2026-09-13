using System.Net;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>The per-user cap on submitting, over HTTP, through the real composition root.</summary>
public sealed class SubmissionRateLimitEndpointTests(RateLimitedWorkspaceApi api) : IClassFixture<RateLimitedWorkspaceApi>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_PastTheCap_Answers429InTheUnifiedErrorBody_QueuesNothing_AndReadsStayOpen()
    {
        // One test rather than several: every request in this host is the same identity, so tests in this
        // class would share one cap and answer depending on which ran first.
        var workspaceId = await OpenWorkspaceAsync();

        for (var i = 0; i < RateLimitedWorkspaceApi.MaxSubmissions; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await SubmitAsync(workspaceId)).StatusCode);
        }

        var refused = await SubmitAsync(workspaceId);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token));
        Assert.Equal("submission_rate_limited", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());

        // Nothing was queued for the refused attempt, and reading is not what the cap limits.
        var history = await api.Client.GetAsync(new Uri($"/api/v1/submissions?workspaceId={workspaceId}", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        var page = await history.Content.ReadFromJsonAsync<SubmissionPageResponse>(Token);
        Assert.NotNull(page);
        Assert.Equal(RateLimitedWorkspaceApi.MaxSubmissions, page.TotalItems);
    }

    private async Task<Guid> OpenWorkspaceAsync()
    {
        var version = await api.PublishVersionAsync();

        var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces", UriKind.Relative),
            new { problemVersionId = version.ProblemVersionId },
            Token);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(Token);
        Assert.NotNull(workspace);
        return workspace.Id;
    }

    private Task<HttpResponseMessage> SubmitAsync(Guid workspaceId) =>
        api.Client.PostAsJsonAsync(new Uri("/api/v1/submissions", UriKind.Relative), new { workspaceId }, Token);
}
