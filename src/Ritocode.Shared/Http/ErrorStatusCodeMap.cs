using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// The single place where domain failures become HTTP status codes.
/// Documented in docs/adr/0003-api-conventions.md; changing a mapping is an API break.
/// </summary>
public static class ErrorStatusCodeMap
{
    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static string ToTitle(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.Unauthenticated => "Authentication required",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.PreconditionFailed => "Precondition failed",
        ErrorType.RateLimited => "Too many requests",
        ErrorType.Unavailable => "Service unavailable",
        _ => "Internal server error",
    };
}
