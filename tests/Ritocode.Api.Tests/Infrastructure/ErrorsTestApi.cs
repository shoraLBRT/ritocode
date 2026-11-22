using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Errors;
using Ritocode.Modules.Errors.Alerts;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The API for the error records (#294): signed in as the development identity, who is an admin here, with
/// the Telegram alerts caught by <see cref="Alerts"/> instead of queued.
/// </summary>
public class ErrorsTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    public const int OccurrencesPerGroup = 3;

    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    public IServiceProvider Services => _host!.Services;

    public RecordingErrorAlerts Alerts { get; } = new();

    protected virtual int MaxPerAddress => 1000;

    protected virtual bool Admin => true;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(GetType().Name, TestContext.Current.CancellationToken);

        _host = await TestApiHost.StartAsync(
            connectionString,
            developmentIdentityEnabled: true,
            settings: new Dictionary<string, string?>
            {
                ["Users:Admin:Emails:0"] = Admin ? "developer@ritocode.local" : "admin@example.test",
                [$"{ErrorsOptions.SectionName}:OccurrencesPerGroup"] = OccurrencesPerGroup.ToString(CultureInfo.InvariantCulture),
                [$"{ErrorsOptions.SectionName}:Browser:MaxPerAddress"] = MaxPerAddress.ToString(CultureInfo.InvariantCulture),
                [$"{ErrorsOptions.SectionName}:Browser:MaxTotal"] = "100000",
            },
            configureServices: services => services.Replace(ServiceDescriptor.Singleton<IErrorAlerts>(Alerts)));
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>As <see cref="ErrorsTestApi"/>, with room for two browser reports per address.</summary>
public sealed class RateLimitedErrorsTestApi(PostgresTestServer postgres) : ErrorsTestApi(postgres)
{
    protected override int MaxPerAddress => 2;
}

public sealed class RecordingErrorAlerts : IErrorAlerts
{
    public ConcurrentQueue<ErrorAlert> Raised { get; } = new();

    public void Raise(ErrorAlert alert) => Raised.Enqueue(alert);
}
