using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Evaluations.Sandbox;

namespace Ritocode.Modules.Evaluations.Tests.Sandbox;

/// <summary>
/// The runner against a real Docker daemon. The flags are only a guarantee if they hold inside a container,
/// so each is observed from inside one rather than read from the argument vector — and every outcome of
/// ADR 0006 §5 is produced for real, not scripted. A busybox image is enough: the runner is language-agnostic,
/// and the .NET image is #22's.
/// </summary>
/// <remarks>The tests in one class run one after another, which the container-leak assertions rely on.</remarks>
public sealed class DockerSandboxRunnerTests(BusyboxImage image) : IClassFixture<BusyboxImage>, IDisposable
{
    private const long MiB = 1024 * 1024;

    private readonly Mounts _mounts = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _mounts.Dispose();

    [Fact]
    public async Task ACommandThatExits_IsCompleted_WithItsOwnExitCode_AndItsStreamsApart()
    {
        var run = await Runner().RunAsync(Request(["sh", "-c", "echo out; echo err >&2; exit 3"]), Token);

        Assert.Equal(SandboxRunOutcome.Completed, run.Outcome);
        Assert.Equal(3, run.ExitCode);
        Assert.False(run.OomKilled);
        Assert.Equal(new CapturedOutput("out\n", Truncated: false), run.Stdout);
        Assert.Equal(new CapturedOutput("err\n", Truncated: false), run.Stderr);
        Assert.Equal(_mounts.Output, run.OutputDirectory);
    }

