using Ritocode.Modules.Evaluations.Sandbox;

namespace Ritocode.Modules.Evaluations.Validators;

/// <summary>
/// What one step of a pipeline reports: the entry <c>submission_reports.validator_results</c> holds per
/// validator, so a person reads each validator's verdict rather than only a score (ADR 0009 §4).
/// </summary>
/// <remarks>
/// Built only through its three factories, each of which holds a rule the report depends on: a run that
/// did not complete is never judged, a judged run completed, and the checks are the sorted, duplicate-free
/// projection a verdict is compared on.
/// </remarks>
public sealed record ValidatorResult
{
    private ValidatorResult(
        string id,
        string type,
        ValidatorOutcome outcome,
        SandboxRunOutcome? runOutcome,
        IReadOnlyList<ValidatorCheck> checks,
        string? summary)
    {
        Id = id;
        Type = type;
        Outcome = outcome;
        RunOutcome = runOutcome;
        Checks = checks;
        Summary = summary;
    }

    /// <summary>The step's <c>id</c>.</summary>
    public string Id { get; }

    /// <summary>The step's <c>type</c>.</summary>
    public string Type { get; }

    public ValidatorOutcome Outcome { get; }

    /// <summary>
    /// How the step's container ended, carried through so <c>TimedOut</c>, <c>ResourceExhausted</c> and
    /// <c>Crashed</c> stay distinct in the report. Null for a step that never ran.
    /// </summary>
    public SandboxRunOutcome? RunOutcome { get; }

    /// <summary>The normalised projection, ordered ordinally by name. Empty for a validator with no per-check breakdown.</summary>
    public IReadOnlyList<ValidatorCheck> Checks { get; }

    /// <summary>One line a person reads. Never raw output, which is an artifact (#23).</summary>
    public string? Summary { get; }

    /// <summary>A completed run, judged by the plugin as passing or failing.</summary>
    /// <exception cref="ArgumentException">The run did not complete, or two checks share a name.</exception>
    public static ValidatorResult Judged(
        ValidatorStepDefinition step,
        SandboxRunResult run,
        bool passed,
        IEnumerable<ValidatorCheck>? checks = null,
        string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(run);

        if (run.Outcome != SandboxRunOutcome.Completed)
        {
            throw new ArgumentException(
                $"A run that ended {run.Outcome} has no verdict; report it with {nameof(NotCompleted)}.",
                nameof(run));
        }

        return new ValidatorResult(
            step.Id,
            step.Type,
            passed ? ValidatorOutcome.Passed : ValidatorOutcome.Failed,
            SandboxRunOutcome.Completed,
            Projection(checks ?? []),
            summary);
    }

    /// <summary>A run that did not complete — timed out, exhausted a resource, or crashed.</summary>
    /// <exception cref="ArgumentException">The run completed, and so has a verdict.</exception>
    public static ValidatorResult NotCompleted(ValidatorStepDefinition step, SandboxRunResult run, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(run);

        if (run.Outcome == SandboxRunOutcome.Completed)
        {
            throw new ArgumentException($"A completed run has a verdict; report it with {nameof(Judged)}.", nameof(run));
        }

        return new ValidatorResult(step.Id, step.Type, ValidatorOutcome.NotCompleted, run.Outcome, [], summary);
    }

    /// <summary>A step that never ran, because the pipeline stopped before it.</summary>
    public static ValidatorResult Skipped(ValidatorStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return new ValidatorResult(step.Id, step.Type, ValidatorOutcome.Skipped, runOutcome: null, [], summary: null);
    }

    private static ValidatorCheck[] Projection(IEnumerable<ValidatorCheck> checks)
    {
        var ordered = checks.OrderBy(check => check.Name, StringComparer.Ordinal).ToArray();

        for (var index = 1; index < ordered.Length; index++)
        {
            if (string.Equals(ordered[index].Name, ordered[index - 1].Name, StringComparison.Ordinal))
            {
                // Two outcomes under one name would make the projection depend on which was kept.
                throw new ArgumentException($"The check '{ordered[index].Name}' is reported twice.", nameof(checks));
            }
        }

        return ordered;
    }
}

/// <summary>What one step's validator concluded.</summary>
public enum ValidatorOutcome
{
    /// <summary>The run completed, and the validator's answer is yes.</summary>
    Passed,

    /// <summary>The run completed, and the validator's answer is no. A wrong answer, not a broken run.</summary>
    Failed,

    /// <summary>The run did not complete; <see cref="ValidatorResult.RunOutcome"/> says how.</summary>
    NotCompleted,

    /// <summary>The step never ran: the pipeline stopped at a required step before it.</summary>
    Skipped,
}

/// <summary>One named check a validator found — for the test validator, one test.</summary>
/// <param name="Name">Stable across runs, and unique within its step.</param>
public sealed record ValidatorCheck(string Name, ValidatorCheckOutcome Outcome);

/// <summary>The outcome of one check.</summary>
public enum ValidatorCheckOutcome
{
    Passed,
    Failed,

    /// <summary>Declared and not executed — a skipped test, which is neither a pass nor a fail.</summary>
    Skipped,
}
