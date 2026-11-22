using System.Net;
using Ritocode.Api.Tests.Infrastructure;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The edge's question before the log viewer (#295, ADR 0020): an admin's session is let through with an empty
/// 2xx, which is all Caddy's <c>forward_auth</c> reads. A non-admin's and a signed-out caller's refusals are in
/// <see cref="NonAdminEndpointTests"/> and <see cref="AnonymousAdminEndpointTests"/>.
/// </summary>
public sealed class AdminAccessEndpointTests(AdminContentTestApi api) : IClassFixture<AdminContentTestApi>
{
    [Fact]
    public async Task AnAdmin_IsLetThrough_WithNothingInTheBody()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/admin/access", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheEdgesForwardedRequest_IsAnsweredTheSame()
    {
        // forward_auth sends the visitor's own headers plus where the request was going.
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/admin/access", UriKind.Relative));
        request.Headers.Add("X-Forwarded-Method", "GET");
        request.Headers.Add("X-Forwarded-Uri", "/admin/logs/");

        var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
