using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>
/// The slice's sandbox runner: <c>docker</c> from the worker host, one container per validator run, under
/// the flag set of docs/adr/0006-sandbox-execution-model.md §1 (#21).
/// </summary>
/// <remarks>
/// <para>
/// A run is create, start attached, wait with the deadline, inspect, remove. <c>docker run</c> has no
/// timeout of its own and its exit code cannot say why a container died, so the runner waits itself, issues
/// the kill on expiry and records that it did; the daemon's state supplies the rest, read by
/// <see cref="ContainerExit.Classify"/>. The container is removed whatever happened, cancellation included.
/// </para>
/// <para>
/// Output is read from the attached streams and capped there, and the container has no log driver: a
/// submission printing without end costs the runner a pipe it keeps draining, never the daemon's disk.
/// </para>
/// </remarks>
internal sealed partial class DockerSandboxRunner(
    IOptions<SandboxRunnerOptions> options,
    TimeProvider timeProvider,
    ILogger<DockerSandboxRunner> logger) : ISandboxRunner
{
    /// <summary>How much of each stream a result carries. What is past it is read and discarded.</summary>
    public const int MaxCapturedCharacters = 262_144;

    /// <summary>The label naming the step a container runs, so a leaked container can be traced to its run.</summary>
    public const string StepLabel = "ritocode.sandbox.step";

    /// <summary>The unprivileged user every run is, whatever the image says (ADR 0006 §1).</summary>
    public const string ContainerUser = "10001:10001";

    /// <summary>Docker refuses a memory limit below 6 MiB.</summary>
    private const long MinimumMemoryBytes = 6 * 1024 * 1024;

    /// <summary>How long the attached CLI gets to notice a killed container before the runner stops waiting on it.</summary>
    private static readonly TimeSpan GraceAfterKill = TimeSpan.FromSeconds(30);

    private readonly DockerCli _docker = new(options.Value.DockerExecutable);

    public async Task<SandboxRunResult> RunAsync(SandboxRunRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var name = $"ritocode-sandbox-{Guid.NewGuid():N}";

        try
        {
            await CreateAsync(name, request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancelled while Docker was creating it: the container may exist.
            await RemoveAsync(name);
            throw;
        }

        try
        {
            return await RunCreatedAsync(name, request, cancellationToken);
        }
        finally
        {
            await RemoveAsync(name);
        }
    }

    /// <summary>The <c>docker create</c> argument vector for a run. Everything a container is allowed is in here.</summary>
    internal static IReadOnlyList<string> CreateArguments(string name, SandboxRunRequest request)
    {
        var limits = request.Environment.Limits;
        var memory = limits.MemoryBytes.ToString(CultureInfo.InvariantCulture);

        List<string> arguments =
        [
            "create",
            "--name", name,
            "--label", $"{StepLabel}={request.StepId}",

            // An evaluation downloads nothing: an image that is not on the host is a fault of the host.
            "--pull", "never",

            // The streams are read attached and capped by the runner; a log driver would keep all of it.
            "--log-driver", "none",

            // Containment, ADR 0006 §1. Nothing inside the container can change any of it.
            "--network", "none",
            "--read-only",
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            "--tmpfs", "/tmp:rw,noexec,nosuid,size=512m",
            "--user", ContainerUser,

            // The environment's limits, versioned with its image (§6). No swap: memory is the limit.
            "--cpus", limits.Cpus.ToString(CultureInfo.InvariantCulture),
            "--memory", memory,
            "--memory-swap", memory,
            "--pids-limit", limits.Pids.ToString(CultureInfo.InvariantCulture),

            // A read-only input tree and a shared writable output mount (§4).
            "--mount", $"type=bind,source={request.WorkspaceDirectory},target=/work,readonly",
            "--mount", $"type=bind,source={request.OutputDirectory},target=/out",
            "--workdir", "/work",

            // Docker stops reading its own flags at the image, so nothing after it is an option to Docker.
            request.Environment.Image,
        ];

        arguments.AddRange(request.Command);
        arguments.AddRange(request.Environment.AppendedArguments);

        return arguments;
    }

    private async Task CreateAsync(string name, SandboxRunRequest request, CancellationToken cancellationToken)
    {
        var created = await _docker.RunAsync(CreateArguments(name, request), cancellationToken);

        if (!created.Succeeded)
        {
            throw new SandboxRunnerException(
                $"Docker did not create the container for step '{request.StepId}' on image '{request.Environment.Image}': {created.Stderr.Trim()}");
        }
    }

    private async Task<SandboxRunResult> RunCreatedAsync(string name, SandboxRunRequest request, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();

        using var attached = _docker.Start(["start", "--attach", name]);

        var stdout = CaptureAsync(attached.StandardOutput);
        var stderr = CaptureAsync(attached.StandardError);

        var killedOnDeadline = await WaitWithDeadlineAsync(name, attached, request.Timeout, cancellationToken);
        var duration = timeProvider.GetElapsedTime(started);

        var state = await InspectAsync(name, cancellationToken);
        var outcome = ContainerExit.Classify(killedOnDeadline, state.ExitCode, state.OomKilled, state.Error);

        return new SandboxRunResult(
            outcome,
            state.ExitCode,
            state.OomKilled,
            duration,
            await stdout,
            await stderr,
            request.OutputDirectory);
    }

    /// <summary>Waits for the container to end, killing it on the deadline. True when the runner issued that kill.</summary>
    private async Task<bool> WaitWithDeadlineAsync(string name, Process attached, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            await attached.WaitForExitAsync(deadline.Token);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A kill that fails found the container already stopped: it ended on its own, at the deadline.
            var killed = await KillAsync(name);
            await WaitAfterKillAsync(attached);
            return killed;
        }
        catch (OperationCanceledException)
        {
            await KillAsync(name);
            DockerCli.StopQuietly(attached);
            throw;
        }
    }

    private async Task<bool> KillAsync(string name)
    {
        var killed = await _docker.RunAsync(["kill", "--signal", "KILL", name], CancellationToken.None);
        return killed.Succeeded;
    }

    private static async Task WaitAfterKillAsync(Process attached)
    {
        using var grace = new CancellationTokenSource(GraceAfterKill);

        try
        {
            await attached.WaitForExitAsync(grace.Token);
        }
        catch (OperationCanceledException)
        {
            DockerCli.StopQuietly(attached);
        }
    }

    private async Task<ContainerState> InspectAsync(string name, CancellationToken cancellationToken)
    {
        var inspected = await _docker.RunAsync(["inspect", "--format", "{{json .State}}", name], cancellationToken);

        if (!inspected.Succeeded)
        {
            throw new SandboxRunnerException($"Docker could not inspect container '{name}': {inspected.Stderr.Trim()}");
        }

        using var json = JsonDocument.Parse(inspected.Stdout);
        var state = json.RootElement;

        return new ContainerState(
            state.GetProperty("ExitCode").GetInt32(),
            state.GetProperty("OOMKilled").GetBoolean(),
            state.TryGetProperty("Error", out var error) ? error.GetString() : null);
    }

    private async Task RemoveAsync(string name)
    {
        try
        {
            var removed = await _docker.RunAsync(["rm", "--force", "--volumes", name], CancellationToken.None);

            if (!removed.Succeeded)
            {
                LogContainerNotRemoved(logger, name, removed.Stderr.Trim());
            }
        }
        catch (SandboxRunnerException exception)
        {
            LogContainerNotRemoved(logger, name, exception.Message);
        }
    }

    /// <summary>Reads a stream to its end, keeping the first <see cref="MaxCapturedCharacters"/>.</summary>
    private static async Task<CapturedOutput> CaptureAsync(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[8192];
        var truncated = false;
        int read;

        // Past the cap the stream is still drained, or a container writing without end would block on a full pipe.
        while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0)
        {
            var room = MaxCapturedCharacters - text.Length;

            if (read > room)
            {
                text.Append(buffer, 0, room);
                truncated = true;
            }
            else
            {
                text.Append(buffer, 0, read);
            }
        }

        return new CapturedOutput(text.ToString(), truncated);
    }

    private static void Validate(SandboxRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StepId))
        {
            throw new ArgumentException("A run names its step.", nameof(request));
        }

        if (request.Environment is not { } environment
            || string.IsNullOrWhiteSpace(environment.Image)
            || environment.Image.StartsWith('-')
            || environment.AppendedArguments is null)
        {
            throw new ArgumentException("A run names an environment with an image.", nameof(request));
        }

        if (environment.Limits is not { Cpus: > 0, MemoryBytes: >= MinimumMemoryBytes, Pids: > 0 })
        {
            throw new ArgumentException("An environment's cpus and pids are positive and its memory at least 6 MiB.", nameof(request));
        }

        if (request.Command is not { Count: > 0 } || request.Command.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("A run has a command of non-empty arguments.", nameof(request));
        }

        RequireMountableDirectory(request.WorkspaceDirectory, "workspace");
        RequireMountableDirectory(request.OutputDirectory, "output");

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Timeout, "A run has a positive deadline.");
        }
    }

    private static void RequireMountableDirectory(string path, string role)
    {
        // A mount is written as comma-separated fields, so a comma or a quote would change what is mounted.
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path)
            || path.Contains(',', StringComparison.Ordinal)
            || path.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException($"The {role} directory is an absolute path with no comma or quote: '{path}'.", nameof(path));
        }

        if (!Directory.Exists(path))
        {
            throw new ArgumentException($"The {role} directory does not exist: '{path}'.", nameof(path));
        }
    }

    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Warning,
        Message = "Sandbox container {Container} was not removed: {Reason}")]
    private static partial void LogContainerNotRemoved(ILogger logger, string container, string reason);

    private sealed record ContainerState(int ExitCode, bool OomKilled, string? Error);
}
