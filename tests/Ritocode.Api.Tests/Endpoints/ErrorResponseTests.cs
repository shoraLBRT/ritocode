using System.Net;
using System.Text;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Http;

namespace Ritocode.Api.Tests.Endpoints;

public sealed class ErrorResponseTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public async Task UnhandledException_BecomesOpaque500InTheUnifiedShape()
    {
        var response = await api.Client.GetAsync(new Uri("/__probe/unhandled", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("internal_error", root.GetProperty("code").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("/__probe/unhandled", root.GetProperty("instance").GetString());

        // The exception message must not reach the client.
        Assert.DoesNotContain("probe failure", root.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppException_KeepsItsDomainCodeAndStatus()
    {
        var response = await api.Client.GetAsync(new Uri("/__probe/app-error", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        Assert.Equal("probe_not_found", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Probe resource is missing.", document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ErrorBody_CarriesTheSameRequestIdAsTheResponseHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__probe/app-error");
        request.Headers.Add(RequestId.HeaderName, "corr-42");

        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        using var document = await ReadJsonAsync(response);
        Assert.Equal("corr-42", document.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("corr-42", response.Headers.GetValues(RequestId.HeaderName).Single());
    }

    [Fact]
    public async Task ValidationFailure_Returns400WithPerFieldMessages()
    {
        using var content = new StringContent(
            """{"title":"far too long to pass","count":0}""", Encoding.UTF8, "application/json");

        var response = await api.Client.PostAsync(new Uri("/__probe/echo", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("validation_failed", root.GetProperty("code").GetString());

        var errors = root.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _), "field keys must be camelCase, matching the request JSON");
        Assert.True(errors.TryGetProperty("count", out _));
    }

    [Fact]
    public async Task ValidRequest_PassesThroughTheValidationFilter()
    {
        using var content = new StringContent(
            """{"title":"ok","count":3}""", Encoding.UTF8, "application/json");

        var response = await api.Client.PostAsync(new Uri("/__probe/echo", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
