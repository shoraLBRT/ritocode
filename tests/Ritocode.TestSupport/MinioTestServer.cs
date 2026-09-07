using System.Collections.Concurrent;
using System.Globalization;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using Ritocode.Shared.Storage;
using Testcontainers.Minio;

namespace Ritocode.TestSupport;

/// <summary>
/// One MinIO container for a test assembly, handing out a fresh set of buckets per caller.
/// Registered as an xUnit assembly fixture, so the container is started once, on first use, and
/// removed when the assembly finishes.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="PostgresTestServer"/>, for the same reason: a test asserts on what
/// the store really did, so isolation is a real bucket of its own rather than a fake in front of
/// one. Buckets are the unit because they are what a <see cref="StorageRole"/> resolves to — a test
/// that puts the same key under two roles is then asserting the routing, not a naming convention.
/// </para>
/// <para>
/// Buckets are not deleted. The container is the lifetime boundary, and it is destroyed when the
/// test assembly ends.
/// </para>
/// </remarks>
public sealed class MinioTestServer : IAsyncDisposable
{
    /// <summary>Matches the image in <c>compose.yaml</c>, so tests never run on a different build.</summary>
    public const string Image = "minio/minio:RELEASE.2025-04-22T22-12-26Z";

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Clients handed to tests, kept so the assembly's run does not leak sockets.</summary>
    private readonly ConcurrentBag<IAmazonS3> _clients = [];

    private MinioContainer? _container;
    private int _bucketSetsCreated;

    /// <summary>
    /// Creates a bucket per storage role and returns the settings that address them.
    /// <paramref name="label"/> only makes the buckets recognisable while a run is in flight;
    /// uniqueness comes from a counter, so the same label may be used twice.
    /// </summary>
    public async Task<ObjectStorageOptions> CreateBucketsAsync(string label, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var container = await StartAsync(cancellationToken);
        var prefix = BucketPrefix(label, Interlocked.Increment(ref _bucketSetsCreated));

        var options = new ObjectStorageOptions
        {
            ServiceUrl = container.GetConnectionString(),
            AccessKey = container.GetAccessKey(),
            SecretKey = container.GetSecretKey(),
            ProblemBundlesBucket = $"{prefix}-problem-bundles",
            WorkspaceSnapshotsBucket = $"{prefix}-workspace-snapshots",
            EvaluationArtifactsBucket = $"{prefix}-evaluation-artifacts",
        };

        using var client = CreateClient(options);
        foreach (var role in StorageRoles.All)
        {
            await client.PutBucketAsync(options.BucketFor(role), cancellationToken);
        }

        return options;
    }

    /// <summary>An <see cref="IObjectStore"/> over <paramref name="options"/>, wired as the host wires it.</summary>
    public IObjectStore CreateStore(ObjectStorageOptions options)
    {
        var client = CreateClient(options);
        _clients.Add(client);
        return new S3ObjectStore(client, Options.Create(options));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }

        _gate.Dispose();
    }

    private static AmazonS3Client CreateClient(ObjectStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.UsePathStyleAddressing,
                AuthenticationRegion = options.Region,
            });
    }

    private async Task<MinioContainer> StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                var container = new MinioBuilder(Image).Build();
                await container.StartAsync(cancellationToken);
                _container = container;
            }

            return _container;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Builds a legal bucket name prefix: lower case, no punctuation beyond the hyphen, and short
    /// enough that the longest suffix below still fits inside S3's 63-character limit.
    /// </summary>
    private static string BucketPrefix(string label, int ordinal)
    {
        const int LongestSuffixLength = 22; // "-evaluation-artifacts", plus the hyphen before the ordinal.
        const int MaxBucketNameLength = 63;

        var ordinalText = ordinal.ToString(CultureInfo.InvariantCulture);
        var room = MaxBucketNameLength - LongestSuffixLength - ordinalText.Length - 1;

        var sanitized = new string([.. label
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .Take(room)]);

        return $"{sanitized}-{ordinalText}";
    }
}
