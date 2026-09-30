using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.Session;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The session cookie of ADR 0012 (#6), on a host with the development identity off, as production
/// runs: a session authenticates, a state-changing request repeats its CSRF token, signing out ends
/// it, and a session that ended or expired authenticates nothing.
/// </summary>
public sealed class SessionTests(AnonymousTestApi api) : IClassFixture<AnonymousTestApi>
{
    [Fact]
    public async Task WithoutASession_AProtectedEndpointIs401_AndWithOneItAnswersTheUser()
    {
        var user = await CreateUserAsync("ada");

        using (var anonymous = await SendAsync(HttpMethod.Get, "/api/v1/me", session: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        var session = await StartAsync(user);
        using var signedIn = await SendAsync(HttpMethod.Get, "/api/v1/me", session);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        var me = await signedIn.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(user.ToString(), me.GetProperty("id").GetString());
        Assert.Equal("ada", me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task AStateChangingRequest_WithoutTheCsrfToken_IsRefused()
    {
        var session = await StartAsync(await CreateUserAsync("brook"));
        var body = new { task = "no-such-task" };

        using var missing = await SendAsync(HttpMethod.Post, "/api/v1/attempts", session, csrf: null, body);
        using var wrong = await SendAsync(HttpMethod.Post, "/api/v1/attempts", session, csrf: "not-the-token", body);
        using var right = await SendAsync(HttpMethod.Post, "/api/v1/attempts", session, csrf: session.CsrfToken, body);

        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(CsrfProtectionMiddleware.InvalidCode, await CodeAsync(missing));
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);

        // Through to the endpoint, which answers for the task: there is no content in this host.
        Assert.Equal(HttpStatusCode.NotFound, right.StatusCode);
        Assert.Equal("task_not_found", await CodeAsync(right));
    }

    [Fact]
    public async Task SigningOut_EndsTheSession_AndClearsItsCookies()
    {
        var session = await StartAsync(await CreateUserAsync("cleo"));

        using (var forged = await SendAsync(HttpMethod.Post, "/auth/logout", session, csrf: null))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        }

        using (var logout = await SendAsync(HttpMethod.Post, "/auth/logout", session, csrf: session.CsrfToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            var cleared = logout.Headers.GetValues("Set-Cookie").ToList();
            Assert.Contains(cleared, cookie => cookie.StartsWith(SessionCookies.SessionName + "=;", StringComparison.Ordinal));
            Assert.Contains(cleared, cookie => cookie.StartsWith(SessionCookies.CsrfName + "=;", StringComparison.Ordinal));
        }

        using var after = await SendAsync(HttpMethod.Get, "/api/v1/me", session);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);

        await using var scope = api.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Sessions
            .SingleAsync(row => row.TokenHash == UserSession.Hash(session.Token), TestContext.Current.CancellationToken);
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task SigningOut_WithoutASession_IsANoOp()
    {
        using var logout = await SendAsync(HttpMethod.Post, "/auth/logout", session: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task AnExpiredSession_OrAnUnknownToken_AuthenticatesNothing()
    {
        var user = await CreateUserAsync("dara");
        var (expired, token) = UserSession.Start(user, DateTimeOffset.UtcNow.AddDays(-31), TimeSpan.FromDays(30));

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            context.Sessions.Add(expired);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var old = await SendAsync(HttpMethod.Get, "/api/v1/me", new IssuedSession(token, expired.CsrfToken, expired.ExpiresAt));
        using var unknown = await SendAsync(HttpMethod.Get, "/api/v1/me", new IssuedSession("made-up", "x", DateTimeOffset.UtcNow.AddDays(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task TheTokenIsNeverStored_OnlyItsHash()
    {
        var session = await StartAsync(await CreateUserAsync("eli"));

        await using var scope = api.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Sessions.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(rows, row => row.TokenHash == session.Token || row.CsrfToken == session.Token);
        Assert.Contains(rows, row => row.TokenHash == UserSession.Hash(session.Token));
    }

    private async Task<Guid> CreateUserAsync(string username)
    {
        var id = Guid.CreateVersion7();

        await using var scope = api.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Users.Add(new User { Id = id, Email = $"{username}@example.test", Username = username, CreatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<IssuedSession> StartAsync(Guid user)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionIssuer>().StartAsync(user, TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, IssuedSession? session, string? csrf = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (session is not null)
        {
            request.Headers.Add("Cookie", $"{SessionCookies.SessionName}={session.Token}");
        }

        if (csrf is not null)
        {
            request.Headers.Add(CsrfProtectionMiddleware.HeaderName, csrf);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString();
}

/// <summary>The cookies a session is written as: prefixed, secure, the token out of scripts' reach.</summary>
public sealed class SessionCookieTests
{
    [Fact]
    public void TheSessionCookie_IsHttpOnlyAndLax_AndTheCsrfCookie_IsReadableAndStrict()
    {
        var context = new DefaultHttpContext();
        var expires = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        SessionCookies.Write(context.Response, new IssuedSession("the-token", "the-csrf", expires));

        var cookies = context.Response.Headers.SetCookie.Select(cookie => cookie!.ToLowerInvariant()).ToList();
        var session = Assert.Single(cookies, cookie => cookie.StartsWith("__host-ritocode-session=the-token", StringComparison.Ordinal));
        var csrf = Assert.Single(cookies, cookie => cookie.StartsWith("__host-ritocode-csrf=the-csrf", StringComparison.Ordinal));

        Assert.Contains("path=/", session, StringComparison.Ordinal);
        Assert.Contains("secure", session, StringComparison.Ordinal);
        Assert.Contains("httponly", session, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", session, StringComparison.Ordinal);
        Assert.Contains("expires=sun, 01 nov 2026", session, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", session, StringComparison.Ordinal);

        Assert.Contains("secure", csrf, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", csrf, StringComparison.Ordinal);
        Assert.DoesNotContain("httponly", csrf, StringComparison.Ordinal);
    }
}
