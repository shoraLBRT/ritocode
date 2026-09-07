using System.Text;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Shared.Tests.Storage;

/// <summary>
/// The storage client against a real MinIO, which is what <c>compose.yaml</c> runs and what a
/// deployment replaces with a managed S3. There is no fake: ADR 0005's reduction table does not
/// list one, and a stub would assert only that the test doubles agree with each other.
/// </summary>
public sealed class S3ObjectStoreTests(MinioTestServer minio) : IAsyncLifetime
{
    private IObjectStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        var options = await minio.CreateBucketsAsync(nameof(S3ObjectStoreTests), Token);
        _store = minio.CreateStore(options);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Put_ThenGet_ReturnsTheSameBytes()
    {
        var reference = StorageKeys.ProblemBundle(Guid.NewGuid());
        var content = Bytes("a problem bundle");

        await _store.PutAsync(reference, new MemoryStream(content), Token);

        Assert.Equal(content, await ReadAsync(reference));
    }

    [Fact]
    public async Task Get_OfAnObjectThatWasNeverWritten_ReportsAbsenceRatherThanThrowing()
    {
        using var destination = new MemoryStream();

        var found = await _store.GetAsync(StorageKeys.WorkspaceSnapshot(Guid.NewGuid()), destination, Token);

        Assert.False(found);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task Put_OverTheSameKey_ReplacesTheContent()
    {
        // A workspace snapshot is overwritten on every save; the later put has to win.
        var reference = StorageKeys.WorkspaceSnapshot(Guid.NewGuid());

        await _store.PutAsync(reference, new MemoryStream(Bytes("first save")), Token);
        await _store.PutAsync(reference, new MemoryStream(Bytes("second save")), Token);

        Assert.Equal(Bytes("second save"), await ReadAsync(reference));
    }

    [Fact]
    public async Task RolesAddressDifferentBuckets_SoTheSameKeyIsTwoObjects()
    {
        // The one behaviour a single bucket with three prefixes would silently break.
        const string Key = "collision/tree.tar.gz";
        var bundles = StorageReference.Create(StorageRole.ProblemBundles, Key);
        var snapshots = StorageReference.Create(StorageRole.WorkspaceSnapshots, Key);

        await _store.PutAsync(bundles, new MemoryStream(Bytes("bundle")), Token);
        await _store.PutAsync(snapshots, new MemoryStream(Bytes("snapshot")), Token);

        Assert.Equal(Bytes("bundle"), await ReadAsync(bundles));
        Assert.Equal(Bytes("snapshot"), await ReadAsync(snapshots));
    }

    [Fact]
    public async Task EveryRole_IsWritableAndReadable()
    {
        foreach (var role in StorageRoles.All)
        {
            var reference = StorageReference.Create(role, $"round-trip/{role.Name()}.txt");

            await _store.PutAsync(reference, new MemoryStream(Bytes(role.Name())), Token);

            Assert.Equal(Bytes(role.Name()), await ReadAsync(reference));
        }
    }

    [Fact]
    public async Task AReferenceReadBackFromItsStoredForm_ResolvesToTheSameObject()
    {
        // Rule 3 of the layout: a key is read back from its stored reference, never recomputed.
        // This is that path, minus the column.
        var written = StorageKeys.SubmissionInputTree(Guid.NewGuid());
        await _store.PutAsync(written, new MemoryStream(Bytes("frozen tree")), Token);

        Assert.True(StorageReference.TryParse(written.ToString(), out var readBack));

        Assert.Equal(Bytes("frozen tree"), await ReadAsync(readBack));
    }

    [Fact]
    public async Task ABinaryPayload_IsReturnedByteForByte()
    {
        // Archives are the real payload; a text-only round trip would not catch an encoding step.
        var content = new byte[8 * 1024];
        Random.Shared.NextBytes(content);
        var reference = StorageKeys.ValidatorOutput(Guid.NewGuid(), "unit-tests");

        await _store.PutAsync(reference, new MemoryStream(content), Token);

        Assert.Equal(content, await ReadAsync(reference));
    }

    [Fact]
    public async Task AnEmptyObject_IsPresentRatherThanMissing()
    {
        // A validator that wrote nothing to stderr is not the same as one that never ran.
        var reference = StorageKeys.ValidatorStderr(Guid.NewGuid(), "compile");

        await _store.PutAsync(reference, new MemoryStream([]), Token);

        using var destination = new MemoryStream();
        Assert.True(await _store.GetAsync(reference, destination, Token));
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task APrefixReference_NamesNoSingleObject_AndIsRefusedByBothOperations()
    {
        var prefix = StorageKeys.SubmissionArtifacts(Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.PutAsync(prefix, new MemoryStream(Bytes("x")), Token));

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
            {
                using var destination = new MemoryStream();
                await _store.GetAsync(prefix, destination, Token);
            });
    }

    [Fact]
    public async Task ANonSeekableStream_IsRefused_RatherThanBufferedSilently()
    {
        using var content = new NonSeekableStream(Bytes("cannot be signed"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.PutAsync(StorageKeys.ProblemBundle(Guid.NewGuid()), content, Token));
    }

    [Fact]
    public async Task Put_DoesNotCloseTheCallersStream()
    {
        using var content = new MemoryStream(Bytes("reused"));

        await _store.PutAsync(StorageKeys.ProblemBundle(Guid.NewGuid()), content, Token);

        content.Position = 0;
        await _store.PutAsync(StorageKeys.ProblemBundle(Guid.NewGuid()), content, Token);
    }

    [Fact]
    public async Task ABucketThatDoesNotExist_FailsOnTheRequest_NotAtConstruction()
    {
        // Construction is offline by design, so the cost of a wrong bucket name is a failed
        // request — reported as ObjectStoreException rather than as an SDK type leaking upward.
        var reachable = await minio.CreateBucketsAsync("absent", Token);

        var store = minio.CreateStore(new ObjectStorageOptions
        {
            ServiceUrl = reachable.ServiceUrl,
            AccessKey = reachable.AccessKey,
            SecretKey = reachable.SecretKey,
            ProblemBundlesBucket = "bucket-that-was-never-created",
        });

        await Assert.ThrowsAsync<ObjectStoreException>(
            () => store.PutAsync(StorageKeys.ProblemBundle(Guid.NewGuid()), new MemoryStream(Bytes("x")), Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private async Task<byte[]> ReadAsync(StorageReference reference)
    {
        using var destination = new MemoryStream();

        Assert.True(await _store.GetAsync(reference, destination, Token), $"{reference} was not found.");

        return destination.ToArray();
    }

    /// <summary>A stream the SDK cannot sign, because its length is not knowable up front.</summary>
    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }
}
