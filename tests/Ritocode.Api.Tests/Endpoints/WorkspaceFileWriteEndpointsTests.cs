using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// Saving a workspace file over HTTP, on a workspace opened from a package published by real ingest —
/// so which files may be saved, and how large they may be, comes from the committed problem.yaml
/// through the real Problems contract rather than from a list written for the test.
/// </summary>
public sealed class WorkspaceFileWriteEndpointsTests(WorkspaceApi api) : IClassFixture<WorkspaceApi>
{
    private const string EditablePath = "src/InvoiceSplitter.cs";
    private const string ReadOnlyPath = "tests/InvoiceSplitterTests.cs";
    private const string AnyRevision = "0000000000000000000000000000000000000000000000000000000000000000";

    [Fact]
    public async Task Put_SavesAnEditableFile_AndReopeningTheWorkspaceKeepsTheChange()
    {
        // The issue's acceptance criteria end to end: the edit persists, and a reload keeps it.
        var workspace = await OpenWorkspaceAsync();
        var original = await ReadFileAsync(workspace.Id, EditablePath);
        const string edited = "namespace Billing;\n\npublic static class InvoiceSplitter { }\n";

        var response = await PutFileAsync(workspace.Id, EditablePath, new { content = edited, baseRevision = original.Revision });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var saved = await ReadBodyAsync<SavedFileResponse>(response);
        Assert.Equal(EditablePath, saved.Path);
        Assert.Equal(Encoding.UTF8.GetByteCount(edited), saved.SizeBytes);
        Assert.NotEqual(original.Revision, saved.Revision);

        // Come back to it the way a person does: open the same version again, then read the file.
        var reopened = await PostOpenAsync(workspace.ProblemVersionId);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);

        var again = await ReadBodyAsync<WorkspaceResponse>(reopened);
        Assert.Equal(workspace.Id, again.Id);
        Assert.True(again.UpdatedAt > workspace.UpdatedAt);

