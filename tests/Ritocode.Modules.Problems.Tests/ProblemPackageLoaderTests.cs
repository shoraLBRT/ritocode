using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests;

public sealed class ProblemPackageLoaderTests
{
    [Fact]
    public void Load_OnAValidPackage_ClassifiesItsFiles()
    {
        using var package = TempPackage.Valid();

        var result = package.Load();

        Assert.True(result.IsSuccess);
        Assert.Equal(["src/Code.cs"], result.Value.EditableFiles);
        Assert.Equal(["tests/CodeTests.cs"], result.Value.ReadonlyFiles);
        Assert.Equal(package.Directory, result.Value.PackageDirectory);
    }

    [Fact]
    public void Load_OnAValidPackage_AppliesTheDefaultLimits()
    {
        using var package = TempPackage.Valid();

        var manifest = package.Load().Value.Manifest;

        Assert.Equal(LimitsSpec.DefaultMaxFiles, manifest.Limits.MaxFiles);
        Assert.Equal(LimitsSpec.DefaultMaxFileBytes, manifest.Limits.MaxFileBytes);
        Assert.Equal(LimitsSpec.DefaultMaxTotalBytes, manifest.Limits.MaxTotalBytes);
    }

    [Fact]
    public void Load_WithoutADirectory_Fails()
    {
        var result = ProblemPackageLoader.Load(
            Path.Combine(Path.GetTempPath(), "ritocode-not-a-package-" + Guid.CreateVersion7().ToString("n")));

        Assert.False(result.IsSuccess);
        Assert.Contains(ProblemPackageLoader.PackageField, result.Error.Fields!.Keys);
    }

    [Fact]
    public void Load_WithoutAManifest_Fails()
    {
        using var package = TempPackage.Empty();

        Assert.Contains(ProblemPackageLoader.PackageField, package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WhenAFileIsClassifiedByNeitherList_Fails()
    {
        using var package = TempPackage.Valid().With("starter/notes.txt", "unclassified\n");

        var fields = package.LoadExpectingFailure();

        Assert.Contains("notes.txt", Assert.Contains("workspace", fields).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenAFileIsClassifiedByBothLists_Fails()
    {
        using var package = TempPackage.Valid().WithManifestChange("    - tests/**", "    - tests/**\n    - src/**/*.cs");

        var fields = package.LoadExpectingFailure();

        Assert.Contains("both", Assert.Contains("workspace", fields).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenNothingIsEditable_Fails()
    {
        // The glob is well-formed and matches nothing, which the manifest alone cannot detect.
        using var package = TempPackage.Valid()
            .WithManifestChange("    - src/**/*.cs", "    - src/**/*.fs")
            .WithManifestChange("    - tests/**", "    - tests/**\n    - src/**");

        Assert.Contains("workspace.editable", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WithoutTheDescriptionFile_Fails()
    {
        using var package = TempPackage.Valid().Without("description.md");

        Assert.Contains("description", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WithAnEmptyDescription_Fails()
    {
        using var package = TempPackage.Valid().With("description.md", "   \n");

        Assert.Contains("description", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WithoutTheWorkspaceRoot_Fails()
    {
        using var package = TempPackage.Valid().WithManifestChange("  root: starter", "  root: workspace");

        Assert.Contains("workspace.root", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WhenAFileIsOverTheSizeLimit_Fails()
    {
        using var package = TempPackage.Valid()
            .WithManifestChange("workspace:", "limits:\n  max_file_bytes: 16\nworkspace:");

        Assert.Contains("limits.max_file_bytes", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WhenThereAreTooManyFiles_Fails()
    {
        using var package = TempPackage.Valid()
            .WithManifestChange("workspace:", "limits:\n  max_files: 1\nworkspace:");

        Assert.Contains("limits.max_files", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WhenTheWorkspaceIsOverTheTotalLimit_Fails()
    {
        using var package = TempPackage.Valid()
            .WithManifestChange("workspace:", "limits:\n  max_file_bytes: 20\n  max_total_bytes: 20\nworkspace:");

        Assert.Contains("limits.max_total_bytes", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WhenAFixtureTouchesAReadOnlyPath_Fails()
    {
        using var package = TempPackage.Valid()
            .WithManifestChange("validators:", "fixtures:\n  passing: fixtures/passing\nvalidators:")
            .With("fixtures/passing/tests/CodeTests.cs", "public sealed class CodeTests { }\n");

        var fields = package.LoadExpectingFailure();

        Assert.Contains(
            "not an editable workspace path",
            Assert.Contains("fixtures.passing", fields).Single(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenAFixtureDirectoryIsMissing_Fails()
    {
        using var package = TempPackage.Valid()
            .WithManifestChange("validators:", "fixtures:\n  failing: fixtures/failing\nvalidators:");

        Assert.Contains("fixtures.failing", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_WithAValidFixture_Succeeds()
    {
        using var package = TempPackage.Valid()
            .WithManifestChange("validators:", "fixtures:\n  passing: fixtures/passing\nvalidators:")
            .With("fixtures/passing/src/Code.cs", "public static class Code { }\n");

        Assert.True(package.Load().IsSuccess);
    }

    [Fact]
    public void Load_ReportsEveryProblemAtOnce()
    {
        // An author who has to run the loader once per mistake stops writing packages.
        using var package = TempPackage.Valid()
            .Without("description.md")
            .With("starter/notes.txt", "unclassified\n");

        var fields = package.LoadExpectingFailure();

        Assert.Equal(["description", "workspace"], fields.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Load_NamesFieldsAsTheyAreWrittenInTheManifest()
    {
        using var package = TempPackage.Valid().WithManifestChange("    weight: 100", "    weight: 40");

        // Not "Validators", which is what FluentValidation would have called it.
        Assert.Contains("validators", package.LoadExpectingFailure().Keys);
    }

    [Fact]
    public void Load_NamesNestedFieldsAsTheyAreWrittenInTheManifest()
    {
        using var package = TempPackage.Valid().WithManifestChange("    timeout_seconds: 60", "    timeout_seconds: 0");

        Assert.Contains("validators[0].timeout_seconds", package.LoadExpectingFailure().Keys);
    }
}
