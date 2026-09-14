using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Evaluations.Sandbox;

namespace Ritocode.Api.Tests.Evaluations;

/// <summary>
/// The sandbox runner as the host composes it. Nothing in the host runs a step until the evaluation path is
/// wired after #20, so a registration that cannot be built would otherwise first fail there.
/// </summary>
public sealed class SandboxRunnerCompositionTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public void TheRunner_ResolvesFromTheHost_WithoutContactingDocker()
    {
        Assert.NotNull(api.Services.GetRequiredService<ISandboxRunner>());
    }

    [Fact]
    public void TheDockerCli_DefaultsToTheOneOnThePath()
    {
        var options = api.Services.GetRequiredService<IOptions<SandboxRunnerOptions>>().Value;

        Assert.Equal("docker", options.DockerExecutable);
    }
}
