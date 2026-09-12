using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ritocode.Shared.Identity;

namespace Ritocode.Shared.Tests.Identity;

/// <summary>
/// The settings behind the seeded development identity, validated where a misconfiguration is
/// cheap to fix — at startup, rather than at the first workspace that cannot find its user.
/// </summary>
public sealed class DevelopmentIdentityOptionsTests
{
    [Fact]
    public void ByDefault_TheDevelopmentIdentityIsOff()
    {
        // Enabled, this authenticates every request as one fixed user. Defaulting it on would make
        // a deployment that forgot to configure authentication open rather than closed.
        var options = Resolve([]);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void TheDefaultIdentifier_IsFixedRatherThanGenerated()
    {
        // A new identifier per start would orphan every workspace and submission created under the
        // previous one, and nothing would report it.
        Assert.Equal(new DevelopmentIdentityOptions().UserId, Resolve([]).UserId);
        Assert.NotEqual(Guid.Empty, new DevelopmentIdentityOptions().UserId);
    }

    [Fact]
    public void AnEmptyIdentifier_IsRejectedWhenEnabled()
    {
        var exception = Assert.Throws<OptionsValidationException>(() => Resolve(new Dictionary<string, string?>
        {
            [$"{DevelopmentIdentityOptions.SectionName}:Enabled"] = "true",
            [$"{DevelopmentIdentityOptions.SectionName}:UserId"] = Guid.Empty.ToString(),
        }));

        Assert.Contains("UserId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyIdentifier_IsHarmlessWhenDisabled()
    {
        // Nothing asserts the identity, so nothing depends on it. Failing here would make a
        // production host refuse to start over a setting it does not use.
        var options = Resolve(new Dictionary<string, string?>
        {
            [$"{DevelopmentIdentityOptions.SectionName}:Enabled"] = "false",
            [$"{DevelopmentIdentityOptions.SectionName}:UserId"] = Guid.Empty.ToString(),
        });

        Assert.Equal(Guid.Empty, options.UserId);
    }

    [Fact]
    public void AnEmailThatIsNotAnEmail_IsRejected()
    {
        // users.email is unique and 320 characters wide, and the seeder writes this value straight
        // into it.
        Assert.Throws<OptionsValidationException>(() => Resolve(new Dictionary<string, string?>
        {
            [$"{DevelopmentIdentityOptions.SectionName}:Enabled"] = "true",
            [$"{DevelopmentIdentityOptions.SectionName}:Email"] = "developer",
        }));
    }

    [Fact]
    public void AConfiguredIdentity_IsBoundWhole()
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            [$"{DevelopmentIdentityOptions.SectionName}:Enabled"] = "true",
            [$"{DevelopmentIdentityOptions.SectionName}:UserId"] = "0199aa00-0000-7000-8000-00000000beef",
            [$"{DevelopmentIdentityOptions.SectionName}:Email"] = "someone@example.com",
            [$"{DevelopmentIdentityOptions.SectionName}:Username"] = "someone",
        });

        Assert.True(options.Enabled);
        Assert.Equal(new Guid("0199aa00-0000-7000-8000-00000000beef"), options.UserId);
        Assert.Equal("someone@example.com", options.Email);
        Assert.Equal("someone", options.Username);
    }

    private static DevelopmentIdentityOptions Resolve(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection()
            .AddRitocodeIdentity(configuration)
            .BuildServiceProvider();

        return services.GetRequiredService<IOptions<DevelopmentIdentityOptions>>().Value;
    }
}
