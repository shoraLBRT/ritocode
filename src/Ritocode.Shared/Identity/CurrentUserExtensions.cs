using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Identity;

public static class CurrentUserExtensions
{
    /// <summary>Whether the request carried a usable identity.</summary>
    public static bool IsAuthenticated(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.Id is not null;
    }

    /// <summary>
    /// The authenticated user's identifier, failing the request when there is none.
    /// </summary>
    /// <remarks>
    /// The host's fallback policy rejects an unauthenticated request before the endpoint runs, so
    /// there are only two ways to arrive here: an endpoint that opted out with
    /// <c>AllowAnonymous</c> and then asked for a user anyway, or a principal that authenticated
    /// without carrying a usable identifier. The first is a bug in the endpoint's metadata; the
    /// second is a malformed credential. Both are an <see cref="AppException"/> carrying
    /// <see cref="ErrorType.Unauthenticated"/> rather than an
    /// <see cref="InvalidOperationException"/>, so the caller gets the same 401 body the middleware
    /// would have produced instead of an opaque 500 that reads as a platform fault.
    /// </remarks>
    /// <exception cref="AppException">The request is anonymous.</exception>
    public static Guid RequireId(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser.Id ?? throw new AppException(AppError.Unauthenticated());
    }
}
