using System.Text.Json.Nodes;
using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Evaluations.Tests;

/// <summary>Steps and runs built in memory, shaped the way <c>validator_config</c> and ADR 0006 §5 shape them.</summary>
internal static class Build
{
    public static ValidatorStepDefinition Step(string id = "unit-tests", string type = "exit-code", JsonObject? with = null) =>
        new(id, type, Weight: 100, Required: true, TimeoutSeconds: 120, with ?? new JsonObject());

    public static SandboxRunResult Run(SandboxRunOutcome outcome = SandboxRunOutcome.Completed, int exitCode = 0) =>
        new(
            outcome,
            exitCode,
            OomKilled: outcome == SandboxRunOutcome.ResourceExhausted,
            Duration: TimeSpan.FromSeconds(3),
            Stdout: new CapturedOutput(string.Empty, Truncated: false),
            Stderr: new CapturedOutput(string.Empty, Truncated: false),
            OutputDirectory: "/out");
}

/// <summary>
/// The smallest real plugin: it runs the declared command and passes when the command exits zero. It
/// exists in the tests only — the module registers no plugin before #19 — to prove the interface can be
/// implemented without a plugin ever starting a process.
/// </summary>
internal sealed class ExitCodeValidator : IValidatorPlugin
{
    public string Type => "exit-code";

    public Result<ValidatorRunPlan> Plan(ValidatorStepDefinition step) => ValidatorRunPlan.FromCommand(step);

    public Task<ValidatorResult> InterpretAsync(ValidatorStepDefinition step, SandboxRunResult run, CancellationToken cancellationToken) =>
        Task.FromResult(run.Outcome == SandboxRunOutcome.Completed
            ? ValidatorResult.Judged(step, run, passed: run.ExitCode == 0, summary: $"Exited with code {run.ExitCode}.")
            : ValidatorResult.NotCompleted(step, run));
}

/// <summary>A plugin answering any type a test names, doing nothing.</summary>
internal sealed class NamedPlugin(string type) : IValidatorPlugin
{
    public string Type => type;

    public Result<ValidatorRunPlan> Plan(ValidatorStepDefinition step) => throw new NotSupportedException();

    public Task<ValidatorResult> InterpretAsync(ValidatorStepDefinition step, SandboxRunResult run, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
