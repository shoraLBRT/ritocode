using Ritocode.Modules.Problems.Contracts;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Packaging;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Problems.Tests.Contracts;

/// <summary>
/// What the Problems module tells Workspaces a version allows, against a real PostgreSQL — the array
/// column is only really exercised by a real database.
/// </summary>
public sealed class WorkspaceAllowanceLookupTests(PostgresTestServer postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Find_ReportsTheEditableFilesAndTheLimitsTheVersionDeclared()
    {
        // A draft, deliberately: facts, not policy. Whether a draft's workspace matters is not this
        // module's question to answer.
        var database = await NewDatabaseAsync();
        var version = await SeedAsync(database, declare: version => version.DeclareWorkspace(
            ["src/a.cs", "src/Z.cs"],
            new LimitsSpec { MaxFiles = 12, MaxFileBytes = 2_048, MaxTotalBytes = 8_192 }));

        var allowance = await LookupFor(database).FindAsync(version.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(allowance);
        Assert.Equal(version.Id, allowance.ProblemVersionId);
        Assert.Equal(["src/Z.cs", "src/a.cs"], allowance.EditableFiles);
        Assert.Equal(12, allowance.MaxFiles);
        Assert.Equal(2_048, allowance.MaxFileBytes);
        Assert.Equal(8_192, allowance.MaxTotalBytes);
    }

    [Fact]
    public async Task Find_ForAVersionThatDeclaredNothing_ReportsNothingEditable_AndTheFormatsDefaults()
    {
        var database = await NewDatabaseAsync();
        var version = await SeedAsync(database, declare: null);

        var allowance = await LookupFor(database).FindAsync(version.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(allowance);
        Assert.Empty(allowance.EditableFiles);
        Assert.Equal(LimitsSpec.DefaultMaxFiles, allowance.MaxFiles);
        Assert.Equal(LimitsSpec.DefaultMaxFileBytes, allowance.MaxFileBytes);
        Assert.Equal(LimitsSpec.DefaultMaxTotalBytes, allowance.MaxTotalBytes);
    }

    [Fact]
    public async Task Find_AnswersNullForAnIdentifierWithNoRow()
    {
        var database = await NewDatabaseAsync();
        await SeedAsync(database, declare: null);

        var allowance = await LookupFor(database).FindAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Null(allowance);
    }

    private Task<ProblemsDatabase> NewDatabaseAsync() =>
        ProblemsDatabase.CreateAsync(postgres, nameof(WorkspaceAllowanceLookupTests));

    private static WorkspaceAllowanceLookup LookupFor(ProblemsDatabase database) => new(database.CreateContext());

    private static async Task<ProblemVersion> SeedAsync(ProblemsDatabase database, Action<ProblemVersion>? declare)
    {
        await using var context = database.CreateContext();

        var problem = Problem.Create("alpha", "Problem alpha", Difficulty.Medium, "Description of alpha", ["refactoring"], Noon);
        context.Problems.Add(problem);

        var version = ProblemVersion.Create(problem.Id, 1, "{}", "starter", Noon);
        declare?.Invoke(version);
        context.ProblemVersions.Add(version);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return version;
    }
}
