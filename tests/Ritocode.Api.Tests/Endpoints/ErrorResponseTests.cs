using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Api.Setup;
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

    public static TheoryData<string, byte[]> UnreadableBodies => new()
    {
        { "malformed-json", Encoding.UTF8.GetBytes("""{"title":"ok","count":""") },
        // "привет" in cp1251 inside an otherwise well-formed body: not UTF-8, so not JSON.
        { "cp1251-body", [.. Encoding.UTF8.GetBytes("{\"title\":\""), 0xEF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2, .. Encoding.UTF8.GetBytes("\",\"count\":1}")] },
    };

    [Theory]
    [MemberData(nameof(UnreadableBodies))]
    public async Task UnreadableJsonBody_Is400RequestInvalid_LoggedAsTheClientsError(string requestId, byte[] body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/__probe/echo");
        request.Headers.Add(RequestId.HeaderName, requestId);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = await ReadJsonAsync(response);
        Assert.Equal("request_invalid", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(requestId, document.RootElement.GetProperty("requestId").GetString());

        var handlerLine = Assert.Single(
            api.Services.GetRequiredService<CapturedLogs>().Lines,
            line => line.Category == typeof(AppExceptionHandler).FullName
                && line.Scope.TryGetValue(RequestId.LogPropertyName, out var id) && Equals(id, requestId));
        Assert.Equal(LogLevel.Information, handlerLine.Level);
    }

    [Fact]
    public async Task ABodyWithoutAContentType_Is415UnsupportedMediaType()
    {
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes("""{"title":"ok","count":3}"""));

        var response = await api.Client.PostAsync(new Uri("/__probe/echo", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        Assert.Equal("unsupported_media_type", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AQueryValueOfTheWrongType_Is400RequestInvalid()
    {
        var response = await api.Client.GetAsync(new Uri("/__probe/page?page=abc", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        Assert.Equal("request_invalid", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}

/// <summary>
/// Outside Development the framework answers a request it cannot bind with a bare 400 and no body;
/// the host makes it throw to the exception handler everywhere, so production gets the unified error.
/// </summary>
public sealed class BadRequestSettingsTests
{
    [Fact]
    public void InProduction_ABindingFailureStillReachesTheExceptionHandler()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.AddRitocodeApi();

        using var services = builder.Services.BuildServiceProvider();

        Assert.True(services.GetRequiredService<IOptions<RouteHandlerOptions>>().Value.ThrowOnBadRequest);
    }
}
