using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// Turns any exception escaping an endpoint into the unified error response.
/// <see cref="AppException"/> keeps its domain code and status; anything else becomes an
/// opaque 500 so internal details never reach the client — the detail lives in the logs,
/// findable by the request id that both the log line and the response carry.
/// </summary>
public sealed partial class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var error = exception is AppException appException
            ? appException.Error
            : new AppError(ErrorType.Unexpected, "internal_error", "An unexpected error occurred.");

        // Materialised once: PathString -> string conversion should not sit inside the log call.
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.Value ?? string.Empty;

        if (error.Type == ErrorType.Unexpected)
        {
            LogUnhandled(logger, method, path, exception);
        }
        else
        {
            LogExpectedFailure(logger, error.Code, method, path);
        }

        var problem = ApiProblem.Create(error, httpContext);

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        // UseExceptionHandler clears the response before re-executing, which drops the correlation
        // header the middleware set. Re-apply it: a 500 is exactly when the caller needs it most.
        httpContext.Response.Headers[RequestId.HeaderName] = (string)problem.Extensions["requestId"]!;

        // The content type is passed to the writer rather than assigned beforehand, because
        // WriteAsJsonAsync overwrites Response.ContentType with application/json otherwise.
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, string method, string path, Exception exception);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Request failed with {ErrorCode} on {Method} {Path}")]
    private static partial void LogExpectedFailure(ILogger logger, string errorCode, string method, string path);
}
