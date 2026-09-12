using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// The problem set of #42, checked as it is committed. What these tests can establish is that each
/// package is loadable content with two fixtures that differ; that the known-good answer passes and
/// the known-bad one fails is a claim only a sandbox run can make, and it belongs to #38 in stage 5.
/// </summary>
public sealed class CatalogPackageTests
{
    public static TheoryData<string> CatalogSlugs => new(ContentPackages.CatalogSlugs);

    public static TheoryData<string> EverySlug => new(ContentPackages.AllSlugs);

    [Theory]
    [MemberData(nameof(EverySlug))]
    public void EveryPackageInTheContentTree_Loads(string slug)
    {
        var package = Load(slug);

        Assert.Equal(slug, package.Manifest.Slug);
        Assert.Equal(ProblemManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
    }

    [Fact]
    public void TheContentTree_HoldsTheCatalogProblemsAndTheReferencePackage()
    {
        // A package nobody mentioned in the docs shows up here first. The seeder publishes whatever
        // is in this directory, so this count is the one a development host ends up serving.
        Assert.Equal(
            [.. ContentPackages.CatalogSlugs.Append(ExamplePackage.Slug).Order(StringComparer.Ordinal)],
            ContentPackages.AllSlugs);
    }

    [Theory]
    [MemberData(nameof(CatalogSlugs))]
    public void EveryCatalogProblem_IsAnAuthoredCSharpTask(string slug)
    {
        var package = Load(slug);

        Assert.Equal("csharp", package.Manifest.Language);
        Assert.NotEmpty(package.Manifest.Tags);
        Assert.NotEmpty(package.Manifest.Hints);
        Assert.StartsWith("# ", package.Description, StringComparison.Ordinal);

        // The verdict is judged honest against the description, so it has to say what is graded and
        // not only what the task is.
        Assert.Contains("## What is graded", package.Description, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CatalogSlugs))]
    public void EveryCatalogProblem_IsGradedByCompileThenTests(string slug)
    {
        var package = Load(slug);

        Assert.Equal(["compile", "tests"], package.Pipeline.Steps.Select(step => step.Id));
        Assert.Equal(["compile", "test"], package.Pipeline.Steps.Select(step => step.Type));
        Assert.Equal(100, package.Pipeline.Steps.Sum(step => step.Weight));
        Assert.All(package.Pipeline.Steps, step => Assert.True(step.Required));
    }

    [Theory]
    [MemberData(nameof(CatalogSlugs))]
    public void EveryCatalogProblem_LetsTheUserChangeTheCodeAndNotTheTests(string slug)
    {
        var package = Load(slug);

        Assert.NotEmpty(package.EditableFiles);
        Assert.All(package.EditableFiles, path => Assert.StartsWith("src/", path, StringComparison.Ordinal));

        // The tests decide the verdict, so they are the package's and never the submission's.
        Assert.Contains(package.ReadonlyFiles, path => path.StartsWith("tests/", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CatalogSlugs))]
    public void EveryCatalogProblem_ShipsTwoFixturesThatDisagree(string slug)
    {
        var package = Load(slug);

        var passing = FixtureFiles(package, package.Manifest.Fixtures.Passing);
        var failing = FixtureFiles(package, package.Manifest.Fixtures.Failing);

        Assert.NotEmpty(passing);
        Assert.NotEmpty(failing);

        // Two fixtures holding the same bytes cannot be a known-good and a known-bad answer, and the
        // cheap way to ship that mistake is to copy one directory onto the other and forget.
        Assert.NotEqual(passing, failing);

        // An overlay that matches the starter byte for byte changes nothing, so it proves nothing.
        Assert.True(
            passing.Any(file => ChangesTheStarter(package, file.Key, file.Value)),
            $"The passing fixture of {slug} leaves the starter tree unchanged.");
        Assert.True(
            failing.Any(file => ChangesTheStarter(package, file.Key, file.Value)),
            $"The failing fixture of {slug} leaves the starter tree unchanged.");
    }

    [Fact]
    public void TheCatalogProblems_AreThreeDifferentTasks()
    {
        var packages = ContentPackages.CatalogSlugs.Select(Load).ToList();

        Assert.Equal(3, packages.Count);

        // Three tasks at one difficulty, or three over the same code, would show that the verdict
        // works on one problem and say nothing about whether it discriminates.
        Assert.Equal(
            [Difficulty.Easy, Difficulty.Medium, Difficulty.Hard],
            packages.Select(package => package.Manifest.Difficulty).Order());
        Assert.Equal(
            3,
            packages.Select(package => package.Manifest.Title).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            3,
            packages
                .Select(package => string.Join(",", package.WorkspaceFiles))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    private static ProblemPackage Load(string slug)
    {
        var result = ProblemPackageLoader.Load(ContentPackages.DirectoryFor(slug));

        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : Describe(slug, result.Error));

        return result.Value;
    }

    /// <summary>Workspace-relative path to contents, for every file in a fixture overlay.</summary>
    private static Dictionary<string, string> FixtureFiles(ProblemPackage package, string? fixture)
    {
        Assert.NotNull(fixture);

        var root = Path.Combine(package.PackageDirectory, fixture);

        return Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
    }

    private static bool ChangesTheStarter(ProblemPackage package, string workspacePath, string contents)
    {
        var starterFile = Path.Combine(
            package.PackageDirectory,
            package.Manifest.Workspace.Root,
            workspacePath.Replace('/', Path.DirectorySeparatorChar));

        return !File.Exists(starterFile)
            || !string.Equals(File.ReadAllText(starterFile), contents, StringComparison.Ordinal);
    }

    private static string Describe(string slug, Ritocode.Shared.Errors.AppError error) =>
        $"{slug}: " + string.Join(
            "; ",
            error.Fields?.Select(field => $"{field.Key}: {string.Join(" | ", field.Value)}")
            ?? [error.Message]);
}
