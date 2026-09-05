using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// The validated validator pipeline of a package, and the one thing stored in
/// <c>problem_versions.validator_config</c>.
/// </summary>
/// <param name="SchemaVersion">The manifest schema this pipeline was read from.</param>
/// <param name="Steps">Executed in this order.</param>
public sealed record ValidatorPipeline(int SchemaVersion, IReadOnlyList<ValidatorStep> Steps)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    internal static ValidatorPipeline FromManifest(ProblemManifest manifest) => new(
        manifest.SchemaVersion!.Value,
        [.. manifest.Validators.Select(ValidatorStep.FromSpec)]);

    /// <summary>
    /// The canonical JSON for <c>validator_config</c>. Canonical means byte-identical for the same
    /// pipeline however the YAML was written: every field is present even when it was left to its
    /// default, field order is fixed here, and <c>with</c> keys were sorted while parsing.
    /// Determinism is the claim the verdict rests on, and it starts with the configuration.
    /// </summary>
    public string ToJson()
    {
        var steps = new JsonArray();

        foreach (var step in Steps)
        {
            steps.Add(new JsonObject
            {
                ["id"] = step.Id,
                ["type"] = step.Type,
                ["weight"] = step.Weight,
                ["required"] = step.Required,
                ["timeoutSeconds"] = step.TimeoutSeconds,
                // Deep-cloned: a JsonNode belongs to one parent, and the step is reusable.
                ["with"] = step.With?.DeepClone() ?? new JsonObject(),
            });
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["validators"] = steps,
        };

        return document.ToJsonString(SerializerOptions);
    }
}

/// <summary>One validated step of the pipeline.</summary>
/// <param name="Id">Identifies the step in the report.</param>
/// <param name="Type">Selects the validator plugin (#18).</param>
/// <param name="Weight">Share of the score, 0–100; a pipeline's weights sum to 100.</param>
/// <param name="Required">A failed required step fails the submission whatever the score.</param>
/// <param name="TimeoutSeconds">Upper bound the runner may lower, never one it must honour.</param>
/// <param name="With">Plugin configuration, carried but not interpreted.</param>
public sealed record ValidatorStep(
    string Id,
    string Type,
    int Weight,
    bool Required,
    int TimeoutSeconds,
    JsonObject? With)
{
    internal static ValidatorStep FromSpec(ValidatorSpec spec) => new(
        spec.Id,
        spec.Type,
        spec.Weight!.Value,
        spec.Required,
        spec.TimeoutSeconds!.Value,
        spec.With);
}
