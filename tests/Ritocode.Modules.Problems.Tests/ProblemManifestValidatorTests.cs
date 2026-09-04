using FluentValidation.Results;
using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests;

public sealed class ProblemManifestValidatorTests
{
    private static readonly ProblemManifestValidator Validator = new();

    [Fact]
    public void AValidManifest_Passes()
    {
        Assert.True(Validate(TempPackage.ValidManifest).IsValid);
    }

    [Theory]
    // schema_version is the only reason a future format can be rejected instead of misread.
    [InlineData("schema_version: 1", "schema_version: 2", "SchemaVersion")]
    [InlineData("slug: temp-problem", "slug: Temp_Problem", "Slug")]
    [InlineData("title: A temporary problem", "title: ''", "Title")]
    [InlineData("language: csharp", "language: C-Sharp", "Language")]
    [InlineData("description: description.md", "description: ../elsewhere.md", "Description")]
    [InlineData("  - refactoring", "  - Refactoring", "Tags[0]")]
    [InlineData("    - src/**/*.cs", "    - /etc/passwd", "Editable[0]")]
    [InlineData("    - tests/**", "    - ../tests/**", "Readonly[0]")]
    [InlineData("  root: starter", "  root: ../starter", "Root")]
    [InlineData("- id: tests", "- id: Test Suite", "Validators[0].Id")]
    [InlineData("    type: test", "    type: unit_test", "Validators[0].Type")]
    [InlineData("    weight: 100", "    weight: 40", "Validators")]
    [InlineData("    timeout_seconds: 60", "    timeout_seconds: 901", "Validators[0].TimeoutSeconds")]
    public void AnInvalidField_IsReportedAgainstThatField(string original, string replacement, string field)
    {
        var result = Validate(TempPackage.ValidManifest.Replace(original, replacement, StringComparison.Ordinal));

        Assert.False(result.IsValid);
        AssertFailedOn(result, field);
    }

    [Fact]
    public void AManifestWithoutAWeight_IsRejected()
    {
        // Absent is not zero: a weight left out would otherwise be a silent zero-scoring validator.
        var result = Validate(TempPackage.ValidManifest.Replace(
            "    weight: 100\n",
            string.Empty,
            StringComparison.Ordinal));

        Assert.False(result.IsValid);
        AssertFailedOn(result, "Validators[0].Weight");
    }

    [Fact]
    public void WeightsThatDoNotSumToAHundred_AreRejected()
    {
        var result = Validate(TempPackage.ManifestWithTwoValidators("compile")
            .Replace("    weight: 50", "    weight: 60", StringComparison.Ordinal));

        Assert.False(result.IsValid);
        Assert.Contains("sum to exactly 100", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoValidatorsSharingAnId_AreRejected()
    {
        var result = Validate(TempPackage.ManifestWithTwoValidators("tests", "test"));

        Assert.False(result.IsValid);
        Assert.Contains("distinct", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoValidatorsOfTheSameType_AreAllowed()
    {
        // One pipeline may run two test suites; only the id has to be unique.
        var result = Validate(TempPackage.ManifestWithTwoValidators("slow-tests", "test"));

        Assert.True(result.IsValid, Messages(result));
    }

    [Fact]
    public void AManifestWithoutValidators_IsRejected()
    {
        var result = Validate(TempPackage.ValidManifest[..TempPackage.ValidManifest.IndexOf("validators:", StringComparison.Ordinal)]);

        Assert.False(result.IsValid);
        AssertFailedOn(result, "Validators");
    }

    [Fact]
    public void AManifestWithNothingEditable_IsRejected()
    {
        var result = Validate(TempPackage.ValidManifest.Replace(
            "  editable:\n    - src/**/*.cs\n",
            string.Empty,
            StringComparison.Ordinal));

        Assert.False(result.IsValid);
        AssertFailedOn(result, "Editable");
    }

    [Fact]
    public void RepeatedTags_AreRejected()
    {
        var result = Validate(TempPackage.ValidManifest.Replace(
            "  - refactoring",
            "  - refactoring\n  - refactoring",
            StringComparison.Ordinal));

        Assert.False(result.IsValid);
        Assert.Contains("distinct", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public void MoreTagsThanAllowed_AreRejected()
    {
        var tags = string.Join('\n', Enumerable.Range(0, ProblemManifest.MaxTags + 1).Select(i => $"  - tag-{i}"));
        var result = Validate(TempPackage.ValidManifest.Replace("  - refactoring", tags, StringComparison.Ordinal));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AHintLongerThanAllowed_IsRejected()
    {
        var result = Validate(TempPackage.ValidManifest.Replace(
            "workspace:",
            $"hints:\n  - {new string('x', ProblemManifest.HintMaxLength + 1)}\nworkspace:",
            StringComparison.Ordinal));

        Assert.False(result.IsValid);
        AssertFailedOn(result, "Hints[0]");
    }

    [Fact]
    public void ATotalLimitBelowTheFileLimit_IsRejected()
    {
        var result = Validate(TempPackage.ValidManifest.Replace(
            "workspace:",
            "limits:\n  max_file_bytes: 4096\n  max_total_bytes: 1024\nworkspace:",
            StringComparison.Ordinal));

        Assert.False(result.IsValid);
        AssertFailedOn(result, "MaxTotalBytes");
    }

    /// <summary>
    /// Child validators prefix the path they are reached by — <c>Workspace.Editable[0]</c> — so the
    /// tail is what a case names.
    /// </summary>
    private static void AssertFailedOn(ValidationResult result, string field) => Assert.Contains(
        result.Errors,
        failure => failure.PropertyName.EndsWith(field, StringComparison.Ordinal));

    private static ValidationResult Validate(string yaml)
    {
        var parsed = ProblemManifestParser.Parse(yaml);

        Assert.True(parsed.IsSuccess, parsed.IsSuccess ? string.Empty : parsed.Error.Message);

        return Validator.Validate(parsed.Value);
    }

    private static string Messages(ValidationResult result) =>
        string.Join(" ", result.Errors.Select(failure => failure.ErrorMessage));
}
