using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Evaluations.Sandbox;

namespace Ritocode.Modules.Evaluations.Tests.Sandbox;

/// <summary>
/// What the runner asks Docker for, and what it refuses before asking. No Docker daemon is needed here:
/// the flags are read from the argument vector, and a refusal is proved against a CLI that does not exist,
/// so a request that reached Docker would fail differently.
/// </summary>
public sealed class DockerSandboxRunnerArgumentsTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ritocode-sandbox-args-");

    private string Workspace => _root.CreateSubdirectory("work").FullName;

    private string Output => _root.CreateSubdirectory("out").FullName;

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public void EveryContainmentFlag_OfAdr0006_IsAskedFor()
    {
        var arguments = DockerSandboxRunner.CreateArguments("sandbox", Request());

        AssertOption(arguments, "--network", "none");
        Assert.Contains("--read-only", arguments);
        AssertOption(arguments, "--cap-drop", "ALL");
        AssertOption(arguments, "--security-opt", "no-new-privileges");
        AssertOption(arguments, "--tmpfs", "/tmp:rw,noexec,nosuid,size=512m");
        AssertOption(arguments, "--user", "10001:10001");
        AssertOption(arguments, "--pull", "never");
        AssertOption(arguments, "--log-driver", "none");
        AssertOption(arguments, "--workdir", "/work");
    }

    [Fact]
    public void TheLimits_AreTheEnvironments_WithNoSwap()
    {
        var arguments = DockerSandboxRunner.CreateArguments("sandbox", Request());

        AssertOption(arguments, "--cpus", "2");
        AssertOption(arguments, "--memory", "2147483648");
        AssertOption(arguments, "--memory-swap", "2147483648");
        AssertOption(arguments, "--pids-limit", "256");
    }

    [Fact]
    public void TheWorkspace_IsMountedReadOnly_AndTheOutputWritable()
    {
        var request = Request();

        var mounts = Values(DockerSandboxRunner.CreateArguments("sandbox", request), "--mount");

        Assert.Equal(
            [
                $"type=bind,source={request.WorkspaceDirectory},target=/work,readonly",
                $"type=bind,source={request.OutputDirectory},target=/out",
            ],
            mounts);
    }

    [Fact]
    public void TheImage_IsFollowedByTheDeclaredCommand_ThenTheEnvironmentsArguments()
    {
        var request = Request(command: ["dotnet", "test", "--no-build"]);

        var arguments = DockerSandboxRunner.CreateArguments("sandbox", request);

        Assert.Equal(
            ["ritocode-runner:csharp", "dotnet", "test", "--no-build", "--artifacts-path", "/out"],
            arguments.TakeLast(6));
    }

    [Fact]
    public void TheContainer_IsLabelledWithItsStep()
    {
        var arguments = DockerSandboxRunner.CreateArguments("sandbox", Request());

        AssertOption(arguments, "--name", "sandbox");
        AssertOption(arguments, "--label", "ritocode.sandbox.step=unit-tests");
    }

    [Fact]
    public async Task ARequestThatCannotBeMounted_IsRefusedBeforeDockerIsAsked()
    {
        var runner = RunnerWithoutDocker();
        var missing = Path.Combine(_root.FullName, "missing");
        var comma = _root.CreateSubdirectory("a,b").FullName;

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(Request() with { WorkspaceDirectory = "relative/work" }, Token));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(Request() with { WorkspaceDirectory = missing }, Token));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(Request() with { OutputDirectory = comma }, Token));
    }

    [Fact]
    public async Task ARequestWithNothingSafeToRun_IsRefusedBeforeDockerIsAsked()
    {
        var runner = RunnerWithoutDocker();

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(Request(command: []), Token));
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(Request(command: ["echo", ""]), Token));
        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(Request() with { Environment = Build.Environment with { Image = "--privileged" } }, Token));
        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(Request() with { Environment = Build.Environment with { Limits = new SandboxLimits(1m, 1024, 64) } }, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync(Request() with { Timeout = TimeSpan.Zero }, Token));
    }

    [Fact]
    public async Task ADockerCliThatCannotBeStarted_IsAFaultOfTheHost_NotAnOutcome()
    {
        await Assert.ThrowsAsync<SandboxRunnerException>(() => RunnerWithoutDocker().RunAsync(Request(), Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SandboxRunRequest Request(string[]? command = null) =>
        new("unit-tests", Build.Environment, command ?? ["dotnet", "build"], Workspace, Output, TimeSpan.FromSeconds(30));

    private static DockerSandboxRunner RunnerWithoutDocker() =>
        new(
            Options.Create(new SandboxRunnerOptions { DockerExecutable = "ritocode-no-such-docker-cli" }),
            TimeProvider.System,
            NullLogger<DockerSandboxRunner>.Instance);

    private static void AssertOption(IReadOnlyList<string> arguments, string option, string value) =>
        Assert.Contains(value, Values(arguments, option));

    private static List<string> Values(IReadOnlyList<string> arguments, string option) =>
        [.. arguments.Select((argument, index) => (argument, index))
            .Where(pair => pair.argument == option && pair.index + 1 < arguments.Count)
            .Select(pair => arguments[pair.index + 1])];
}
