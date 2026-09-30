using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Tests.Scoring;

/// <summary>The numbers of SPEC §5.2 are configuration, with the specification's values as defaults.</summary>
public sealed class ScoringParametersTests
{
    [Fact]
    public void WithoutConfiguration_TheSpecificationsNumbersApply()
    {
        var parameters = Resolve([]);

        Assert.Equal((10, 3, 3, 5, 2), (parameters.Found, parameters.Missed, parameters.Extra, parameters.TreatmentMatched, parameters.WrongLeaf));
    }

    [Fact]
    public void Configuration_OverridesAParameter()
    {
        var parameters = Resolve(new() { ["Attempts:Scoring:Missed"] = "5" });

        Assert.Equal(5, parameters.Missed);
        Assert.Equal(10, parameters.Found);
    }

    [Fact]
    public void ANegativeParameter_FailsValidation()
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(new() { ["Attempts:Scoring:Extra"] = "-3" }));
    }

    private static ScoringParameters Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();

        new AttemptsModule().RegisterServices(services, configuration);

        return services.BuildServiceProvider().GetRequiredService<IOptions<ScoringParameters>>().Value;
    }
}
