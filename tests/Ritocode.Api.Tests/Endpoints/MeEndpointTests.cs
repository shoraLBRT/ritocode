using System.Net;
using System.Text.Json;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary><c>GET /api/v1/me</c> under the development identity: the seeded user.</summary>
public sealed class MeEndpointTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public async Task TheCaller_IsTheSeededDevelopmentIdentity()
    {
        var seeded = new DevelopmentIdentityOptions();

        var response = await api.Client.GetAsync(new Uri("/api/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(seeded.UserId.ToString(), body.RootElement.GetProperty("id").GetString());
        Assert.Equal(seeded.Username, body.RootElement.GetProperty("username").GetString());
    }
}

/// <summary>Signed out, <c>/me</c> is a 401 in the error body — how the frontend learns it is signed out.</summary>
public sealed class AnonymousMeEndpointTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ASignedOutCaller_IsUnauthenticated()
    {
        var response = await api.Client.GetAsync(new Uri("/api/v1/me", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("unauthenticated", body.RootElement.GetProperty("code").GetString());
    }
}
