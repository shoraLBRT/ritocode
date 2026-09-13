using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Evaluations.Validators;

/// <summary>
/// One kind of validator — compile, test, lint, patch scope — selected by the <c>type</c> a pipeline
/// step names. The seam that makes "two validators instead of four" an addition rather than a rewrite
/// (ADR 0005): a new kind is a new implementation registered beside the others, and nothing that runs a
/// pipeline changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A plugin never executes anything.</b> It says what the sandbox runner should run, and reads what
/// the run left behind. User code runs only inside a sandbox runner (AGENTS.md, ADR 0005's first
/// forbidden row), so a plugin that started a process — even "just" a build — would be the defect that
/// rule exists to prevent.
/// </para>
/// <para>
/// <b>A verdict is derived from a normalised projection.</b> The run's raw artifacts differ between two
/// runs of one submission; the verdict may not (ADR 0006 §6). <see cref="InterpretAsync"/> reports the
/// checks it found — for a test validator, the sorted test-name-to-outcome pairs — and never timings,
/// ordering or run identity.
/// </para>
/// <para>
/// A plugin reports what happened in its own step. What that is worth — how a weight turns a verdict
/// into points, and whether a failed required step stops the pipeline — is the orchestrator's and #20's,
/// so two plugins cannot score the same outcome two ways.
/// </para>
/// </remarks>
public interface IValidatorPlugin
{
    /// <summary>The pipeline <c>type</c> this plugin answers, matched ordinally.</summary>
    string Type { get; }

    /// <summary>
    /// What the runner should run for <paramref name="step"/>, read from its <c>with</c>.
    /// </summary>
    /// <returns>
    /// The plan, or a validation error naming the <c>with</c> field that cannot be run. Content is
    /// validated at ingest without interpreting <c>with</c>, so this is where a malformed one is found.
    /// </returns>
    Result<ValidatorRunPlan> Plan(ValidatorStepDefinition step);

    /// <summary>
    /// The verdict of one run of <paramref name="step"/>, from the runner's observation and what the run
    /// wrote to its output directory.
    /// </summary>
    /// <remarks>
    /// Called for every outcome, not only a completed run: a plugin may read its own output to explain a
    /// <see cref="SandboxRunOutcome.Crashed"/> to a person, which the runner is forbidden to guess at. It
    /// never turns a run that did not complete into a pass or a fail.
    /// </remarks>
    Task<ValidatorResult> InterpretAsync(ValidatorStepDefinition step, SandboxRunResult run, CancellationToken cancellationToken);
}
