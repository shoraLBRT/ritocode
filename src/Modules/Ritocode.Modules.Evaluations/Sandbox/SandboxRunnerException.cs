namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// The runner could not run the container at all — the Docker CLI is missing, the daemon refused, the image
/// is not on the host. A fault of the evaluation host, never an outcome of the submission, so it is thrown
/// rather than reported as <see cref="SandboxRunOutcome.Crashed"/>: a person must not be told their code
/// crashed when it never ran.
/// </summary>
public sealed class SandboxRunnerException : Exception
{
    public SandboxRunnerException()
    {
    }

    public SandboxRunnerException(string message)
        : base(message)
    {
    }

    public SandboxRunnerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
