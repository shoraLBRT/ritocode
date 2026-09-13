using System.Text.Json.Nodes;

namespace Ritocode.Modules.Evaluations.Validators;

/// <summary>
/// One step of a problem version's validator pipeline, as the Evaluations module consumes it — the
/// fields of <c>validator_config</c> that docs/PROBLEM_PACKAGE_SPEC.md documents, and nothing of the
/// Problems module's types.
/// </summary>
/// <remarks>
/// How it reaches this module — a read contract answering a version's pipeline — is #17's to add, with
/// its first caller. The plugin interface is written against this record so that #17 changes where the
/// steps come from and nothing a plugin sees.
/// </remarks>
/// <param name="Id">Identifies the step in the report; stable across runs.</param>
/// <param name="Type">Selects the plugin, compared ordinally.</param>
/// <param name="Weight">Share of the score, 0–100. What a weight buys is #20's rule, not a plugin's.</param>
/// <param name="Required">A failed required step fails the submission and stops the pipeline.</param>
/// <param name="TimeoutSeconds">Upper bound the runner may lower, never one it must honour.</param>
/// <param name="With">Plugin configuration, carried by the manifest uninterpreted and read only by the plugin its type selects.</param>
public sealed record ValidatorStepDefinition(
    string Id,
    string Type,
    int Weight,
    bool Required,
    int TimeoutSeconds,
    JsonObject With);
