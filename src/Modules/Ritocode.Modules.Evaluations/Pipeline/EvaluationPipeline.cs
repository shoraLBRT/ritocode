using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Evaluations.Pipeline;

/// <summary>
/// Runs a problem version's validator pipeline over one frozen input tree, step by step (#17).
/// </summary>
/// <remarks>
/// <para>
/// Sequential, as the slice allows: no retries, priorities, cancellation or parallelism. For each step
/// the plugin its type selects plans a command, the sandbox runner runs it, and the plugin interprets
/// what the run left. Nothing here starts a process; every execution goes through
/// <see cref="ISandboxRunner"/>.
/// </para>
/// <para>
/// When the pipeline stops, and what that means for the attempt, are the rules of this class: a step
/// that could not complete — or could not be run at all — stops it, and the attempt did not run to the
/// end, so ADR 0009 §4 records it <c>Failed</c>; a failed <em>required</em> step stops it too, and the
/// attempt did run to the end, because a wrong answer is a score and not a broken run. Every step after a
/// stop is reported <c>Skipped</c>, so a report always has one entry per step.
/// </para>
/// <para>
/// <b>Not registered in the host yet — decided with the maintainer on 2026-09-13.</b> Everything an
/// evaluation needs to run arrives in slice stage 5: the runner (#21), its image (#22) and the compile and
/// test validators (#19). This class depends on <see cref="ISandboxRunner"/>, and the host validates its
/// container on build in Development, so a registration nothing can construct would stop the host
/// starting. The <c>ISubmissionEvaluator</c> contract of ADR 0009 — which ADR 0007 §7 requires to be
/// registered from the moment it exists — and the hosted loop that calls it land with the runner.
/// </para>
/// </remarks>
public sealed class EvaluationPipeline(IValidatorPluginRegistry plugins, ISandboxRunner runner)
{
    public async Task<PipelineOutcome> RunAsync(
        IReadOnlyList<ValidatorStepDefinition> steps,
        string workspaceDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (steps.Count == 0)
        {
            // The package format requires weights summing to 100, so a stored pipeline always has a step.
            throw new ArgumentException("A pipeline has at least one step.", nameof(steps));
        }

        var results = new List<ValidatorResult>(steps.Count);
        var ranToEnd = true;
        var stopped = false;

        foreach (var step in steps)
        {
            if (stopped)
            {
                results.Add(ValidatorResult.Skipped(step));
                continue;
            }

            var result = await RunStepAsync(step, workspaceDirectory, outputDirectory, cancellationToken);
            results.Add(result);

            switch (result.Outcome)
            {
                case ValidatorOutcome.NotCompleted:
                case ValidatorOutcome.NotRunnable:
                    // The attempt cannot be graded, so running the rest would spend containers on a verdict
                    // nobody gets.
                    ranToEnd = false;
                    stopped = true;
                    break;

                case ValidatorOutcome.Failed when step.Required:
                    // docs/PROBLEM_PACKAGE_SPEC.md: a task that does not compile has nothing to test.
                    stopped = true;
                    break;
            }
        }

        return new PipelineOutcome(ranToEnd, results);
    }

    private async Task<ValidatorResult> RunStepAsync(
        ValidatorStepDefinition step,
        string workspaceDirectory,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (plugins.Find(step.Type) is not { } plugin)
        {
            return ValidatorResult.NotRunnable(step, $"No validator answers the type '{step.Type}'.");
        }

        var plan = plugin.Plan(step);

        if (!plan.IsSuccess)
        {
            return ValidatorResult.NotRunnable(step, Describe(plan.Error));
        }

        var request = new SandboxRunRequest(
            step.Id,
            plan.Value.Command,
            workspaceDirectory,
            outputDirectory,
            TimeSpan.FromSeconds(step.TimeoutSeconds));

        var run = await runner.RunAsync(request, cancellationToken);
        var result = await plugin.InterpretAsync(step, run, cancellationToken);

        RequireAnHonestReport(plugin, step, run, result);

        return result;
    }

    /// <summary>
    /// A plugin's result is what a person reads and what #38 compares between two evaluations. One that
    /// answers for another step, reports a run outcome the runner did not observe, or calls a step that ran
    /// skipped or unrunnable would put a false entry in the report — so it fails the evaluation loudly
    /// instead of being recorded.
    /// </summary>
    private static void RequireAnHonestReport(
        IValidatorPlugin plugin,
        ValidatorStepDefinition step,
        SandboxRunResult run,
        ValidatorResult result)
    {
        var name = plugin.GetType().Name;

        if (!string.Equals(result.Id, step.Id, StringComparison.Ordinal)
            || !string.Equals(result.Type, step.Type, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{name} reported for step '{result.Id}' ({result.Type}) while interpreting step '{step.Id}' ({step.Type}).");
        }

        if (result.Outcome is ValidatorOutcome.Skipped or ValidatorOutcome.NotRunnable)
        {
            throw new InvalidOperationException($"{name} reported step '{step.Id}' as {result.Outcome}, but it ran.");
        }

        if (result.RunOutcome != run.Outcome)
        {
            throw new InvalidOperationException(
                $"{name} reported step '{step.Id}' as ending {result.RunOutcome}; the runner observed {run.Outcome}.");
        }
    }

    private static string Describe(AppError error) =>
        error.Fields is { Count: > 0 } fields
            ? $"{error.Message} "
              + string.Join(
                  " ",
                  fields
                      .OrderBy(field => field.Key, StringComparer.Ordinal)
                      .SelectMany(field => field.Value.Select(message => $"{field.Key}: {message}")))
            : error.Message;
}

/// <summary>What running a pipeline produced.</summary>
/// <param name="RanToEnd">
/// Every step either completed or was skipped after a failed required step. False when a step could not
/// complete or could not be run, which per ADR 0009 §4 makes the attempt <c>Failed</c> whatever its
/// results say.
/// </param>
/// <param name="Results">One per step, in pipeline order — the entries of <c>validator_results</c>.</param>
public sealed record PipelineOutcome(bool RanToEnd, IReadOnlyList<ValidatorResult> Results);
