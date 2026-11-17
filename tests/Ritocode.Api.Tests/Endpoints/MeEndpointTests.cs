using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.Session;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary><c>GET /api/v1/me</c> under the development identity: the seeded user, linked to no provider.</summary>
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
        Assert.Equal(TimeSpan.Zero, body.RootElement.GetProperty("createdAt").GetDateTimeOffset().Offset);
        Assert.Empty(body.RootElement.GetProperty("providers").EnumerateArray());
    }
}

/// <summary>
/// <c>/me</c> for a user behind a session cookie: the join date as stored, in UTC, and the providers
/// linked to the account in the order the sign-in addresses list them.
/// </summary>
public sealed class SignedInMeEndpointTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task ANewUser_ReportsTheJoinDate_AndTheProviderItSignedInWith()
    {
        var joined = new DateTimeOffset(2026, 3, 14, 12, 26, 53, TimeSpan.FromHours(3));
        var user = await UserAsync("nova", joined, IdentityProvider.GitHub);

        using var me = await MeAsync(user);

        Assert.Equal(user.ToString(), me.RootElement.GetProperty("id").GetString());
        Assert.Equal("nova", me.RootElement.GetProperty("username").GetString());
        var createdAt = me.RootElement.GetProperty("createdAt").GetDateTimeOffset();
        Assert.Equal(joined, createdAt);
        Assert.Equal(TimeSpan.Zero, createdAt.Offset);
        Assert.Equal(["github"], me.RootElement.GetProperty("providers").EnumerateArray().Select(provider => provider.GetString()));
    }

    [Fact]
    public async Task AUserWithTwoProviders_ListsBoth_GitHubFirst()
    {
        // Linked Google first, so the order in the answer is the contract's, not the insertion's.
        var user = await UserAsync("orion", DateTimeOffset.UtcNow, IdentityProvider.Google, IdentityProvider.GitHub);

        using var me = await MeAsync(user);

        Assert.Equal(["github", "google"], me.RootElement.GetProperty("providers").EnumerateArray().Select(provider => provider.GetString()));
        Assert.False(me.RootElement.TryGetProperty("email", out _));
    }

    private async Task<Guid> UserAsync(string username, DateTimeOffset createdAt, params IdentityProvider[] providers)
    {
        await using var scope = api.Services.CreateAsyncScope();

        var user = User.Create($"{username}@example.test", username, createdAt);
        var users = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        users.Users.Add(user);
        await users.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        foreach (var provider in providers)
        {
            auth.LinkedAccounts.Add(LinkedAccount.Create(user.Id, provider, $"{provider}-{user.Id:N}", username, DateTimeOffset.UtcNow));
            await auth.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return user.Id;
    }

    private async Task<JsonDocument> MeAsync(Guid userId)
    {
        IssuedSession session;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            session = await scope.ServiceProvider.GetRequiredService<ISessionIssuer>().StartAsync(userId, TestContext.Current.CancellationToken);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add("Cookie", $"{SessionCookies.SessionName}={session.Token}");
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
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
