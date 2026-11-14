using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Api.Feedback;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// An anonymous host whose feedback goes to an in-memory issue tracker. Every request in a test host
/// comes from the same address, so the limit is high here and low in <see cref="FeedbackLimitTestApi"/>.
/// </summary>
public class FeedbackTestApi(PostgresTestServer postgres) : IAsyncLifetime
{
    public const int MaxLength = 100;

    public virtual int MaxPerWindow => 1000;

    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    public FakeFeedbackIssues Issues { get; } = new();

    public async ValueTask InitializeAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(GetType().Name, TestContext.Current.CancellationToken);

        _host = await TestApiHost.StartAsync(
            connectionString,
            developmentIdentityEnabled: false,
            settings: new Dictionary<string, string?>
            {
                [$"{FeedbackOptions.SectionName}:MaxPerWindow"] = MaxPerWindow.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [$"{FeedbackOptions.SectionName}:MaxLength"] = MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            configureServices: services => services.Replace(ServiceDescriptor.Singleton<IFeedbackIssues>(Issues)));
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

/// <summary>The same host with a limit of three messages per address.</summary>
public sealed class FeedbackLimitTestApi(PostgresTestServer postgres) : FeedbackTestApi(postgres)
{
    public override int MaxPerWindow => 3;
}

/// <summary>Keeps every issue it is asked to open; answers with <see cref="Next"/> when it is set.</summary>
public sealed class FakeFeedbackIssues : IFeedbackIssues
{
    private int _number;

    public ConcurrentQueue<FeedbackIssue> Created { get; } = new();

    public FeedbackIssueOutcome? Next { get; set; }

    public Task<FeedbackIssueResult> CreateAsync(FeedbackIssue issue, CancellationToken cancellationToken)
    {
        if (Next is { } outcome and not FeedbackIssueOutcome.Created)
        {
            return Task.FromResult(new FeedbackIssueResult(outcome));
        }

        Created.Enqueue(issue);
        return Task.FromResult(new FeedbackIssueResult(FeedbackIssueOutcome.Created, Interlocked.Increment(ref _number)));
    }
}
