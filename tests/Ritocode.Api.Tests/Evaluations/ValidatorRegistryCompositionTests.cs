using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Evaluations.Validators;

namespace Ritocode.Api.Tests.Evaluations;

/// <summary>
/// The validator registry as the host composes it. Nothing in the host resolves it before the
/// orchestrator of #17, so a registration that cannot be built would otherwise fail there first.
/// </summary>
public sealed class ValidatorRegistryCompositionTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public void TheRegistry_ResolvesFromTheHost_WithNoPluginRegisteredYet()
    {
        // No validator is registered until #19. When the first one is, this assertion is the one that
        // changes — deliberately, so the set of validators a host can run is visible in a test.
        var registry = api.Services.GetRequiredService<IValidatorPluginRegistry>();

        Assert.Empty(registry.Types);
    }
}
