using Ritocode.Modules.Evaluations.Sandbox;

namespace Ritocode.Modules.Evaluations.Tests.Sandbox;

/// <summary>
/// How a container's end is read, row by row against what the sandbox spike observed
/// (spikes/sandbox-execution/README.md, "the exit code does not say why a container died").
/// </summary>
public sealed class ContainerExitTests
{
    [Theory]
    [InlineData(false, 0, false, null, SandboxRunOutcome.Completed)] // passed
    [InlineData(false, 1, false, null, SandboxRunOutcome.Completed)] // a validator failing normally
    [InlineData(false, 2, false, null, SandboxRunOutcome.Completed)] // the pid limit, as a shell reports a failed fork
    [InlineData(false, 128, false, null, SandboxRunOutcome.Completed)] // 128 is no signal
    [InlineData(false, 255, false, null, SandboxRunOutcome.Completed)] // past every signal: the program's own answer
    [InlineData(false, 134, false, null, SandboxRunOutcome.Crashed)] // a managed OutOfMemoryException: SIGABRT, OOMKilled false
    [InlineData(false, 137, false, null, SandboxRunOutcome.Crashed)] // SIGKILL from something that was not the runner
    [InlineData(false, 192, false, null, SandboxRunOutcome.Crashed)] // the highest real-time signal
    [InlineData(false, 127, false, "exec: \"missing\": executable file not found", SandboxRunOutcome.Crashed)] // never started
    [InlineData(false, 137, true, null, SandboxRunOutcome.ResourceExhausted)] // the kernel's OOM killer
    [InlineData(true, 137, false, null, SandboxRunOutcome.TimedOut)] // the runner's own kill on the deadline
    [InlineData(true, 137, true, null, SandboxRunOutcome.TimedOut)] // killed on the deadline while out of memory: the kill is known
    public void TheOutcome_IsReadFromWhatTheRunnerAndTheDaemonObserved(
        bool killedOnDeadline,
        int exitCode,
        bool oomKilled,
        string? stateError,
        SandboxRunOutcome expected)
    {
        Assert.Equal(expected, ContainerExit.Classify(killedOnDeadline, exitCode, oomKilled, stateError));
    }
}
