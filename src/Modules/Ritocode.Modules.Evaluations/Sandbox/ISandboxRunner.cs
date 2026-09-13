namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// Runs one validator's command in a container under the flags of docs/adr/0006-sandbox-execution-model.md
/// §1, and reports what it observed. The only place user code executes.
/// </summary>
/// <remarks>
/// Declared here, ahead of its implementation in #21, because the pipeline that calls it is written
/// first. The orchestrator owns the deadline and the runner owns the container (ADR 0006): the runner
/// kills a container that outlives <see cref="SandboxRunRequest.Timeout"/> and reports it
/// <see cref="SandboxRunOutcome.TimedOut"/>, which only the one who issued the kill can know.
/// </remarks>
public interface ISandboxRunner
{
    Task<SandboxRunResult> RunAsync(SandboxRunRequest request, CancellationToken cancellationToken);
}

/// <summary>One validator run, as the orchestrator asks for it.</summary>
/// <remarks>
/// The image, its injected arguments and the resource limits are not here: per ADR 0006 §2 and §6 they
/// belong to the runner registry entry and are versioned with the image, which arrives with #22. They
/// join this record by addition, and nothing that builds a request today changes.
/// </remarks>
/// <param name="StepId">The pipeline step, for artifacts and logs.</param>
/// <param name="Command">The plugin's argument vector. The runner appends the image's arguments; it never runs a shell.</param>
/// <param name="WorkspaceDirectory">The frozen input tree on the worker host, mounted read-only.</param>
/// <param name="OutputDirectory">The writable output mount, shared by every step of one evaluation (ADR 0006 §4).</param>
/// <param name="Timeout">The step's deadline — its <c>timeout_seconds</c>, which the runner may lower and never raise.</param>
public sealed record SandboxRunRequest(
    string StepId,
    IReadOnlyList<string> Command,
    string WorkspaceDirectory,
    string OutputDirectory,
    TimeSpan Timeout);
