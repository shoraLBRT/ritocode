using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Tests.Contracts;

/// <summary>
/// The cross-module contracts as a consumer module will receive them: resolved from the host's real
/// composition, in a scope, over a migrated database.
/// </summary>
/// <remarks>
/// The architecture tests prove each contract is registered once by its owner; they never resolve
/// one. This is where a registration that compiles and cannot be constructed — a lifetime mismatch,
/// a dependency nobody registered — fails before the first workspace endpoint does.
/// </remarks>
public sealed class ContractResolutionTests(TestApi api) : IClassFixture<TestApi>
{
    private static readonly DevelopmentIdentityOptions SeededIdentity = new();

    [Fact]
    public async Task UserLookup_FindsTheSeededDevelopmentIdentity()
    {
        // The two halves of the identity seam agreeing, seen from the side #10 will see it from: the
        // user the claim names is a user another module can confirm exists.
        await using var scope = api.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IUserLookup>();

        var user = await lookup.FindAsync(SeededIdentity.UserId, TestContext.Current.CancellationToken);

        Assert.NotNull(user);
        Assert.Equal(SeededIdentity.UserId, user.Id);
        Assert.Equal(SeededIdentity.Username, user.Username);
    }

    [Fact]
    public async Task UserLookup_AnswersNullForAUserWithNoRow()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IUserLookup>();

        var user = await lookup.FindAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Null(user);
    }

    [Fact]
    public async Task ProblemVersionLookup_ResolvesAndAnswersNullForAVersionWithNoRow()
    {
        // Content seeding is off in this host, so there is no version to find; what is proved is
        // that the contract constructs from the composed container and queries the real schema.
        await using var scope = api.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IProblemVersionLookup>();

        var version = await lookup.FindAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Null(version);
    }
}
