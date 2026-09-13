using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// Listing and reading a workspace's files over HTTP, on a workspace opened through the real
/// composition root from a package published by real ingest — so what a client reads is compared
/// against the package on disk, not against a fixture written to agree with it.
/// </summary>
public sealed class WorkspaceFileEndpointsTests(WorkspaceApi api) : IClassFixture<WorkspaceApi>
{
    [Fact]
    public async Task Files_OfANewWorkspace_AreItsStarterTree_WithTheirSizes()
    {
        var workspaceId = await OpenWorkspaceAsync();

        var response = await GetAsync($"/api/v1/workspaces/{workspaceId}/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tree = await response.Content.ReadFromJsonAsync<FileTreeResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(tree);
        Assert.Equal(api.Package.WorkspaceFiles, tree.Files.Select(file => file.Path));
        Assert.All(tree.Files, file => Assert.Equal(new FileInfo(StarterPath(file.Path)).Length, file.SizeBytes));
    }

    [Fact]
    public async Task Content_AnswersAStarterFile_AsItIsInThePackage()
    {
        // The issue's "frontend can load workspace files": the tree names a path, and that path reads.
        var workspaceId = await OpenWorkspaceAsync();

        var response = await ReadFileAsync(workspaceId, "src/InvoiceSplitter.cs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var file = await response.Content.ReadFromJsonAsync<FileResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(file);
        Assert.Equal("src/InvoiceSplitter.cs", file.Path);
        Assert.Equal(new FileInfo(StarterPath(file.Path)).Length, file.SizeBytes);
        Assert.Equal(await File.ReadAllTextAsync(StarterPath(file.Path), TestContext.Current.CancellationToken), file.Content);
    }

    [Theory]
    [InlineData("problem.yaml")]
    [InlineData("description.md")]
    [InlineData("src")]
    public async Task Content_ForAPathTheTreeDoesNotHold_Answers404WorkspaceFileNotFound(string path)
    {
        // The manifest and the description are in the bundle, beside the starter tree, and never in a
        // workspace — reading one by name is a miss, not a way into the package.
        var workspaceId = await OpenWorkspaceAsync();

        await AssertErrorAsync(await ReadFileAsync(workspaceId, path), HttpStatusCode.NotFound, "workspace_file_not_found");
    }

    [Theory]
    [InlineData("../problem.yaml")]
    [InlineData("src/../../problem.yaml")]
    [InlineData("/src/InvoiceSplitter.cs")]
    [InlineData("src\\InvoiceSplitter.cs")]
    [InlineData("./src/InvoiceSplitter.cs")]
    [InlineData("")]
    public async Task Content_ForAPathThatCouldLeaveTheTree_Answers400NamingPath(string path)
    {
        // The issue's "invalid paths rejected", over HTTP. These arrive verbatim because the path is a
        // query value: in the URL path the server would have removed the dot segments before routing,
        // and the refusal could not be observed.
        var workspaceId = await OpenWorkspaceAsync();

        using var document = await AssertErrorAsync(await ReadFileAsync(workspaceId, path), HttpStatusCode.BadRequest, "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("path", out _));
    }

    [Fact]
    public async Task Content_WithNoPathAtAll_Answers400NamingPath()
    {
        var workspaceId = await OpenWorkspaceAsync();

        using var document = await AssertErrorAsync(
            await GetAsync($"/api/v1/workspaces/{workspaceId}/files/content"),
            HttpStatusCode.BadRequest,
            "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("path", out _));
    }

    [Fact]
    public async Task BothEndpoints_AnswerAnotherUsersWorkspaceAs404WorkspaceNotFound()
    {
        // Written straight into the table, as in WorkspaceEndpointsTests: this host has one identity,
        // so no request could create a workspace that belongs to someone else. It has no snapshot
        // either, which is the point — ownership is refused before storage is ever asked.
        var theirs = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>();
            context.Workspaces.Add(theirs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await AssertErrorAsync(await GetAsync($"/api/v1/workspaces/{theirs.Id}/files"), HttpStatusCode.NotFound, "workspace_not_found");
        await AssertErrorAsync(await ReadFileAsync(theirs.Id.ToString(), "src/App.cs"), HttpStatusCode.NotFound, "workspace_not_found");
    }

    [Theory]
    [InlineData("0199aa00-0000-7000-8000-00000000abcd")]
    [InlineData("not-a-workspace-id")]
    public async Task BothEndpoints_AnswerAnIdNothingNames_As404InTheUnifiedErrorBody(string id)
    {
        await AssertErrorAsync(await GetAsync($"/api/v1/workspaces/{id}/files"), HttpStatusCode.NotFound, "workspace_not_found");
        await AssertErrorAsync(await ReadFileAsync(id, "src/App.cs"), HttpStatusCode.NotFound, "workspace_not_found");
    }

    /// <summary>A fresh version per call, for the reason <see cref="WorkspaceApi"/> gives.</summary>
    private async Task<string> OpenWorkspaceAsync()
    {
        var version = await api.PublishVersionAsync();

        var response = await api.Client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces", UriKind.Relative),
            new { problemVersionId = version.ProblemVersionId },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(workspace);

        return workspace.Id.ToString();
    }

    private string StarterPath(string path) =>
        Path.Combine(api.Package.PackageDirectory, api.Package.Manifest.Workspace.Root, path);

    private Task<HttpResponseMessage> GetAsync(string path) =>
        api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> ReadFileAsync(string workspaceId, string path) =>
        GetAsync($"/api/v1/workspaces/{workspaceId}/files/content?path={Uri.EscapeDataString(path)}");

    private static async Task<JsonDocument> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());

        return document;
    }
}

public sealed record FileTreeResponse(IReadOnlyList<FileEntryResponse> Files);

public sealed record FileEntryResponse(string Path, long SizeBytes);

public sealed record FileResponse(string Path, long SizeBytes, string Content);
