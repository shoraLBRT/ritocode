using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// The projection into <c>problem_versions.validator_config</c>. Determinism is the claim the
/// verdict rests on, so the same pipeline has to serialise to the same bytes — otherwise "the same
/// submission" is a phrase nothing can check.
/// </summary>
public sealed class ValidatorPipelineTests
{
    [Fact]
    public void ToJson_WritesEveryFieldIncludingTheDefaults()
    {
        // Stored state states what it does. Reading it back must not depend on the loader's
        // defaults, which are free to change under a later schema_version.
        Assert.Equal(
            """
            {"schemaVersion":1,"validators":[{"id":"tests","type":"test","weight":100,"required":true,"timeoutSeconds":60,"with":{}}]}
            """,
            Json(TempPackage.ValidManifest));
    }

    [Fact]
    public void ToJson_IsUnchangedByTheOrderTheYamlWasWrittenIn()
    {
        var written = Json(TempPackage.ValidManifest + "\n    with:\n      beta: 2\n      alpha: 1");
        var reordered = Json(TempPackage.ValidManifest + "\n    with:\n      alpha: 1\n      beta: 2");

        Assert.Equal(written, reordered);
        Assert.Contains("""{"alpha":1,"beta":2}""", written, StringComparison.Ordinal);
    }

    [Fact]
    public void ToJson_IsStableAcrossCalls()
    {
        var pipeline = Pipeline(TempPackage.ValidManifest + "\n    with:\n      command: [dotnet, test]");

        Assert.Equal(pipeline.ToJson(), pipeline.ToJson());
    }

    [Fact]
    public void ToJson_KeepsTheStepsInTheOrderTheyRun()
    {
        var json = Json(TempPackage.ManifestWithTwoValidators("compile"));

        Assert.True(
            json.IndexOf("\"tests\"", StringComparison.Ordinal) < json.IndexOf("\"compile\"", StringComparison.Ordinal),
            $"The pipeline order was not preserved: {json}");
    }

    [Fact]
    public void ToJson_CarriesAnOptedOutRequirement()
    {
        Assert.Contains(
            "\"required\":false",
            Json(TempPackage.ValidManifest + "\n    required: false"),
            StringComparison.Ordinal);
    }

    private static ValidatorPipeline Pipeline(string yaml)
    {
        using var package = TempPackage.Valid().With("problem.yaml", yaml);

        var result = package.Load();

        Assert.True(result.IsSuccess, result.IsSuccess ? string.Empty : result.Error.Message);

        return result.Value.Pipeline;
    }

    private static string Json(string yaml) => Pipeline(yaml).ToJson();
}
