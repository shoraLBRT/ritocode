using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Packaging;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Problems.Tests;

public sealed class ProblemManifestParserTests
{
    [Fact]
    public void Parse_ReadsTheDocumentedFields()
    {
        var manifest = Parsed(TempPackage.ValidManifest);

        Assert.Equal(ProblemManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.Equal("temp-problem", manifest.Slug);
        Assert.Equal("A temporary problem", manifest.Title);
        Assert.Equal(Difficulty.Easy, manifest.Difficulty);
        Assert.Equal("csharp", manifest.Language);
        Assert.Equal(["refactoring"], manifest.Tags);
        Assert.Equal("description.md", manifest.Description);
        Assert.Equal(["src/**/*.cs"], manifest.Workspace.Editable);
    }

    [Fact]
    public void Parse_AppliesDefaultsForWhatWasNotWritten()
    {
        var manifest = Parsed(TempPackage.ValidManifest);

        Assert.Empty(manifest.Hints);
        Assert.Null(manifest.Fixtures.Passing);
        Assert.Equal(WorkspaceSpec.DefaultRoot, manifest.Workspace.Root);
        Assert.Equal(LimitsSpec.DefaultMaxFiles, manifest.Limits.MaxFiles);

        // A validator is required unless it says otherwise: the safe default is the strict one.
        Assert.True(Assert.Single(manifest.Validators).Required);
    }

    [Fact]
    public void Parse_KeepsHintsInTheOrderWritten()
    {
        var manifest = Parsed(TempPackage.ValidManifest.Replace(
            "workspace:",
            "hints:\n  - Look here first.\n  - Then here.\nworkspace:",
            StringComparison.Ordinal));

        Assert.Equal(["Look here first.", "Then here."], manifest.Hints);
    }

    [Fact]
    public void Parse_OnAnUnknownKey_Fails()
    {
        // A silently ignored key is a rule the author believes is in force and is not.
        var error = Failed(TempPackage.ValidManifest + "\nreward: 500\n");

        Assert.Contains("reward", Message(error), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OnADuplicateKey_Fails()
    {
        var error = Failed(TempPackage.ValidManifest + "\nslug: written-twice\n");

        Assert.Contains("slug", Message(error), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OnAnUnknownDifficulty_SaysWhatIsAllowed()
    {
        var error = Failed(TempPackage.ValidManifest.Replace(
            "difficulty: easy",
            "difficulty: trivial",
            StringComparison.Ordinal));

        Assert.Contains("easy, medium, hard", Message(error), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DoesNotAcceptTheNumericFormOfADifficulty()
    {
        Assert.False(ProblemManifestParser.Parse(
            TempPackage.ValidManifest.Replace("difficulty: easy", "difficulty: 1", StringComparison.Ordinal))
            .IsSuccess);
    }

    [Fact]
    public void Parse_OnAnEmptyDocument_Fails()
    {
        Assert.False(ProblemManifestParser.Parse(string.Empty).IsSuccess);
    }

    [Fact]
    public void Parse_ReportsWhereTheProblemIs()
    {
        var error = Failed("schema_version: 1\nslug: [not, a, string]\n");

        Assert.Contains("line 2", Message(error), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_KeepsTheYamlTypesOfValidatorConfiguration()
    {
        var manifest = Parsed(WithValidatorConfig("""
                  retries: 3
                  label: "3"
                  strict: true
                  ratio: 0.5
                  absent: null
                  command: [dotnet, test]
            """));

        var with = Assert.Single(manifest.Validators).With;

        Assert.NotNull(with);
        Assert.Equal(3, with["retries"]!.GetValue<long>());
        Assert.Equal("3", with["label"]!.GetValue<string>());
        Assert.True(with["strict"]!.GetValue<bool>());
        Assert.Equal(0.5, with["ratio"]!.GetValue<double>());
        Assert.Null(with["absent"]);
        Assert.Equal("""["dotnet","test"]""", with["command"]!.ToJsonString());
    }

    [Fact]
    public void Parse_SortsValidatorConfigurationKeys()
    {
        // Authoring order is not information; it must not reach the stored validator_config.
        var manifest = Parsed(WithValidatorConfig("""
                  zulu: 1
                  alpha: 2
                  mike:
                    zulu: 1
                    alpha: 2
            """));

        var with = Assert.Single(manifest.Validators).With;

        Assert.NotNull(with);
        Assert.Equal("""{"alpha":2,"mike":{"alpha":2,"zulu":1},"zulu":1}""", with.ToJsonString());
    }

    private static string WithValidatorConfig(string with) =>
        TempPackage.ValidManifest + "\n    with:\n" + with;

    private static ProblemManifest Parsed(string yaml)
    {
        var result = ProblemManifestParser.Parse(yaml);

        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : Message(result.Error));

        return result.Value;
    }

    private static AppError Failed(string yaml)
    {
        var result = ProblemManifestParser.Parse(yaml);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(ProblemManifestParser.DocumentField, result.Error.Fields!.Keys);

        return result.Error;
    }

    private static string Message(AppError error) =>
        string.Join(" ", error.Fields![ProblemManifestParser.DocumentField]);
}
