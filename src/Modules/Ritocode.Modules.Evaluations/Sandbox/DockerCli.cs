using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// The Docker CLI, started as a process with an argument vector — never through a shell, so no argument a
/// problem author or a submission wrote is ever interpreted on the worker host.
/// </summary>
internal sealed class DockerCli(string executable)
{
    /// <summary>Runs a short command to completion and returns what it printed.</summary>
    public async Task<DockerCliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = Start(arguments);

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StopQuietly(process);
            throw;
        }

        return new DockerCliResult(process.ExitCode, await stdout, await stderr);
    }

    /// <summary>Starts a command whose output the caller reads itself.</summary>
    public Process Start(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(startInfo)
                ?? throw new SandboxRunnerException($"The Docker CLI '{executable}' did not start.");
        }
        catch (Win32Exception exception)
        {
            throw new SandboxRunnerException($"The Docker CLI '{executable}' could not be started.", exception);
        }
    }

    /// <summary>Ends a CLI process the runner has given up on. The container is the runner's to remove, not this.</summary>
    public static void StopQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // It exited between the check and the kill.
        }
    }
}

/// <summary>What a short Docker CLI command answered.</summary>
internal sealed record DockerCliResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Succeeded => ExitCode == 0;
}
