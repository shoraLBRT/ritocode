using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ritocode.Api.Tests.Infrastructure;
using Ritocode.Modules.Submissions.Queue;

namespace Ritocode.Api.Tests.Queue;

/// <summary>
/// The queue as the host composes it. Nothing in the host calls it until #17, so a registration that
/// compiles and cannot be constructed — the options binding, the clock, the context — would otherwise
/// first fail inside the worker loop.
/// </summary>
public sealed class SubmissionDispatcherCompositionTests(TestApi api) : IClassFixture<TestApi>
{
    [Fact]
    public async Task TheQueue_ResolvesFromTheHost_AndClaimsNothingFromAnEmptyTable()
    {
        await using var scope = api.Services.CreateAsyncScope();

        var queue = scope.ServiceProvider.GetRequiredService<ISubmissionDispatcher>();

        Assert.Null(await queue.ClaimNextAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheClaimTimeout_DefaultsToFifteenMinutes()
    {
        var options = api.Services.GetRequiredService<IOptions<SubmissionQueueOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(15), options.ClaimTimeout);
    }
}
