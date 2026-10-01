using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The parts of the security baseline (docs/SPEC.md §10.1, #35) that belong to the host rather than to
/// one endpoint: the response headers the API owns, the cap on a request body, and the admin policy on
/// every admin address. CSRF on every state-changing endpoint is swept in <see cref="SessionTests"/>.
/// </summary>
public sealed class SecurityBaselineTests(TestApi api) : IClassFixture<TestApi>
{
    [Theory]
    [InlineData("/api/v1/me", HttpStatusCode.OK)]
    [InlineData("/api/v1/no-such-thing", HttpStatusCode.NotFound)]
    [InlineData("/__probe/unhandled", HttpStatusCode.InternalServerError)]
    [InlineData("/health/live", HttpStatusCode.OK)]
    public async Task EveryResponse_CarriesTheApisSecurityHeaders_ErrorsIncluded(string path, HttpStatusCode status)
    {
        using var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Header(response, "Content-Security-Policy"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        // An endpoint's own policy stands — health checks and the error handler say "no-store, no-cache" — but it never allows storing.
        Assert.Contains("no-store", Header(response, "Cache-Control") ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void TheServer_ReadsNoBodyLargerThanTheCap()
    {
        var kestrel = api.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.Equal(64 * 1024, kestrel.Limits.MaxRequestBodySize);
    }

    [Fact]
    public void EveryAdminAddress_IsBehindTheAdminPolicy()
    {
        var admin = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1/admin", StringComparison.Ordinal) == true)
            .ToList();

        // Signals, resolving one, users and attempts (SPEC §9.3).
        Assert.True(admin.Count >= 4, $"Only {admin.Count} admin endpoints were found.");
        Assert.All(admin, endpoint => Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == AdminPolicy.Name));
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : null;
}
