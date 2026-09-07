using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Ritocode.Shared.Storage;

/// <summary>
/// <see cref="IObjectStore"/> over the S3 API, which is what MinIO speaks locally and what a
/// managed provider speaks in a deployment — so moving between them is configuration, per
/// docs/ARCHITECTURE.md.
/// </summary>
public sealed class S3ObjectStore(IAmazonS3 client, IOptions<ObjectStorageOptions> options) : IObjectStore
{
    private readonly ObjectStorageOptions _options = options.Value;

    public async Task PutAsync(StorageReference reference, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(content);
        RequireObjectReference(reference);

        if (!content.CanSeek)
        {
            throw new ArgumentException(
                "Content must be seekable; the request is signed over a known length.",
                nameof(content));
        }

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketFor(reference.Role),
            Key = reference.Key,
            InputStream = content,
            // The caller owns the stream it passed in. Letting the SDK close it would make a
            // perfectly ordinary "write this, then write it again" into an ObjectDisposedException.
            AutoCloseStream = false,
        };

        try
        {
            await client.PutObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            throw Failure(reference, "store", exception);
        }
    }

    public async Task<bool> GetAsync(
        StorageReference reference,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(destination);
        RequireObjectReference(reference);

        var request = new GetObjectRequest
        {
            BucketName = _options.BucketFor(reference.Role),
            Key = reference.Key,
        };

        try
        {
            // Disposing the response is what returns the connection; the body has to be copied out
            // before that happens, which is why the destination is a parameter rather than a return.
            using var response = await client.GetObjectAsync(request, cancellationToken);
            await response.ResponseStream.CopyToAsync(destination, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Covers both NoSuchKey and NoSuchBucket. A missing bucket is a deployment fault rather
            // than a missing object, but the two are indistinguishable to a caller holding a
            // reference, and startup already validated the names it was given.
            return false;
        }
        catch (AmazonS3Exception exception)
        {
            throw Failure(reference, "read", exception);
        }
    }

    private static void RequireObjectReference(StorageReference reference)
    {
        if (reference.IsPrefix)
        {
            throw new ArgumentException(
                $"'{reference}' is a prefix reference and names no single object.",
                nameof(reference));
        }
    }

    private static ObjectStoreException Failure(StorageReference reference, string verb, AmazonS3Exception exception) =>
        new($"Could not {verb} '{reference}': {exception.ErrorCode ?? exception.StatusCode.ToString()}.", exception);
}
