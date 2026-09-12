using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The host as a developer runs it: the seeded development identity from ADR 0005 authenticating
/// every request.
/// </summary>
/// <remarks>
/// One instance per test class, on a migrated database of its own from
/// <see cref="PostgresTestServer"/>. Each fixture names its own database, which matters now that
/// the host writes a row on startup: <see cref="AnonymousTestApi"/> asserts that it writes none.
/// </remarks>
public sealed class TestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    /// <summary>The host's composed container. Resolve scoped services from a scope of your own.</summary>
    public IServiceProvider Services => _host!.Services;

    /// <summary>Connection string of this class's database, for tests that assert against it directly.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync(nameof(TestApi), TestContext.Current.CancellationToken);
        _host = await TestApiHost.StartAsync(ConnectionString, developmentIdentityEnabled: true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }
}
