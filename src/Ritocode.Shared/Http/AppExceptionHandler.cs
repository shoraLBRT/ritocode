using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// Turns any exception escaping an endpoint into the unified error response.
/// <see cref="AppException"/> keeps its domain code and status; a request the framework could not
/// read (<see cref="BadHttpRequestException"/>) is the client's error and keeps its 4xx; anything
/// else becomes an opaque 500 so internal details never reach the client — the detail lives in the
/// logs, findable by the request id that both the log line and the response carry.
/// </summary>
public sealed partial class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var error = exception switch
        {
            AppException appException => appException.Error,
            BadHttpRequestException badRequest => Unreadable(badRequest),
            _ => new AppError(ErrorType.Unexpected, "internal_error", "An unexpected error occurred."),
        };

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

        await ApiProblem.WriteAsync(httpContext, error, cancellationToken);

        return true;
    }

    /// <summary>
    /// A body that is not JSON (or not UTF-8), a missing body, a query value that does not bind to
    /// its type, a body over the server's cap, a content type the endpoint does not read. The
    /// framework's status says which; the message stays generic, as the client knows what it sent.
    /// </summary>
    /// <remarks>
    /// Logged like any expected failure, without the framework's message: for a parameter that does
    /// not bind it quotes the value, and query strings stay out of the logs.
    /// </remarks>
    private static AppError Unreadable(BadHttpRequestException exception) => exception.StatusCode switch
    {
        StatusCodes.Status413PayloadTooLarge => new AppError(
            ErrorType.PayloadTooLarge, "request_too_large", "The request body is larger than the server accepts."),
        StatusCodes.Status415UnsupportedMediaType => new AppError(
            ErrorType.UnsupportedMediaType, "unsupported_media_type", "The request body must be JSON (application/json)."),
        _ => new AppError(
            ErrorType.Validation, "request_invalid", "The request could not be read: its body is not valid UTF-8 JSON, or a parameter is malformed."),
    };

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
