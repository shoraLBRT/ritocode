using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.Session;
using Ritocode.Modules.Auth.SignIn;
using Ritocode.Modules.Users.Persistence;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// Sign-in with GitHub and Google (#7, docs/SPEC.md §6.1) through the real OAuth handler, against a
/// fake provider: the first sign-in creates a user, a second provider with the same verified address
/// reaches it, an unverified address never links, and the return address is only ever a local path.
/// </summary>
public sealed class SignInTests(SignInTestApi api) : IClassFixture<SignInTestApi>
{
    [Fact]
    public async Task TheFirstSignIn_CreatesAUser_StartsASession_AndReturnsToThePath()
    {
        var code = api.Provider.GitHubPerson(1001, "Ada-Lovelace", "ada@example.test", verified: true);

        var result = await SignInAsync("github", code, "/tasks/flower-shop-daily-revenue");

        Assert.Equal("/tasks/flower-shop-daily-revenue", result.Location);
        var me = await MeAsync(result.Session!);
        Assert.Equal("ada-lovelace", me.GetProperty("username").GetString());

        var accounts = await LinkedAccountsAsync(Guid.Parse(me.GetProperty("id").GetString()!));
        var account = Assert.Single(accounts);
        Assert.Equal(IdentityProvider.GitHub, account.Provider);
        Assert.Equal("1001", account.ProviderUserId);
    }

    [Fact]
    public async Task TheSecondProvider_WithTheSameVerifiedAddress_ReachesTheSameUser()
    {
        var github = await SignInAsync("github", api.Provider.GitHubPerson(1002, "brook", "Brook@Example.test", verified: true));
        var google = await SignInAsync("google", api.Provider.GooglePerson("g-1002", "brook@example.test", verified: true));

        var first = (await MeAsync(github.Session!)).GetProperty("id").GetString();
        var second = (await MeAsync(google.Session!)).GetProperty("id").GetString();
        Assert.Equal(first, second);

        var accounts = await LinkedAccountsAsync(Guid.Parse(first!));
        Assert.Equal([IdentityProvider.GitHub, IdentityProvider.Google], accounts.Select(account => account.Provider).Order());
    }

