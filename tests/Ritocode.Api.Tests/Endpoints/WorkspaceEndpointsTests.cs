using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Storage;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// Opening a workspace over HTTP, through the real composition root: both cross-module contracts
/// resolved for real, a bundle written by real ingest, and a snapshot read back from a real MinIO.
/// </summary>
public sealed class WorkspaceEndpointsTests(WorkspaceApi api) : IClassFixture<WorkspaceApi>
{
    private static readonly Guid DeveloperId = new DevelopmentIdentityOptions().UserId;

    [Fact]
    public async Task Post_OpensAWorkspace_AndAnswers201WithWhereToFindIt()
    {
        var version = await api.PublishVersionAsync();

        var response = await PostAsync(new { problemVersionId = version.ProblemVersionId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await ReadAsync(response);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(version.ProblemVersionId, created.ProblemVersionId);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        // The Location is the address the workspace is opened at, and following it answers with the
        // same workspace — the issue's "user can open workspace", end to end.
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        Assert.Equal($"/api/v1/workspaces/{created.Id}", path);

        var opened = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(created, await ReadAsync(opened));
    }

    [Fact]
    public async Task Post_ForAVersionTheCallerAlreadyOpened_Answers200WithTheSameWorkspace()
    {
        var version = await api.PublishVersionAsync();

        var first = await ReadAsync(await PostAsync(new { problemVersionId = version.ProblemVersionId }));
        var again = await PostAsync(new { problemVersionId = version.ProblemVersionId });

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first, await ReadAsync(again));
    }

    [Fact]
    public async Task TheNewWorkspace_HoldsExactlyTheStarterFilesOfItsVersion()
    {
        // The issue's other acceptance criterion. Compared against the package on disk, so the whole
        // chain — loader, ingest, bundle, contract, materialisation — has to agree for it to pass.
        var version = await api.PublishVersionAsync();
        var created = await ReadAsync(await PostAsync(new { problemVersionId = version.ProblemVersionId }));

        var row = await FindRowAsync(created.Id);
        Assert.Equal(DeveloperId, row.UserId);

        await using var scope = api.Services.CreateAsyncScope();
        using var snapshot = new MemoryStream();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IObjectStore>()
            .GetAsync(row.SnapshotReference, snapshot, TestContext.Current.CancellationToken));

        var tree = await ReadTreeAsync(snapshot);
        Assert.Equal(api.Package.WorkspaceFiles, tree.Keys);

        var starter = Path.Combine(api.Package.PackageDirectory, api.Package.Manifest.Workspace.Root, "src", "InvoiceSplitter.cs");
        Assert.Equal(await File.ReadAllTextAsync(starter, TestContext.Current.CancellationToken), tree["src/InvoiceSplitter.cs"]);
    }

    [Fact]
    public async Task Post_IgnoresAUserIdInTheBody()
    {
        // ADR 0005's second forbidden row, asserted rather than assumed: the owner is whoever
        // authenticated, whatever the body claims.
        var version = await api.PublishVersionAsync();

        var created = await ReadAsync(await PostAsync(new
        {
            problemVersionId = version.ProblemVersionId,
            userId = Guid.CreateVersion7(),
        }));

        Assert.Equal(DeveloperId, (await FindRowAsync(created.Id)).UserId);
    }

    [Fact]
    public async Task Post_ForADraftVersion_Answers404ProblemVersionNotFound()
    {
        var draftId = await api.AddDraftVersionAsync();

        await AssertErrorAsync(
            await PostAsync(new { problemVersionId = draftId }),
            HttpStatusCode.NotFound,
            "problem_version_not_found");
    }

    [Fact]
    public async Task Post_ForAVersionThatDoesNotExist_Answers404ProblemVersionNotFound()
    {
        await AssertErrorAsync(
            await PostAsync(new { problemVersionId = Guid.CreateVersion7() }),
            HttpStatusCode.NotFound,
            "problem_version_not_found");
    }

    [Fact]
    public async Task Post_WithoutAVersion_Answers400NamingTheField()
    {
        using var document = await AssertErrorAsync(
            await PostAsync(new { }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("problemVersionId", out _));
    }

    [Fact]
    public async Task Get_AnotherUsersWorkspace_Answers404WorkspaceNotFound()
    {
        // Written straight into the table: there is only one identity in this host, so no request
        // could create a workspace that belongs to someone else.
        var theirs = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>();
            context.Workspaces.Add(theirs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await AssertErrorAsync(await GetAsync(theirs.Id.ToString()), HttpStatusCode.NotFound, "workspace_not_found");
    }

    [Theory]
    [InlineData("0199aa00-0000-7000-8000-00000000abcd")]
    [InlineData("not-a-workspace-id")]
    public async Task Get_AnIdNothingNames_Answers404InTheUnifiedErrorBody(string id)
    {
        await AssertErrorAsync(await GetAsync(id), HttpStatusCode.NotFound, "workspace_not_found");
    }

    private Task<HttpResponseMessage> PostAsync(object body) =>
        api.Client.PostAsJsonAsync(new Uri("/api/v1/workspaces", UriKind.Relative), body, TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> GetAsync(string id) =>
        api.Client.GetAsync(new Uri($"/api/v1/workspaces/{id}", UriKind.Relative), TestContext.Current.CancellationToken);

    private static async Task<WorkspaceResponse> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    private async Task<Workspace> FindRowAsync(Guid id)
    {
        await using var scope = api.Services.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<WorkspacesDbContext>()
            .Workspaces
            .AsNoTracking()
            .SingleAsync(workspace => workspace.Id == id, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());

        return document;
    }

    private static async Task<SortedDictionary<string, string>> ReadTreeAsync(MemoryStream archive)
    {
        archive.Position = 0;

        await using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);

        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);

        while (await reader.GetNextEntryAsync(cancellationToken: TestContext.Current.CancellationToken) is { } entry)
        {
            using var text = new StreamReader(entry.DataStream ?? Stream.Null);
            files.Add(entry.Name, await text.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        return files;
    }
}

public sealed record WorkspaceResponse(Guid Id, Guid ProblemVersionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
