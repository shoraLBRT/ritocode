namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// What the sandbox runner observed about one validator's container, in the shape
/// docs/adr/0006-sandbox-execution-model.md §5 fixed. Declared here, ahead of the runner of #21,
/// because it is what a validator plugin interprets — and a plugin interface written against a result
/// that did not exist yet would be rewritten by the first runner.
/// </summary>
/// <param name="Outcome">How the container ended. Never whether the validator passed — that is the plugin's answer.</param>
/// <param name="ExitCode">The container's exit code; meaningful only when <paramref name="Outcome"/> is <see cref="SandboxRunOutcome.Completed"/>.</param>
/// <param name="OomKilled">The daemon's <c>.State.OOMKilled</c>: a reliable positive and an unreliable negative.</param>
/// <param name="Duration">Wall clock, runner-measured. Never part of a verdict: two runs of one submission differ here.</param>
/// <param name="Stdout">Captured standard output, truncated at a fixed cap.</param>
/// <param name="Stderr">Captured standard error, truncated at a fixed cap.</param>
/// <param name="OutputDirectory">The writable output mount on the worker host, for the plugin to read what the run produced.</param>
public sealed record SandboxRunResult(
    SandboxRunOutcome Outcome,
    int ExitCode,
    bool OomKilled,
    TimeSpan Duration,
    CapturedOutput Stdout,
    CapturedOutput Stderr,
    string OutputDirectory);

/// <summary>One captured stream, and whether the runner cut it short.</summary>
public sealed record CapturedOutput(string Text, bool Truncated);

/// <summary>How a validator's container ended, per ADR 0006 §5.</summary>
public enum SandboxRunOutcome
{
    /// <summary>The container ran to completion; <see cref="SandboxRunResult.ExitCode"/> is the validator's own answer.</summary>
    Completed,

    /// <summary>The orchestrator killed it on its deadline. Known only to whoever issued the kill.</summary>
    TimedOut,

    /// <summary>The kernel killed it for memory. Reported only when the daemon says so.</summary>
    ResourceExhausted,

    /// <summary>It died, and the runner could not attribute why — which does not mean it was not a resource problem.</summary>
    Crashed,
}
