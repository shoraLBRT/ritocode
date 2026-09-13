using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Ingest;
using Ritocode.Modules.Problems.Packaging;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The host as a developer runs it, with a real MinIO behind it and a real package to publish — the
/// whole path a workspace is created along, with nothing in it faked.
/// </summary>
/// <remarks>
/// Versions are published per test rather than once for the class. Every request here is the same
/// development identity, and opening a version it already has a workspace on answers 200 instead of
/// 201, so a shared version would make each test's answer depend on which ran first.
/// </remarks>
public sealed class WorkspaceApi(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    /// <summary>The committed package every version here is published from.</summary>
    public const string Slug = "split-the-invoice";

    private TestApiHost? _host;

    public HttpClient Client => _host!.Client;

    /// <summary>The host's composed container. Resolve scoped services from a scope of your own.</summary>
    public IServiceProvider Services => _host!.Services;

    public ProblemPackage Package { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var connectionString = await postgres.CreateDatabaseAsync(nameof(WorkspaceApi), cancellationToken);
        var storage = await minio.CreateBucketsAsync(nameof(WorkspaceApi), cancellationToken);

        _host = await TestApiHost.StartAsync(connectionString, developmentIdentityEnabled: true, storage);

        var loaded = ProblemPackageLoader.Load(Path.Combine(AppContext.BaseDirectory, "content", "problems", Slug));
        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? string.Empty : loaded.Error.Message);
        Package = loaded.Value;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    /// <summary>Publishes the package again, through real ingest, as a version nobody has opened.</summary>
    public async Task<IngestedProblemVersion> PublishVersionAsync()
    {
        await using var scope = Services.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<IProblemIngest>()
            .IngestAsync(Package, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A draft revision of the package's problem. Written directly, because nothing in the slice can
    /// produce one: ingest publishes as it goes.
    /// </summary>
    public async Task<Guid> AddDraftVersionAsync()
    {
        var published = await PublishVersionAsync();

        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProblemsDbContext>();

        var draft = ProblemVersion.Create(
            published.ProblemId,
            published.Version + 1,
            Package.ValidatorConfigJson,
            Package.Manifest.Workspace.Root,
            DateTimeOffset.UtcNow);

        context.ProblemVersions.Add(draft);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return draft.Id;
    }
}
