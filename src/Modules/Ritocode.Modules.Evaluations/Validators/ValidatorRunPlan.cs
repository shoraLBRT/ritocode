using System.Text.Json.Nodes;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Evaluations.Validators;

/// <summary>What a plugin asks the runner to run for one step.</summary>
/// <remarks>
/// Only the command. The image, its injected arguments, the limits and the deadline belong to the runner
/// registry and the orchestrator (ADR 0006 §2, §6), so a plugin cannot choose them — and a task author,
/// who writes <c>with</c>, cannot either.
/// </remarks>
/// <param name="Command">The argument vector as the manifest declared it. The runner appends the image's arguments.</param>
public sealed record ValidatorRunPlan(IReadOnlyList<string> Command)
{
    /// <summary>The <c>with</c> key the compile and test validators of the slice read (docs/PROBLEM_PACKAGE_SPEC.md).</summary>
    public const string CommandKey = "command";

    /// <summary>
    /// The plan of a step whose <c>with</c> names its <c>command</c> as a non-empty list of non-blank
    /// strings — the shape every command-running plugin shares, so it is parsed once.
    /// </summary>
    public static Result<ValidatorRunPlan> FromCommand(ValidatorStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        // An argument vector, never a string for a shell to split: a shell would be a second interpreter
        // of content, inside the container, with rules the author did not write the command against.
        if (step.With[CommandKey] is not JsonArray arguments || arguments.Count == 0)
        {
            return Invalid(step, "Must be a non-empty list of strings.");
        }

        var command = new List<string>(arguments.Count);

        foreach (var argument in arguments)
        {
            if (argument is not JsonValue value
                || !value.TryGetValue<string>(out var text)
                || string.IsNullOrWhiteSpace(text))
            {
                return Invalid(step, "Every element must be a non-blank string.");
            }

            command.Add(text);
        }

        return new ValidatorRunPlan(command);
    }

    private static AppError Invalid(ValidatorStepDefinition step, string message) =>
        AppError.Validation(
            $"Validator '{step.Id}' cannot be run.",
            new Dictionary<string, string[]> { [$"validators.{step.Id}.with.{CommandKey}"] = [message] });
}
