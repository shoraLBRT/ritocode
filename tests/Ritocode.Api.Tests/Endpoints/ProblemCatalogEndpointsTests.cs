using System.Net;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Http;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The catalog over HTTP, through the real composition root: the pagination envelope of ADR 0003,
/// the unified error body, and the JSON shape a client actually receives.
/// </summary>
public sealed class ProblemCatalogEndpointsTests(CatalogApi api) : IClassFixture<CatalogApi>
{
    [Fact]
    public async Task List_ReturnsThePaginationEnvelopeAndNotABareArray()
    {
        using var document = await GetAsync("/api/v1/problems", HttpStatusCode.OK);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(CatalogApi.PublishedCount, root.GetProperty("totalItems").GetInt64());
        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
        Assert.False(root.GetProperty("hasNextPage").GetBoolean());
        Assert.False(root.GetProperty("hasPreviousPage").GetBoolean());

        var slugs = root.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString())
            .ToArray();

        Assert.Equal(CatalogApi.PublishedCount, slugs.Length);
        Assert.Equal(CatalogApi.NewestSlug, slugs[0]);
        Assert.DoesNotContain(CatalogApi.DraftSlug, slugs, StringComparer.Ordinal);
    }

    [Fact]
    public async Task List_WritesDifficultyAsAName()
    {
        using var document = await GetAsync("/api/v1/problems", HttpStatusCode.OK);

        var difficulty = document.RootElement.GetProperty("items")[0].GetProperty("difficulty");

        // A number here would make every client depend on the enum's member order.
        Assert.Equal(JsonValueKind.String, difficulty.ValueKind);
        Assert.Equal("hard", difficulty.GetString());
    }

    [Fact]
    public async Task List_PagesWithPageAndPageSize()
    {
        using var document = await GetAsync("/api/v1/problems?page=2&pageSize=1", HttpStatusCode.OK);
        var root = document.RootElement;

        Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(2, root.GetProperty("pageNumber").GetInt32());
        Assert.True(root.GetProperty("hasPreviousPage").GetBoolean());
        Assert.True(root.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public async Task List_RejectsAPageSizeOverTheMaximum()
    {
        using var document = await GetAsync("/api/v1/problems?pageSize=1000", HttpStatusCode.BadRequest);
        var root = document.RootElement;

        Assert.Equal("validation_failed", root.GetProperty("code").GetString());
        Assert.True(root.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task Get_ReturnsTheDetailOfTheHighestPublishedVersion()
    {
        using var document = await GetAsync($"/api/v1/problems/{CatalogApi.TwoVersionSlug}", HttpStatusCode.OK);
        var root = document.RootElement;

        Assert.Equal(CatalogApi.TwoVersionSlug, root.GetProperty("slug").GetString());
        Assert.Equal("easy", root.GetProperty("difficulty").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Contains("What to improve", root.GetProperty("description").GetString());

        // The id a workspace will be created from (#10). Carried so a client never has to guess it.
        Assert.NotEqual(Guid.Empty, root.GetProperty("problemVersionId").GetGuid());
    }

    [Theory]
    [InlineData("no-such-problem")]
    [InlineData(CatalogApi.DraftSlug)]
    public async Task Get_AnswersUnservableSlugsWithTheUnifiedErrorBody(string slug)
    {
        var response = await api.Client.GetAsync(new Uri($"/api/v1/problems/{slug}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;

        Assert.Equal("problem_not_found", root.GetProperty("code").GetString());
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal(
            response.Headers.GetValues(RequestId.HeaderName).Single(),
            root.GetProperty("requestId").GetString());
    }

    private async Task<JsonDocument> GetAsync(string path, HttpStatusCode expected)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
