namespace Ritocode.Shared.Errors;

/// <summary>
/// Transport-agnostic classification of a failure. The HTTP layer is the only place that
/// turns these into status codes (see <see cref="Http.ErrorStatusCodeMap"/>), so domain code
/// never has to know about HTTP.
/// </summary>
public enum ErrorType
{
    /// <summary>Request payload or parameters failed validation.</summary>
    Validation,

    /// <summary>No usable credentials were presented.</summary>
    Unauthenticated,

    /// <summary>Credentials were valid but the caller may not perform the action.</summary>
    Forbidden,

    /// <summary>The addressed resource does not exist, or is not visible to the caller.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state of the resource.</summary>
    Conflict,

    /// <summary>An If-Match / version precondition did not hold.</summary>
    PreconditionFailed,

    /// <summary>The caller exceeded a rate or quota limit.</summary>
    RateLimited,

    /// <summary>A dependency the request needs is temporarily unavailable.</summary>
    Unavailable,

    /// <summary>An unhandled or unclassified failure.</summary>
    Unexpected,
}
