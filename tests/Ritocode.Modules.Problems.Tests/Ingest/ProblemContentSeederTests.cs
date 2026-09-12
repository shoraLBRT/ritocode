using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Problems.Ingest;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Problems.Tests.Ingest;

/// <summary>
/// The seeder is the first caller of ingest, and the only one until issue #42 brings real content.
/// What it has to get right is not the ingest — that is tested next door — but when it declines to
/// run one.
/// </summary>
public sealed class ProblemContentSeederTests(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    private IObjectStore _objectStore = null!;

    public async ValueTask InitializeAsync()
    {
        var storage = await minio.CreateBucketsAsync(
            nameof(ProblemContentSeederTests),
            TestContext.Current.CancellationToken);

        _objectStore = minio.CreateStore(storage);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Seeding_PublishesEveryPackageInTheContentDirectory()
    {
        var database = await NewDatabaseAsync();

        await RunAsync(database, seedOnStartup: true);

        await using var context = database.CreateContext();
        var problems = await context.Problems
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Every package, not the first one: since #42 the directory holds the three catalog problems
        // as well as the reference package, and a seeder that stopped early would look like success.
        Assert.Equal(ContentPackages.AllSlugs, [.. problems.Select(problem => problem.Slug).Order(StringComparer.Ordinal)]);

        foreach (var problem in problems)
        {
            Assert.True(
                await context.ProblemVersions
                    .AsNoTracking()
                    .AnyAsync(
                        v => v.ProblemId == problem.Id && v.PublishedAt != null,
                        TestContext.Current.CancellationToken),
                $"{problem.Slug} has no published version.");
        }
    }

    [Fact]
    public async Task Seeding_IsOffUnlessConfigurationTurnsItOn()
    {
        var database = await NewDatabaseAsync();

        await RunAsync(database, seedOnStartup: false);

        await using var context = database.CreateContext();

        // Off by default matters beyond tidiness: a host that seeds unasked needs object storage to
        // start, which is the property issue #37 spent a session buying back.
        Assert.Empty(await context.Problems.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedingTwice_LeavesTheVersionNumberAlone()
    {
        var database = await NewDatabaseAsync();

        await RunAsync(database, seedOnStartup: true);
        await RunAsync(database, seedOnStartup: true);

        await using var context = database.CreateContext();
        var versions = await context.ProblemVersions
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // A restart is not a revision. Without the skip, the catalog would report a version number
        // that counts how often the host was started.
        Assert.Equal(ContentPackages.AllSlugs.Count, versions.Count);
        Assert.All(versions, version => Assert.Equal(1, version.Version));
    }

    [Fact]
    public async Task AMissingContentDirectory_IsReportedRatherThanFatal()
    {
        var database = await NewDatabaseAsync();

        await RunAsync(database, seedOnStartup: true, directory: "no-such-directory");

        await using var context = database.CreateContext();
        Assert.Empty(await context.Problems.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
    }

    private Task<ProblemsDatabase> NewDatabaseAsync() =>
        ProblemsDatabase.CreateAsync(postgres, nameof(ProblemContentSeederTests));

    private async Task RunAsync(
        ProblemsDatabase database,
        bool seedOnStartup,
        string directory = "content/problems")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => database.CreateContext());
        services.AddScoped<IProblemIngest>(provider => new ProblemIngestService(
            provider.GetRequiredService<ProblemsDbContext>(),
            _objectStore,
            TimeProvider.System));

        await using var provider = services.BuildServiceProvider();

        using var seeder = new ProblemContentSeeder(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProblemContentOptions { SeedOnStartup = seedOnStartup, Directory = directory }),
            new TestEnvironment(AppContext.BaseDirectory),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<ProblemContentSeeder>());

        await seeder.StartAsync(TestContext.Current.CancellationToken);

        // StartAsync only hands the work off; the assertions are about what the work did.
        await seeder.ExecuteTask!;
    }

    /// <summary>
    /// Content root pointing at the test output, where the project file copies the real
    /// <c>content/</c> tree — so the seeder reads the committed packages, not a fixture.
    /// </summary>
    private sealed class TestEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(ProblemContentSeederTests);

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
