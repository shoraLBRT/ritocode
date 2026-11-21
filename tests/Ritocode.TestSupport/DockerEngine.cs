using DotNet.Testcontainers.Configurations;

namespace Ritocode.TestSupport;

/// <summary>
/// Waits for the Docker engine to answer before a container is asked for.
/// </summary>
/// <remarks>
/// Docker Desktop on Windows runs its process and opens its pipe well before the engine behind it
/// answers: for tens of seconds after a start, every API call is refused with
/// <c>500 Internal Server Error</c>. A test run started in that window fails every test that needs
/// PostgreSQL at once, and the same run a minute later passes. Waiting for the engine's
/// <c>/version</c> turns that window into a delay; an engine that never answers still fails, with
/// the engine's last error, once <see cref="DefaultTimeout"/> has passed.
/// </remarks>
public static class DockerEngine
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    /// <summary>Returns once the engine Testcontainers is configured for answers.</summary>
    public static Task WaitUntilAnsweringAsync(CancellationToken cancellationToken) =>
        WaitUntilAnsweringAsync(AskVersionAsync, DefaultTimeout, DefaultInterval, cancellationToken);

    /// <summary>
    /// Calls <paramref name="probe"/> until it completes without throwing, waiting
    /// <paramref name="interval"/> between attempts, and throws a <see cref="TimeoutException"/>
    /// carrying the probe's last error once <paramref name="timeout"/> has passed.
    /// </summary>
    public static async Task WaitUntilAnsweringAsync(
        Func<CancellationToken, Task> probe,
        TimeSpan timeout,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            try
            {
                await probe(cancellationToken);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow + interval > deadline)
                {
                    throw new TimeoutException(
                        $"The Docker engine did not answer within {timeout.TotalSeconds:0} s. Tests that need PostgreSQL start it through Testcontainers, so Docker has to be running.",
                        exception);
                }
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    private static async Task AskVersionAsync(CancellationToken cancellationToken)
    {
        using var client = TestcontainersSettings.OS.DockerEndpointAuthConfig
            .GetDockerClientBuilder(Guid.NewGuid())
            .Build();

        await client.System.GetVersionAsync(cancellationToken);
    }
}
