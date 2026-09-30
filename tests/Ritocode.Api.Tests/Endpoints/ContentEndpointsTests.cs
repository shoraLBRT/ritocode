using System.Net;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The content reads through the real composition root, signed out. What they answer over ingested
/// content is proved in the Content module's own tests; here it is routing, anonymity and the error
/// shapes of ADR 0003.
/// </summary>
public sealed class ContentEndpointsTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task TheProblemCatalogue_IsOneObject_WithClassesAndCards()
    {
        using var document = await GetJsonAsync("/api/v1/problems", HttpStatusCode.OK);

        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("classes").ValueKind);
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("cards").ValueKind);
    }

    [Fact]
    public async Task TheTaskCatalogue_IsAPage()
    {
        using var document = await GetJsonAsync("/api/v1/tasks", HttpStatusCode.OK);

        Assert.Equal(0, document.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("pageNumber").GetInt32());
    }

    [Fact]
    public async Task APageSizeOutOfRange_IsAValidationError()
    {
        using var document = await GetJsonAsync("/api/v1/tasks?pageSize=1000", HttpStatusCode.BadRequest);

        Assert.Equal("validation_failed", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task AnUnknownTask_IsTaskNotFound()
    {
        using var document = await GetJsonAsync("/api/v1/tasks/no-such-task", HttpStatusCode.NotFound);

        Assert.Equal("task_not_found", document.RootElement.GetProperty("code").GetString());
    }

    private async Task<JsonDocument> GetJsonAsync(string path, HttpStatusCode expected)
    {
        var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
