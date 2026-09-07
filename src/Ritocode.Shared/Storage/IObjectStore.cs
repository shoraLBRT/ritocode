namespace Ritocode.Shared.Storage;

/// <summary>
/// Put and get against object storage, addressed by the references of docs/STORAGE_LAYOUT.md.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately two methods. Deletion is a third piece, deferred with issue #43 along with
/// retention — every object written today is written forever, which is a known hole rather than an
/// oversight. Listing a prefix is what deleting one needs and arrives with it.
/// </para>
/// <para>
/// A read writes into a destination the caller owns rather than returning a stream, so there is no
/// question about who disposes the underlying HTTP response, and no version of this interface that
/// buffers a whole archive in memory to avoid that question. Every consumer either unpacks the
/// bytes or copies them onward, and both already have a destination in hand.
/// </para>
/// </remarks>
public interface IObjectStore
{
    /// <summary>
    /// Writes <paramref name="content"/> to the object <paramref name="reference"/> names,
    /// replacing whatever was there. A put is atomic per object: a concurrent reader sees the old
    /// bytes or the new ones and never half of either.
    /// </summary>
    /// <param name="reference">An object reference. A prefix reference names no single object and is rejected.</param>
    /// <param name="content">
    /// The bytes to store, positioned where reading should start. Must be seekable — the request has
    /// to be signed over a known length, and buffering an arbitrary stream to discover that length
    /// is a cost the caller is better placed to decide about.
    /// </param>
    /// <exception cref="ObjectStoreException">The store rejected or could not serve the request.</exception>
    Task PutAsync(StorageReference reference, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies the object <paramref name="reference"/> names into <paramref name="destination"/>.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when no such object exists, having written nothing. A missing object
    /// is reported rather than thrown because the caller — the module that owns the row pointing at
    /// it — is the one entitled to decide what that means and which error code a client sees.
    /// </returns>
    /// <exception cref="ObjectStoreException">The store rejected or could not serve the request.</exception>
    Task<bool> GetAsync(StorageReference reference, Stream destination, CancellationToken cancellationToken = default);
}