    [Fact]
    public async Task EveryContainmentFlag_HoldsInsideTheContainer()
    {
        const string probe = """
            echo "uid=$(id -u)"
            if touch /probe 2>/dev/null; then echo root=writable; else echo root=read-only; fi
            if touch /work/probe 2>/dev/null; then echo work=writable; else echo work=read-only; fi
            if echo artifact > /out/artifact.txt; then echo out=writable; else echo out=read-only; fi
            echo "interfaces=$(ls /sys/class/net | tr '\n' ' ')"
            echo "capeff=$(grep CapEff /proc/self/status | cut -f2)"
            echo "nonewprivs=$(grep NoNewPrivs /proc/self/status | cut -f2)"
            if [ -e /var/run/docker.sock ]; then echo socket=visible; else echo socket=absent; fi
            echo "pids=$(cat /sys/fs/cgroup/pids.max 2>/dev/null || cat /sys/fs/cgroup/pids/pids.max)"
            echo "memory=$(cat /sys/fs/cgroup/memory.max 2>/dev/null || cat /sys/fs/cgroup/memory/memory.limit_in_bytes)"
            """;

        var run = await Runner().RunAsync(Request(["sh", "-c", probe], Busybox(memoryBytes: 256 * MiB, pids: 64)), Token);

        Assert.Equal(SandboxRunOutcome.Completed, run.Outcome);
        var seen = run.Stdout.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1].Trim(), StringComparer.Ordinal);

        Assert.Equal("10001", seen["uid"]);
        Assert.Equal("read-only", seen["root"]);
        Assert.Equal("read-only", seen["work"]);
        Assert.Equal("writable", seen["out"]);
        Assert.Equal("lo", seen["interfaces"]);
        Assert.Equal("0000000000000000", seen["capeff"]);
        Assert.Equal("1", seen["nonewprivs"]);
        Assert.Equal("absent", seen["socket"]);
        Assert.Equal("64", seen["pids"]);
        Assert.Equal((256 * MiB).ToString(System.Globalization.CultureInfo.InvariantCulture), seen["memory"]);

        // The input tree is untouched, and what the run wrote is on the host after the container is gone.
        Assert.Equal(["input.txt"], Directory.EnumerateFileSystemEntries(_mounts.Workspace).Select(Path.GetFileName));
        Assert.Equal("frozen\n", await File.ReadAllTextAsync(Path.Combine(_mounts.Workspace, "input.txt"), Token));
        Assert.Equal("artifact\n", await File.ReadAllTextAsync(Path.Combine(_mounts.Output, "artifact.txt"), Token));
    }

    [Fact]
    public async Task TheDeclaredCommand_RunsWithTheEnvironmentsArgumentsAppended_AndThroughNoShell()
    {
        var environment = Busybox() with { AppendedArguments = ["appended"] };

        var run = await Runner().RunAsync(Request(["echo", "declared; touch /out/injected"], environment), Token);

        Assert.Equal(SandboxRunOutcome.Completed, run.Outcome);
        Assert.Equal("declared; touch /out/injected appended\n", run.Stdout.Text);
        Assert.False(File.Exists(Path.Combine(_mounts.Output, "injected")));
    }

    [Fact]
    public async Task AContainerPastItsDeadline_IsKilled_ReportedTimedOut_AndRemoved()
    {
        var step = UniqueStep();

        var run = await Runner().RunAsync(Request(["sleep", "300"], timeout: TimeSpan.FromSeconds(2), step: step), Token);

        Assert.Equal(SandboxRunOutcome.TimedOut, run.Outcome);
        Assert.InRange(run.Duration, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));
        Assert.Empty(await image.ContainersLabelledAsync(step, Token));
    }

    [Fact]
    public async Task AContainerTheKernelKillsForMemory_IsResourceExhausted()
    {
        // tmpfs pages are charged to the memory cgroup, so filling a tmpfs wider than the limit makes the kernel,
        // not a runtime, do the killing — the spike's kernel-oom probe.
        var run = await Runner().RunAsync(
            Request(["sh", "-c", "head -c 268435456 /dev/zero > /tmp/fill"], Busybox(memoryBytes: 64 * MiB)),
            Token);

        Assert.Equal(SandboxRunOutcome.ResourceExhausted, run.Outcome);
        Assert.True(run.OomKilled);
    }

    [Fact]
    public async Task AProcessEndedByASignal_IsCrashed()
    {
        var run = await Runner().RunAsync(Request(["sh", "-c", "sh -c 'kill -KILL $$'; exit $?"]), Token);

        Assert.Equal(SandboxRunOutcome.Crashed, run.Outcome);
        Assert.Equal(137, run.ExitCode);
        Assert.False(run.OomKilled);
    }

    [Fact]
    public async Task ACommandTheImageDoesNotHave_IsCrashed()
    {
        var run = await Runner().RunAsync(Request(["ritocode-no-such-command"]), Token);

        Assert.Equal(SandboxRunOutcome.Crashed, run.Outcome);
    }

    [Fact]
    public async Task OutputPastTheCap_IsCutAtTheCap_AndFlagged()
    {
        var run = await Runner().RunAsync(Request(["sh", "-c", "yes aaaaaaa | head -c 1000000"]), Token);

        Assert.Equal(SandboxRunOutcome.Completed, run.Outcome);
        Assert.True(run.Stdout.Truncated);
        Assert.Equal(DockerSandboxRunner.MaxCapturedCharacters, run.Stdout.Text.Length);
        Assert.False(run.Stderr.Truncated);
    }

    [Fact]
    public async Task ACancelledRun_KillsAndRemovesItsContainer()
    {
        var step = UniqueStep();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Runner().RunAsync(Request(["sleep", "300"], step: step), cancellation.Token));

        Assert.Empty(await image.ContainersLabelledAsync(step, Token));
    }

    [Fact]
    public async Task AnImageNotOnTheHost_IsAFaultOfTheHost_NotAnOutcome()
    {
        var absent = Busybox() with { Image = "ritocode-sandbox-absent:never-built" };

        await Assert.ThrowsAsync<SandboxRunnerException>(() => Runner().RunAsync(Request(["true"], absent), Token));
    }

    private static DockerSandboxRunner Runner() =>
        new(Options.Create(new SandboxRunnerOptions()), TimeProvider.System, NullLogger<DockerSandboxRunner>.Instance);

    private static SandboxEnvironment Busybox(long memoryBytes = 256 * MiB, int pids = 64) =>
        new(BusyboxImage.Reference, [], new SandboxLimits(Cpus: 1m, memoryBytes, pids));

    private SandboxRunRequest Request(
        string[] command,
        SandboxEnvironment? environment = null,
        TimeSpan? timeout = null,
        string step = "probe") =>
        new(step, environment ?? Busybox(), command, _mounts.Workspace, _mounts.Output, timeout ?? TimeSpan.FromSeconds(60));

    private static string UniqueStep() => $"leak-{Guid.NewGuid():N}";

    /// <summary>A workspace holding one file and an empty output directory, both reachable by the container's user.</summary>
    private sealed class Mounts : IDisposable
    {
        private const UnixFileMode Readable =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

        private const UnixFileMode Writable = Readable | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ritocode-sandbox-");

        public Mounts()
        {
            Workspace = _root.CreateSubdirectory("work").FullName;
            Output = _root.CreateSubdirectory("out").FullName;
            File.WriteAllText(Path.Combine(Workspace, "input.txt"), "frozen\n");

            // The container runs as uid 10001, which owns neither directory on a Linux host.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Workspace, Readable);
                File.SetUnixFileMode(Output, Writable);
            }
        }

        public string Workspace { get; }

        public string Output { get; }

        public void Dispose() => _root.Delete(recursive: true);
    }
}

/// <summary>
/// Puts the test image on the host once per class. The runner never pulls, on purpose, so the image has to be
/// there before a run — the same obligation a deployment has for its runner image.
/// </summary>
public sealed class BusyboxImage : IAsyncLifetime
{
    public const string Reference = "busybox:1.37.0";

    private readonly DockerCli _docker = new("docker");

    public async ValueTask InitializeAsync()
    {
        if ((await _docker.RunAsync(["image", "inspect", Reference], CancellationToken.None)).Succeeded)
        {
            return;
        }

        var pulled = await _docker.RunAsync(["pull", Reference], CancellationToken.None);

        if (!pulled.Succeeded)
        {
            throw new InvalidOperationException($"Could not pull {Reference}: {pulled.Stderr}");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Every container, running or not, the runner labelled with this step.</summary>
    public async Task<IReadOnlyList<string>> ContainersLabelledAsync(string step, CancellationToken cancellationToken)
    {
        var listed = await _docker.RunAsync(
            ["ps", "--all", "--quiet", "--filter", $"label={DockerSandboxRunner.StepLabel}={step}"],
            cancellationToken);

        Assert.True(listed.Succeeded, listed.Stderr);

        return listed.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
