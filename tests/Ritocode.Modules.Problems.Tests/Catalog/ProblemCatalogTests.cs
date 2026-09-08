using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Catalog;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Paging;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Problems.Tests.Catalog;

/// <summary>
/// What the catalog resolves, against a real PostgreSQL. The queries here do the version
/// resolution in SQL, so a test on anything but a real database would assert the LINQ rather than
/// the answer.
/// </summary>
public sealed class ProblemCatalogTests(PostgresTestServer postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_ReportsTheHighestPublishedVersionOfEachProblem()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("alpha", versions: 3));

        var page = await ListAsync(database);

        var item = Assert.Single(page.Items);
        Assert.Equal("alpha", item.Slug);
        Assert.Equal(3, item.Version);
    }

    [Fact]
    public async Task List_LeavesOutAProblemWhoseVersionsAreAllDrafts()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("published", versions: 1), Draft("draft-only"));

        var page = await ListAsync(database);

        Assert.Equal(["published"], page.Items.Select(item => item.Slug));
        Assert.Equal(1, page.TotalItems);
    }

    [Fact]
    public async Task List_IgnoresADraftThatIsNewerThanThePublishedVersion()
    {
        var database = await NewDatabaseAsync();

        // The shape a review flow produces: version 2 written but not yet published. Resolving the
        // highest version rather than the highest *published* one would hand a workspace content
        // nobody has approved.
        await SeedAsync(database, new Seed("alpha", PublishedVersions: 1, DraftVersions: 1));

        var item = Assert.Single((await ListAsync(database)).Items);
        Assert.Equal(1, item.Version);
    }

    [Fact]
    public async Task List_OrdersNewestFirstAndPagesWithoutRepeatingARow()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("first"), Published("second"), Published("third"));

        var firstPage = await ListAsync(database, page: 1, pageSize: 2);
        var secondPage = await ListAsync(database, page: 2, pageSize: 2);

        Assert.Equal(["third", "second"], firstPage.Items.Select(item => item.Slug));
        Assert.Equal(["first"], secondPage.Items.Select(item => item.Slug));
        Assert.Equal(3, firstPage.TotalItems);
        Assert.True(firstPage.HasNextPage);
        Assert.False(secondPage.HasNextPage);
    }

    [Fact]
    public async Task List_PastTheLastPage_IsEmptyAndStillReportsTheTotal()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("only"));

        var page = await ListAsync(database, page: 5, pageSize: 20);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalItems);
    }

    [Fact]
    public async Task GetBySlug_ReturnsTheDetailWithItsDescription()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("alpha", versions: 2));

        var result = await CatalogFor(database).GetBySlugAsync("alpha", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("alpha", result.Value.Slug);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal("Description of alpha", result.Value.Description);
        Assert.Equal(Difficulty.Medium, result.Value.Difficulty);
    }

    [Theory]
    [InlineData("ALPHA")]
    [InlineData("  alpha  ")]
    public async Task GetBySlug_NormalisesTheAddressTheSameWayIngestDid(string slug)
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("alpha"));

        var result = await CatalogFor(database).GetBySlugAsync(slug, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("alpha", result.Value.Slug);
    }

    [Theory]
    [InlineData("no-such-problem")]
    [InlineData("draft-only")]
    [InlineData("")]
    public async Task GetBySlug_ReportsNotFoundForAnythingTheCatalogWillNotServe(string slug)
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("alpha"), Draft("draft-only"));

        var result = await CatalogFor(database).GetBySlugAsync(slug, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProblemCatalog.NotFoundCode, result.Error.Code);
    }

    [Fact]
    public async Task GetBySlug_RejectsASlugLongerThanTheColumnCanHold()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, Published("alpha"));

        var result = await CatalogFor(database)
            .GetBySlugAsync(new string('a', Problem.SlugMaxLength + 1), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProblemCatalog.NotFoundCode, result.Error.Code);
    }

    private Task<ProblemsDatabase> NewDatabaseAsync() =>
        ProblemsDatabase.CreateAsync(postgres, nameof(ProblemCatalogTests));

    private static ProblemCatalog CatalogFor(ProblemsDatabase database) => new(database.CreateContext());

    private static async Task<Page<CatalogProblem>> ListAsync(
        ProblemsDatabase database,
        int page = 1,
        int pageSize = 20)
    {
        var request = PageRequest.Create(page, pageSize);
        Assert.True(request.IsSuccess);

        return await CatalogFor(database).ListAsync(request.Value, TestContext.Current.CancellationToken);
    }

    private static Seed Published(string slug, int versions = 1) => new(slug, versions, 0);

    private static Seed Draft(string slug) => new(slug, 0, 1);

    /// <summary>
    /// Rows written through the entities rather than through ingest: the catalog is being tested
    /// here, and going through ingest would make every one of these cases need a package on disk.
    /// </summary>
    private static async Task SeedAsync(ProblemsDatabase database, params Seed[] seeds)
    {
        await using var context = database.CreateContext();

        for (var index = 0; index < seeds.Length; index++)
        {
            var seed = seeds[index];

            // Distinct creation instants, so "newest first" is a claim the seed can actually
            // falsify — and in the order written, so the expectations below read naturally.
            var problem = Problem.Create(
                seed.Slug,
                $"Problem {seed.Slug}",
                Difficulty.Medium,
                $"Description of {seed.Slug}",
                ["refactoring"],
                Noon.AddMinutes(index));

            context.Problems.Add(problem);

            var version = 0;

            for (var published = 0; published < seed.PublishedVersions; published++)
            {
                var row = ProblemVersion.Create(problem.Id, ++version, "{}", Noon);
                row.Publish(Noon);
                context.ProblemVersions.Add(row);
            }

            for (var draft = 0; draft < seed.DraftVersions; draft++)
            {
                context.ProblemVersions.Add(ProblemVersion.Create(problem.Id, ++version, "{}", Noon));
            }
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Seed(string Slug, int PublishedVersions, int DraftVersions);
}
