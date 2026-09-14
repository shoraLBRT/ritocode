namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// Runs one validator's command in a container under the flags of docs/adr/0006-sandbox-execution-model.md
/// §1, and reports what it observed. The only place user code executes.
/// </summary>
/// <remarks>
/// The orchestrator owns the deadline and the runner owns the container (ADR 0006): the runner kills a
/// container that outlives <see cref="SandboxRunRequest.Timeout"/> and reports it
/// <see cref="SandboxRunOutcome.TimedOut"/>, which only the one who issued the kill can know. A run the
/// runner could not even start — no daemon, no such image — is not an outcome of the submission, and throws
/// <see cref="SandboxRunnerException"/> instead. Implemented by <see cref="DockerSandboxRunner"/> (#21).
/// </remarks>
public interface ISandboxRunner
{
    Task<SandboxRunResult> RunAsync(SandboxRunRequest request, CancellationToken cancellationToken);
}

/// <summary>One validator run, as the orchestrator asks for it.</summary>
/// <param name="StepId">The pipeline step, for artifacts and logs.</param>
/// <param name="Environment">The image, its appended arguments and its limits — a runner registry entry (#22).</param>
/// <param name="Command">The plugin's argument vector. The runner appends the environment's arguments; it never runs a shell.</param>
/// <param name="WorkspaceDirectory">
/// The frozen input tree on the worker host, as an absolute path, mounted read-only at <c>/work</c>. It has to
/// be readable by the container's user, uid 10001.
/// </param>
/// <param name="OutputDirectory">
/// The writable output mount at <c>/out</c>, shared by every step of one evaluation (ADR 0006 §4), as an
/// absolute path. It has to be writable by uid 10001.
/// </param>
/// <param name="Timeout">The step's deadline — its <c>timeout_seconds</c>.</param>
public sealed record SandboxRunRequest(
    string StepId,
    SandboxEnvironment Environment,
    IReadOnlyList<string> Command,
    string WorkspaceDirectory,
    string OutputDirectory,
    TimeSpan Timeout);
