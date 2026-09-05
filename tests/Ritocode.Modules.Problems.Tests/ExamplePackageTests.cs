using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// The acceptance criterion of #8: the package shipped as the format's reference actually loads.
/// This is what keeps docs/PROBLEM_PACKAGE_SPEC.md and the loader from drifting apart — a format
/// change that the committed example no longer satisfies fails here.
/// </summary>
public sealed class ExamplePackageTests
{
    private static ProblemPackage Load()
    {
        var result = ProblemPackageLoader.Load(ExamplePackage.Directory);

        Assert.True(
            result.IsSuccess,
            result.IsSuccess ? string.Empty : Describe(result.Error));

        return result.Value;
    }

    [Fact]
    public void ReferencePackage_Loads()
    {
        var package = Load();

        Assert.Equal(ExamplePackage.Slug, package.Manifest.Slug);
        Assert.Equal(Difficulty.Medium, package.Manifest.Difficulty);
        Assert.Equal("csharp", package.Manifest.Language);
        Assert.Equal(ProblemManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
    }

    [Fact]
    public void ReferencePackage_CarriesItsDescription()
    {
        var package = Load();

        Assert.Contains("Refactor `src/OrderTotal.cs`", package.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferencePackage_ClassifiesEveryWorkspaceFile()
    {
        var package = Load();

        Assert.Equal(["src/OrderLine.cs", "src/OrderTotal.cs"], package.EditableFiles);
        Assert.Equal(
            ["Orders.csproj", "README.md", "tests/OrderTotalTests.cs"],
            package.ReadonlyFiles);
        Assert.Equal(5, package.WorkspaceFiles.Count);
    }

    [Fact]
    public void ReferencePackage_HasATwoStepPipelineWorthAHundred()
    {
        var package = Load();

        Assert.Equal(["compile", "tests"], package.Pipeline.Steps.Select(step => step.Id));
        Assert.Equal(100, package.Pipeline.Steps.Sum(step => step.Weight));
        Assert.All(package.Pipeline.Steps, step => Assert.True(step.Required));
    }

    [Fact]
    public void ReferencePackage_ProjectsItsPipelineToStorableJson()
    {
        var package = Load();

        Assert.Equal(
            """
            {"schemaVersion":1,"validators":[{"id":"compile","type":"compile","weight":30,"required":true,"timeoutSeconds":120,"with":{"command":["dotnet","build","--warnaserror"]}},{"id":"tests","type":"test","weight":70,"required":true,"timeoutSeconds":300,"with":{"command":["dotnet","test","--no-build"]}}]}
            """,
            package.ValidatorConfigJson);
    }

    [Fact]
    public void ReferencePackage_ShipsBothFixtures()
    {
        var package = Load();

        Assert.Equal("fixtures/passing", package.Manifest.Fixtures.Passing);
        Assert.Equal("fixtures/failing", package.Manifest.Fixtures.Failing);
    }

    private static string Describe(Ritocode.Shared.Errors.AppError error) =>
        string.Join(
            "; ",
            error.Fields?.Select(field => $"{field.Key}: {string.Join(" | ", field.Value)}")
            ?? [error.Message]);
}
