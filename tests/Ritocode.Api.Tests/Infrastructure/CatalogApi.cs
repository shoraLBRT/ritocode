using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// A <see cref="TestApi"/> whose database already holds a known catalog, so the endpoint tests
/// assert on the HTTP contract rather than on how the rows got there.
/// </summary>
/// <remarks>
/// Rows are written directly, not through ingest: ingest needs object storage, and what these
/// tests are for is the shape of the response. Ingest has its own tests, against a real MinIO, in
/// the Problems module's assembly.
/// </remarks>
public sealed class CatalogApi(PostgresTestServer postgres) : IAsyncLifetime
{
    /// <summary>Seeded, published, and the newest of the three.</summary>
    public const string NewestSlug = "third-problem";

    /// <summary>Seeded with two versions, of which only the first is published.</summary>
    public const string TwoVersionSlug = "first-problem";

    /// <summary>Seeded with a draft version only, so the catalog must not serve it.</summary>
    public const string DraftSlug = "unpublished-problem";

    /// <summary>How many problems the catalog should report.</summary>
    public const int PublishedCount = 3;

    private readonly TestApi _api = new(postgres);

    public HttpClient Client => _api.Client;

    public async ValueTask InitializeAsync()
    {
        await _api.InitializeAsync();
        await SeedAsync();
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    private async Task SeedAsync()
    {
        await using var context = new ProblemsDbContext(
            new DbContextOptionsBuilder<ProblemsDbContext>()
                .UseNpgsql(_api.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options);

        var createdAt = new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

        Add(context, TwoVersionSlug, "First problem", Difficulty.Easy, createdAt, published: 1, drafts: 1);
        Add(context, "second-problem", "Second problem", Difficulty.Medium, createdAt.AddMinutes(1), published: 1, drafts: 0);
        Add(context, NewestSlug, "Third problem", Difficulty.Hard, createdAt.AddMinutes(2), published: 2, drafts: 0);
        Add(context, DraftSlug, "Unpublished problem", Difficulty.Easy, createdAt.AddMinutes(3), published: 0, drafts: 1);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void Add(
        ProblemsDbContext context,
        string slug,
        string title,
        Difficulty difficulty,
        DateTimeOffset createdAt,
        int published,
        int drafts)
    {
        var problem = Problem.Create(slug, title, difficulty, $"# {title}\n\nWhat to improve.", ["refactoring"], createdAt);
        context.Problems.Add(problem);

        var version = 0;

        for (var index = 0; index < published; index++)
        {
            var row = ProblemVersion.Create(problem.Id, ++version, "{}", createdAt);
            row.Publish(createdAt);
            context.ProblemVersions.Add(row);
        }

        for (var index = 0; index < drafts; index++)
        {
            context.ProblemVersions.Add(ProblemVersion.Create(problem.Id, ++version, "{}", createdAt));
        }
    }
}
