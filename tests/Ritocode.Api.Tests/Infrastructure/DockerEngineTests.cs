using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// The wait in front of every PostgreSQL container: a test run started while Docker Desktop is
/// still coming up must wait for it rather than fail every database test at once.
/// </summary>
public sealed class DockerEngineTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task WaitUntilAnswering_ReturnsOnceTheEngineAnswers()
    {
        var attempts = 0;

        await DockerEngine.WaitUntilAnsweringAsync(
            _ => ++attempts < 3
                ? throw new InvalidOperationException("500 Internal Server Error")
                : Task.CompletedTask,
            TimeSpan.FromMinutes(1),
            Interval,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task WaitUntilAnswering_GivesUpWithTheEnginesLastError()
    {
        var refused = new InvalidOperationException("500 Internal Server Error");

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => DockerEngine.WaitUntilAnsweringAsync(
            _ => throw refused,
            TimeSpan.FromMilliseconds(20),
            Interval,
            TestContext.Current.CancellationToken));

        Assert.Same(refused, exception.InnerException);
    }

    [Fact]
    public async Task WaitUntilAnswering_StopsWhenTheRunIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DockerEngine.WaitUntilAnsweringAsync(
            token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            TimeSpan.FromMinutes(1),
            Interval,
            cancellation.Token));
    }
}
