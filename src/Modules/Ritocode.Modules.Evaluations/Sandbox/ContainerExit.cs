namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// How a container's end is read into an outcome, per docs/adr/0006-sandbox-execution-model.md §5. The only
/// place that decides it, and a pure function, so every row of the spike's table is a unit test.
/// </summary>
internal static class ContainerExit
{
    /// <summary>The conventional exit code of a process ended by signal <c>n</c> is <c>128 + n</c>.</summary>
    private const int SignalBase = 128;

    /// <summary>The highest signal number Linux has, real-time signals included.</summary>
    private const int HighestSignal = 64;

    /// <summary>Reads what the runner and the daemon observed into the outcome the runner reports.</summary>
    /// <param name="killedOnDeadline">The runner itself killed the container on its deadline.</param>
    /// <param name="exitCode">The daemon's <c>.State.ExitCode</c>.</param>
    /// <param name="oomKilled">The daemon's <c>.State.OOMKilled</c>.</param>
    /// <param name="stateError">The daemon's <c>.State.Error</c> — set when the command could not be started.</param>
    public static SandboxRunOutcome Classify(bool killedOnDeadline, int exitCode, bool oomKilled, string? stateError)
    {
        // Exit 137 is both the kernel and the deadline; only the one who issued the kill knows it was the deadline.
        if (killedOnDeadline)
        {
            return SandboxRunOutcome.TimedOut;
        }

        // A reliable positive. False proves nothing: a managed runtime aborts at 134 before the kernel acts.
        if (oomKilled)
        {
            return SandboxRunOutcome.ResourceExhausted;
        }

        // The command never started, or the process died by a signal: the container did not end with an answer
        // of its own, and the runner does not guess why — refining it is the validator's job, from its output.
        if (!string.IsNullOrEmpty(stateError) || exitCode is > SignalBase and <= SignalBase + HighestSignal)
        {
            return SandboxRunOutcome.Crashed;
        }

        return SandboxRunOutcome.Completed;
    }
}
