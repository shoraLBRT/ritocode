using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The host with the development identity off and both providers configured, their addresses moved to
/// <c>github.test</c> and <c>google.test</c> by configuration as the end-to-end test moves them (#39),
/// and their token and profile endpoints answered by <see cref="FakeOAuthProvider"/> — the real OAuth handler, state, correlation
/// cookie and PKCE included, with nothing leaving the process.
/// </summary>
public sealed class SignInTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    public IServiceProvider Services => _host!.Services;

    public FakeOAuthProvider Provider { get; } = new();

    public async ValueTask InitializeAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(nameof(SignInTestApi), TestContext.Current.CancellationToken);

        _host = await TestApiHost.StartAsync(
            connectionString,
            developmentIdentityEnabled: false,
            new Dictionary<string, string?>
            {
                // The development settings send the return to Vite's origin; these tests read the path.
                ["Auth:SignIn:AppOrigin"] = string.Empty,
                ["Auth:GitHub:ClientId"] = "github-client",
                ["Auth:GitHub:ClientSecret"] = "github-secret",
                ["Auth:GitHub:AuthorizationEndpoint"] = "https://github.test/authorize",
                ["Auth:GitHub:TokenEndpoint"] = "https://github.test/token",
                ["Auth:GitHub:UserInformationEndpoint"] = "https://github.test/user",
                ["Auth:Google:ClientId"] = "google-client",
                ["Auth:Google:ClientSecret"] = "google-secret",
                ["Auth:Google:AuthorizationEndpoint"] = "https://google.test/authorize",
                ["Auth:Google:TokenEndpoint"] = "https://google.test/token",
                ["Auth:Google:UserInformationEndpoint"] = "https://google.test/user",
            },
            services =>
            {
                services.PostConfigure<OAuthOptions>("github", options => options.Backchannel = new HttpClient(Provider));
                services.PostConfigure<OAuthOptions>("google", options => options.Backchannel = new HttpClient(Provider));
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }
}

/// <summary>
/// Stands in for GitHub and Google behind the OAuth handler's backchannel. The authorisation code a
/// test sends back is the access token, and the token names the profile registered for it.
/// </summary>
public sealed class FakeOAuthProvider : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, (string User, string? Emails)> _people = new();

    /// <summary>A GitHub account: <c>/user</c>, and <c>/user/emails</c> with one primary address.</summary>
    public string GitHubPerson(long id, string login, string email, bool verified)
    {
        var code = Guid.NewGuid().ToString("N");
        _people[code] = (
            JsonSerializer.Serialize(new { id, login, email = (string?)null }),
            JsonSerializer.Serialize(new object[]
            {
                new { email, primary = true, verified },
                new { email = $"other-{email}", primary = false, verified = true },
            }));
        return code;
    }

    /// <summary>A Google account, as its OpenID Connect userinfo answers.</summary>
    public string GooglePerson(string sub, string email, bool verified)
    {
        var code = Guid.NewGuid().ToString("N");
        _people[code] = (JsonSerializer.Serialize(new { sub, email, email_verified = verified }), null);
        return code;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path == "/token")
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var fields = form.Split('&').Select(pair => pair.Split('=')).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));

            // The handler sends the PKCE verifier with the code; a provider that supports PKCE checks it.
            if (!fields.ContainsKey("code_verifier") || !_people.ContainsKey(fields["code"]))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            return Json(JsonSerializer.Serialize(new { access_token = fields["code"], token_type = "bearer" }));
        }

        var token = request.Headers.Authorization?.Parameter ?? string.Empty;
        if (!_people.TryGetValue(token, out var person))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return path switch
        {
            "/user" => Json(person.User),
            "/user/emails" when person.Emails is not null => Json(person.Emails),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