        var reread = await ReadFileAsync(workspace.Id, EditablePath);
        Assert.Equal(edited, reread.Content);
        Assert.Equal(saved.Revision, reread.Revision);
    }

    [Fact]
    public async Task Put_OverACopyThatIsNoLongerCurrent_Answers412WorkspaceFileChanged()
    {
        var workspace = await OpenWorkspaceAsync();
        var original = await ReadFileAsync(workspace.Id, EditablePath);

        var first = await PutFileAsync(workspace.Id, EditablePath, new { content = "// first\n", baseRevision = original.Revision });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PutFileAsync(workspace.Id, EditablePath, new { content = "// second\n", baseRevision = original.Revision });

        await AssertErrorAsync(second, HttpStatusCode.PreconditionFailed, "workspace_file_changed");
        Assert.Equal("// first\n", (await ReadFileAsync(workspace.Id, EditablePath)).Content);
    }

    [Fact]
    public async Task Put_AReadOnlyFile_Answers403WorkspaceFileReadOnly_AndLeavesItAsPackaged()
    {
        // The package's tests decide the verdict, and the package says a user may not change them.
        var workspace = await OpenWorkspaceAsync();
        var tests = await ReadFileAsync(workspace.Id, ReadOnlyPath);

        var response = await PutFileAsync(workspace.Id, ReadOnlyPath, new { content = "// no tests\n", baseRevision = tests.Revision });

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "workspace_file_read_only");
        Assert.Equal(
            await File.ReadAllTextAsync(StarterPath(ReadOnlyPath), TestContext.Current.CancellationToken),
            (await ReadFileAsync(workspace.Id, ReadOnlyPath)).Content);
    }

    [Theory]
    [InlineData("problem.yaml")]
    [InlineData("src/NewFile.cs")]
    public async Task Put_APathTheTreeDoesNotHold_Answers404WorkspaceFileNotFound(string path)
    {
        // Neither the manifest beside the tree nor a new file inside it: a save only replaces.
        var workspace = await OpenWorkspaceAsync();

        var response = await PutFileAsync(workspace.Id, path, new { content = "// new\n", baseRevision = AnyRevision });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "workspace_file_not_found");
    }

    [Theory]
    [InlineData("../problem.yaml")]
    [InlineData("src/../src/InvoiceSplitter.cs")]
    [InlineData("/src/InvoiceSplitter.cs")]
    [InlineData("src\\InvoiceSplitter.cs")]
    [InlineData("")]
    public async Task Put_APathThatCouldLeaveTheTree_Answers400NamingPath(string path)
    {
        // ADR 0005: "writing workspace files at paths taken from the request without normalisation" is
        // forbidden, and so is normalising. Observable because the path is a query value.
        var workspace = await OpenWorkspaceAsync();

        using var document = await AssertErrorAsync(
            await PutFileAsync(workspace.Id, path, new { content = "// escape\n", baseRevision = AnyRevision }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("path", out _));
    }

    [Fact]
    public async Task Put_WithNeitherContentNorRevision_Answers400NamingBoth()
    {
        var workspace = await OpenWorkspaceAsync();

        using var document = await AssertErrorAsync(
            await PutFileAsync(workspace.Id, EditablePath, new { }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("content", out _));
        Assert.True(errors.TryGetProperty("baseRevision", out _));
    }

    [Fact]
    public async Task Put_WithARevisionThatCannotBeOne_Answers400NamingBaseRevision()
    {
        // "latest" is the client that meant "overwrite whatever is there". There is no such request.
        var workspace = await OpenWorkspaceAsync();

        using var document = await AssertErrorAsync(
            await PutFileAsync(workspace.Id, EditablePath, new { content = "// mine\n", baseRevision = "latest" }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("baseRevision", out _));
    }

    [Fact]
    public async Task Put_LargerThanThePackageAllowsOneFile_Answers400NamingContent()
    {
        // The committed package's own max_file_bytes, plus one.
        var workspace = await OpenWorkspaceAsync();
        var original = await ReadFileAsync(workspace.Id, EditablePath);
        var tooLarge = new string('a', api.Package.Manifest.Limits.MaxFileBytes + 1);

        using var document = await AssertErrorAsync(
            await PutFileAsync(workspace.Id, EditablePath, new { content = tooLarge, baseRevision = original.Revision }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("content", out _));
        Assert.Equal(original.Revision, (await ReadFileAsync(workspace.Id, EditablePath)).Revision);
    }

    [Fact]
    public async Task Put_AnotherUsersWorkspace_Answers404WorkspaceNotFound()
    {
        // Written straight into the table, as the read tests do: this host has one identity, so no
        // request could create a workspace that belongs to someone else.
        var theirs = Workspace.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>();
            context.Workspaces.Add(theirs);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await PutFileAsync(theirs.Id, EditablePath, new { content = "// mine now\n", baseRevision = AnyRevision });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "workspace_not_found");
    }

    /// <summary>A fresh version per call, for the reason <see cref="WorkspaceApi"/> gives.</summary>
    private async Task<WorkspaceResponse> OpenWorkspaceAsync()
    {
        var version = await api.PublishVersionAsync();
        var response = await PostOpenAsync(version.ProblemVersionId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadBodyAsync<WorkspaceResponse>(response);
    }

    private Task<HttpResponseMessage> PostOpenAsync(Guid problemVersionId) =>
        api.Client.PostAsJsonAsync(
            new Uri("/api/v1/workspaces", UriKind.Relative),
            new { problemVersionId },
            TestContext.Current.CancellationToken);

    private async Task<FileResponse> ReadFileAsync(Guid workspaceId, string path)
    {
        var response = await api.Client.GetAsync(ContentUri(workspaceId, path), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadBodyAsync<FileResponse>(response);
    }

    private Task<HttpResponseMessage> PutFileAsync(Guid workspaceId, string path, object body) =>
        api.Client.PutAsJsonAsync(ContentUri(workspaceId, path), body, TestContext.Current.CancellationToken);

    private static Uri ContentUri(Guid workspaceId, string path) =>
        new($"/api/v1/workspaces/{workspaceId}/files/content?path={Uri.EscapeDataString(path)}", UriKind.Relative);

    private string StarterPath(string path) =>
        Path.Combine(api.Package.PackageDirectory, api.Package.Manifest.Workspace.Root, path);

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var body = await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);

        return body;
    }

    private static async Task<JsonDocument> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());

        return document;
    }
}