    [Fact]
    public async Task AnUnverifiedAddress_NeverLinks_NorCreatesAUser()
    {
        var github = await SignInAsync("github", api.Provider.GitHubPerson(1003, "cleo", "cleo@example.test", verified: true));
        var userId = Guid.Parse((await MeAsync(github.Session!)).GetProperty("id").GetString()!);

        var google = await SignInAsync("google", api.Provider.GooglePerson("g-1003", "cleo@example.test", verified: false), "/tasks");
        var stranger = await SignInAsync("github", api.Provider.GitHubPerson(1004, "dara", "dara@example.test", verified: false));

        Assert.Null(google.Session);
        Assert.Equal("/tasks?signInError=email_unverified", google.Location);
        Assert.Null(stranger.Session);
        Assert.Single(await LinkedAccountsAsync(userId));

        await using var scope = api.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .AnyAsync(user => user.Email == "dara@example.test", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SigningInAgain_IsTheSameUser_AndKeepsTheRenamedLogin()
    {
        var first = await SignInAsync("github", api.Provider.GitHubPerson(1005, "eli", "eli@example.test", verified: true));

        // Renamed at GitHub, and the address changed too: the immutable id is what matches.
        var again = await SignInAsync("github", api.Provider.GitHubPerson(1005, "eli-renamed", "eli.new@example.test", verified: true));

        var userId = (await MeAsync(first.Session!)).GetProperty("id").GetString();
        Assert.Equal(userId, (await MeAsync(again.Session!)).GetProperty("id").GetString());
        Assert.Equal("eli-renamed", Assert.Single(await LinkedAccountsAsync(Guid.Parse(userId!))).ProviderLogin);
    }

    [Theory]
    [InlineData("https://evil.test/")]
    [InlineData("//evil.test/")]
    [InlineData("/\\evil.test")]
    [InlineData("tasks")]
    public async Task AReturnAddress_ThatIsNotALocalPath_IsRefused(string returnUrl)
    {
        using var response = await api.Client.GetAsync(
            QueryHelpers.AddQueryString("/auth/login/github", "returnUrl", returnUrl),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(body.GetProperty("errors").TryGetProperty("returnUrl", out _));
    }

    [Fact]
    public async Task AProviderNotOffered_Is404()
    {
        using var response = await api.Client.GetAsync("/auth/login/facebook", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheLoginRedirect_CarriesStateAndPkce()
    {
        // Not behind a proxy, a forwarded scheme is anyone's to claim and is ignored.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/login/google?returnUrl=/progress");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("https://google.test/authorize", response.Headers.Location.GetLeftPart(UriPartial.Path));
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("http://localhost/auth/callback/google", query["redirect_uri"]);
    }

    [Fact]
    public async Task ACallbackWithoutItsCorrelationCookie_SignsNobodyIn()
    {
        var code = api.Provider.GitHubPerson(1006, "fay", "fay@example.test", verified: true);
        var (state, _) = await BeginAsync("github", "/tasks");

        using var response = await api.Client.GetAsync($"/auth/callback/github?code={code}&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("signInError=provider_failed", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Null(SessionFrom(response));
    }

    private async Task<(string State, string Cookies)> BeginAsync(string provider, string returnUrl)
    {
        using var login = await api.Client.GetAsync(
            QueryHelpers.AddQueryString($"/auth/login/{provider}", "returnUrl", returnUrl),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var state = QueryHelpers.ParseQuery(login.Headers.Location!.Query)["state"].ToString();
        var cookies = string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));

        return (state, cookies);
    }

    /// <summary>The browser's round trip: login, the provider sending it back with a code, the callback.</summary>
    private async Task<SignInResult> SignInAsync(string provider, string code, string returnUrl = "/")
    {
        var (state, cookies) = await BeginAsync(provider, returnUrl);

        using var callback = new HttpRequestMessage(HttpMethod.Get, $"/auth/callback/{provider}?code={code}&state={Uri.EscapeDataString(state)}");
        callback.Headers.Add("Cookie", cookies);
        using var response = await api.Client.SendAsync(callback, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return new SignInResult(response.Headers.Location!.OriginalString, SessionFrom(response));
    }

    private static string? SessionFrom(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(cookie => cookie.Split(';')[0])
                .Where(pair => pair.StartsWith(SessionCookies.SessionName + "=", StringComparison.Ordinal))
                .Select(pair => pair[(SessionCookies.SessionName.Length + 1)..])
                .FirstOrDefault(token => token.Length > 0)
            : null;

    private async Task<JsonElement> MeAsync(string session)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Add("Cookie", $"{SessionCookies.SessionName}={session}");
        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<List<LinkedAccount>> LinkedAccountsAsync(Guid userId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AuthDbContext>().LinkedAccounts.AsNoTracking()
            .Where(account => account.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record SignInResult(string Location, string? Session);
}

/// <summary>The return address and the username a provider's hint becomes, without a host.</summary>
public sealed class SignInRuleTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/tasks/some-task?x=1#y", true)]
    [InlineData("//evil.test", false)]
    [InlineData("/\\evil.test", false)]
    [InlineData("https://evil.test", false)]
    [InlineData("tasks", false)]
    [InlineData("", false)]
    [InlineData("/tasks\n", false)]
    public void OnlyALocalPath_IsLocal(string value, bool local) =>
        Assert.Equal(local, ReturnUrl.IsLocal(value));

    [Fact]
    public void AConfiguredAppOrigin_PrefixesThePath_AndAnErrorGoesInTheQuery() =>
        Assert.Equal(
            "http://localhost:5173/tasks/x?signInError=email_unverified#top",
            ReturnUrl.Resolve("http://localhost:5173/", "/tasks/x#top", "email_unverified"));
}
