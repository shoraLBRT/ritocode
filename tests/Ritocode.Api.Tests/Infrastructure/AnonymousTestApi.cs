using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The same host with the development identity switched off, so every request arrives anonymous.
/// </summary>
/// <remarks>
/// This is what a deployment looks like before a real session provider exists, and what it looks
/// like the moment the seeded one is replaced. It is also the only way to prove anything about the
/// authorisation policy: with the development identity on, every request passes and a lost
/// <c>AllowAnonymous</c> or a missing fallback policy looks identical to a correct host.
/// </remarks>
public sealed class AnonymousTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync(
            nameof(AnonymousTestApi),
            TestContext.Current.CancellationToken);

        _host = await TestApiHost.StartAsync(ConnectionString, developmentIdentityEnabled: false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }
}
