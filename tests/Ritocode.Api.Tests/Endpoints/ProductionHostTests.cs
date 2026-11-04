using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>
/// The host as the production Compose file runs it (#135): behind the proxy, which terminates TLS and
/// forwards the scheme, and with the data-protection key ring on a volume.
/// </summary>
public sealed class ProductionHostTests(ProxiedTestApi api) : IClassFixture<ProxiedTestApi>
{
    [Fact]
    public async Task BehindTheProxy_TheSignInCallback_IsBuiltWithTheForwardedScheme()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/login/github?returnUrl=/tasks");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("https://localhost/auth/callback/github", query["redirect_uri"]);
    }

    [Fact]
    public async Task WithNoAddressesConfigured_SignInGoesToTheProvidersOwn()
    {
        using var response = await api.Client.GetAsync("/auth/login/github?returnUrl=/tasks", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://github.com/login/oauth/authorize", response.Headers.Location!.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public void TheKeyRing_IsKeptInTheConfiguredDirectory()
    {
        var protector = api.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("probe");

        Assert.Equal("kept", protector.Unprotect(protector.Protect("kept")));
        Assert.NotEmpty(Directory.GetFiles(api.KeysDirectory, "*.xml"));
    }
}

/// <summary>The host with <c>Api:BehindProxy</c> on, GitHub configured, and the key ring in a directory of its own.</summary>
public sealed class ProxiedTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    public IServiceProvider Services => _host!.Services;

    public string KeysDirectory { get; } = Path.Combine(Path.GetTempPath(), "ritocode-keys-" + Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(nameof(ProxiedTestApi), TestContext.Current.CancellationToken);

        _host = await TestApiHost.StartAsync(
            connectionString,
            developmentIdentityEnabled: false,
            new Dictionary<string, string?>
            {
                ["Api:BehindProxy"] = "true",
                ["Api:DataProtectionKeysDirectory"] = KeysDirectory,
                ["Auth:SignIn:AppOrigin"] = string.Empty,
                ["Auth:GitHub:ClientId"] = "github-client",
                ["Auth:GitHub:ClientSecret"] = "github-secret",
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        if (Directory.Exists(KeysDirectory))
        {
            Directory.Delete(KeysDirectory, recursive: true);
        }
    }
}
