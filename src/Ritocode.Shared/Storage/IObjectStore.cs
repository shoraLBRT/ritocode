namespace Ritocode.Shared.Storage;

/// <summary>
/// Put, get and copy against object storage, addressed by the references of docs/STORAGE_LAYOUT.md.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately three methods. Deletion is a fourth piece, deferred with issue #43 along with
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

    /// <summary>
    /// Copies the object <paramref name="source"/> names to <paramref name="destination"/> inside the
    /// store, replacing whatever was there, without the bytes passing through this process.
    /// </summary>
    /// <remarks>
    /// The copy is a new object, not a pointer: a later put to <paramref name="source"/> leaves it as it
    /// was. That is the whole reason a submission freezes its input tree with one (docs/STORAGE_LAYOUT.md).
    /// </remarks>
    /// <returns>
    /// <see langword="false"/> when no object exists at <paramref name="source"/>, having written
    /// nothing — for the reason <see cref="GetAsync"/> gives.
    /// </returns>
    /// <exception cref="ObjectStoreException">The store rejected or could not serve the request.</exception>
    Task<bool> CopyAsync(StorageReference source, StorageReference destination, CancellationToken cancellationToken = default);
}
