using Ritocode.Modules.Problems.Contracts;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Problems.Tests.Contracts;

/// <summary>
/// What the Problems module tells another module about a version, against a real PostgreSQL. The
/// projection crosses a navigation and a value converter, both of which only a real database
/// actually exercises.
/// </summary>
public sealed class ProblemVersionLookupTests(PostgresTestServer postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Find_ReportsAPublishedVersionWithItsProblemAndBundle()
    {
        var database = await NewDatabaseAsync();
        var (problem, versions) = await SeedAsync(database, "alpha", publishedVersions: 1, draftVersions: 0);
        var version = Assert.Single(versions);

        var summary = await LookupFor(database).FindAsync(version.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(summary);
        Assert.Equal(version.Id, summary.Id);
        Assert.Equal(problem.Id, summary.ProblemId);
        Assert.Equal("alpha", summary.Slug);
        Assert.Equal(1, summary.Version);
        Assert.Equal(Noon, summary.PublishedAt);

        // Read back from the column, and still the key the layout gives this version's bundle.
        Assert.Equal(StorageKeys.ProblemBundle(version.Id), summary.SnapshotReference);
    }

    [Fact]
    public async Task Find_ReportsADraftAsADraft_RatherThanHidingIt()
    {
        // Facts, not policy: whether a workspace may open on a draft is the consumer's rule. A lookup
        // that answered null here would be making that rule in the wrong module, and a consumer
        // could no longer tell "no such version" from "not published".
        var database = await NewDatabaseAsync();
        var (_, versions) = await SeedAsync(database, "alpha", publishedVersions: 0, draftVersions: 1);

        var summary = await LookupFor(database).FindAsync(Assert.Single(versions).Id, TestContext.Current.CancellationToken);

        Assert.NotNull(summary);
        Assert.Null(summary.PublishedAt);
    }

    [Fact]
    public async Task Find_ReportsTheVersionAskedFor_NotTheProblemsLatest()
    {
        // The catalog resolves the highest published version; a workspace is pinned to the one it
        // was created from. Sharing the catalog's query here would silently move every workspace
        // forward when a problem is revised.
        var database = await NewDatabaseAsync();
        var (_, versions) = await SeedAsync(database, "alpha", publishedVersions: 3, draftVersions: 0);

        var summary = await LookupFor(database).FindAsync(versions[0].Id, TestContext.Current.CancellationToken);

        Assert.NotNull(summary);
        Assert.Equal(1, summary.Version);
    }

    [Fact]
    public async Task Find_AnswersNullForAnIdentifierWithNoRow()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, "alpha", publishedVersions: 1, draftVersions: 0);

        var summary = await LookupFor(database).FindAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Null(summary);
    }

    private Task<ProblemsDatabase> NewDatabaseAsync() =>
        ProblemsDatabase.CreateAsync(postgres, nameof(ProblemVersionLookupTests));

    private static ProblemVersionLookup LookupFor(ProblemsDatabase database) => new(database.CreateContext());

    private static async Task<(Problem Problem, ProblemVersion[] Versions)> SeedAsync(
        ProblemsDatabase database,
        string slug,
        int publishedVersions,
        int draftVersions)
    {
        await using var context = database.CreateContext();

        var problem = Problem.Create(slug, $"Problem {slug}", Difficulty.Medium, $"Description of {slug}", ["refactoring"], Noon);
        context.Problems.Add(problem);

        var versions = new List<ProblemVersion>();

        for (var number = 1; number <= publishedVersions + draftVersions; number++)
        {
            var version = ProblemVersion.Create(problem.Id, number, "{}", Noon);

            if (number <= publishedVersions)
            {
                version.Publish(Noon);
            }

            context.ProblemVersions.Add(version);
            versions.Add(version);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (problem, [.. versions]);
    }
}
