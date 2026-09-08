using System.Formats.Tar;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Ingest;
using Ritocode.Modules.Problems.Packaging;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Problems.Tests.Ingest;

/// <summary>
/// Ingest against a real PostgreSQL and a real MinIO — the first code in the project that writes to
/// either. Nothing is faked, because what is being asserted is that a published version and the
/// object it points at end up consistent with each other.
/// </summary>
public sealed class ProblemIngestServiceTests(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    private static readonly DateTimeOffset IngestedAt = new(2026, 9, 8, 10, 30, 0, TimeSpan.Zero);

    private IObjectStore _objectStore = null!;

    public async ValueTask InitializeAsync()
    {
        var storage = await minio.CreateBucketsAsync(
            nameof(ProblemIngestServiceTests),
            TestContext.Current.CancellationToken);

        _objectStore = minio.CreateStore(storage);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Ingest_PublishesAVersionWhoseBundleIsInTheStore()
    {
        var database = await NewDatabaseAsync();
        var ingested = await IngestExampleAsync(database);

        await using var context = database.CreateContext();
        var version = await context.ProblemVersions
            .AsNoTracking()
            .SingleAsync(v => v.Id == ingested.ProblemVersionId, TestContext.Current.CancellationToken);

        Assert.Equal(1, version.Version);
        Assert.Equal(IngestedAt, version.PublishedAt);
        Assert.Equal(StorageKeys.ProblemBundle(version.Id), version.SnapshotReference);
        Assert.Equal(StorageRole.ProblemBundles, version.SnapshotReference.Role);

        // The reference read back from the row is what resolves the object, per
        // docs/STORAGE_LAYOUT.md rule 3 — not a key rebuilt from the id at read time.
        using var bundle = new MemoryStream();
        Assert.True(await _objectStore.GetAsync(version.SnapshotReference, bundle, TestContext.Current.CancellationToken));
        Assert.Contains("problem.yaml", await EntryNamesAsync(bundle), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Ingest_CopiesTheManifestMetadataOntoTheProblem()
    {
        var database = await NewDatabaseAsync();
        var ingested = await IngestExampleAsync(database);

        await using var context = database.CreateContext();
        var problem = await context.Problems
            .AsNoTracking()
            .SingleAsync(p => p.Id == ingested.ProblemId, TestContext.Current.CancellationToken);

        Assert.Equal(ExamplePackage.Slug, problem.Slug);
        Assert.Equal("Untangle the order total calculator", problem.Title);
        Assert.Equal(Difficulty.Medium, problem.Difficulty);
        Assert.Equal(["refactoring", "code-quality"], problem.Tags);
        Assert.Contains("order", problem.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ingest_StoresTheCanonicalValidatorConfig()
    {
        var database = await NewDatabaseAsync();
        var package = LoadExample();
        var ingested = await IngestAsync(database, package);

        await using var context = database.CreateContext();
        var version = await context.ProblemVersions
            .AsNoTracking()
            .SingleAsync(v => v.Id == ingested.ProblemVersionId, TestContext.Current.CancellationToken);

        Assert.Equal(package.ValidatorConfigJson, version.ValidatorConfig);
    }

    [Fact]
    public async Task IngestingTheSameSlugTwice_AddsAVersionRatherThanReplacingOne()
    {
        var database = await NewDatabaseAsync();

        var first = await IngestExampleAsync(database);
        var second = await IngestExampleAsync(database);

        Assert.Equal(first.ProblemId, second.ProblemId);
        Assert.NotEqual(first.ProblemVersionId, second.ProblemVersionId);
        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);

        // Each version keeps a bundle of its own, or the older one would be graded against the
        // newer one's content — the reason a workspace is created from a version at all.
        Assert.NotEqual(first.Bundle, second.Bundle);

        await using var context = database.CreateContext();
        Assert.True(await context.ProblemVersions
            .AsNoTracking()
            .AnyAsync(v => v.Id == first.ProblemVersionId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AReferenceColumnHoldingSomethingElse_FailsWhereItIsRead()
    {
        var database = await NewDatabaseAsync();
        var ingested = await IngestExampleAsync(database);

        // Written the way a repair script or an older build would write it: past the typed
        // property, straight into the column. The converter is what turns that into a failure at
        // the read rather than a bad key handed to the object store.
        await using (var writer = database.CreateContext())
        {
            await writer.Database.ExecuteSqlAsync(
                $"UPDATE problems.problem_versions SET snapshot_reference = 'not-a-role/key' WHERE id = {ingested.ProblemVersionId}",
                TestContext.Current.CancellationToken);
        }

        await using var reader = database.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ProblemVersions
            .AsNoTracking()
            .SingleAsync(v => v.Id == ingested.ProblemVersionId, TestContext.Current.CancellationToken));
    }

    private Task<ProblemsDatabase> NewDatabaseAsync() =>
        ProblemsDatabase.CreateAsync(postgres, nameof(ProblemIngestServiceTests));

    private Task<IngestedProblemVersion> IngestExampleAsync(ProblemsDatabase database) =>
        IngestAsync(database, LoadExample());

    private async Task<IngestedProblemVersion> IngestAsync(ProblemsDatabase database, ProblemPackage package)
    {
        await using var context = database.CreateContext();
        var ingest = new ProblemIngestService(context, _objectStore, new FixedClock(IngestedAt));

        return await ingest.IngestAsync(package, TestContext.Current.CancellationToken);
    }

    private static ProblemPackage LoadExample()
    {
        var loaded = ProblemPackageLoader.Load(ExamplePackage.Directory);

        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? string.Empty : loaded.Error.Message);

        return loaded.Value;
    }

    private static async Task<List<string>> EntryNamesAsync(MemoryStream bundle)
    {
        bundle.Position = 0;

        await using var gzip = new GZipStream(bundle, CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);

        var names = new List<string>();
        while (await reader.GetNextEntryAsync(cancellationToken: TestContext.Current.CancellationToken) is { } entry)
        {
            names.Add(entry.Name);
        }

        return names;
    }

    /// <summary>A clock that does not move, so a stored timestamp is an assertion and not a range.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
