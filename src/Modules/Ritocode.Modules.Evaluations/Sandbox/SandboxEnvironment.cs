namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// The evaluation environment one run happens in: the image, the arguments it appends to every command,
/// and the resource limits — the fields of a runner registry entry, per
/// docs/adr/0006-sandbox-execution-model.md §2 and §6.
/// </summary>
/// <remarks>
/// The registry that chooses an environment for a problem version's language arrives with #22. The runner
/// only reads the entry it is handed, so it stays language-agnostic: a second language is a second value of
/// this record, never a branch in the runner. The offline package cache is inside the image and not a field
/// here — the runner has nothing to do with it.
/// </remarks>
/// <param name="Image">The image reference. The runner never pulls it: an evaluation does not download anything.</param>
/// <param name="AppendedArguments">Precedence-winning arguments appended after the declared command (ADR 0006 §2).</param>
/// <param name="Limits">
/// The resource limits. Part of the determinism contract rather than an operational knob — the runtime reads
/// its own cgroup — so they are versioned with the image and never tuned per run (ADR 0006 §6).
/// </param>
public sealed record SandboxEnvironment(
    string Image,
    IReadOnlyList<string> AppendedArguments,
    SandboxLimits Limits);

/// <summary>The resource limits of an environment. Swap is always equal to memory: a run has no swap.</summary>
/// <param name="Cpus">Docker's <c>--cpus</c>.</param>
/// <param name="MemoryBytes">Docker's <c>--memory</c>, and <c>--memory-swap</c> with it.</param>
/// <param name="Pids">Docker's <c>--pids-limit</c>.</param>
public sealed record SandboxLimits(decimal Cpus, long MemoryBytes, int Pids);
