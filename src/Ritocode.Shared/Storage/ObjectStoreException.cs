namespace Ritocode.Shared.Storage;

/// <summary>
/// Object storage could not serve a request: unreachable, unauthorised, or a bucket that does not
/// exist. Thrown rather than returned, because none of these is a condition calling code can act
/// on — unlike a missing object, which <see cref="IObjectStore.GetAsync"/> reports as a value.
/// </summary>
public sealed class ObjectStoreException : Exception
{
    public ObjectStoreException()
    {
    }

    public ObjectStoreException(string message)
        : base(message)
    {
    }

    public ObjectStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
