using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// Builds the unified error body. Every non-2xx response the API produces goes through here,
/// so the shape is identical whether the failure came from validation, a thrown
/// <see cref="AppException"/>, or an unhandled exception.
/// </summary>
/// <remarks>
/// Shape is RFC 9457 <c>application/problem+json</c> with two Ritocode extensions:
/// <c>code</c> (stable machine-readable identifier) and <c>requestId</c> (correlation id).
/// Validation failures add <c>errors</c>: a field name to messages map.
/// </remarks>
public static class ApiProblem
{
    /// <summary>Base URI for the <c>type</c> member; the error code is appended to it.</summary>
    public const string TypeUriPrefix = "https://ritocode.dev/errors/";

    public static ProblemDetails Create(AppError error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);

        var problem = new ProblemDetails
        {
            Type = TypeUriPrefix + error.Code,
            Title = ErrorStatusCodeMap.ToTitle(error.Type),
            Status = ErrorStatusCodeMap.ToStatusCode(error.Type),
            Detail = error.Message,
            Instance = context.Request.Path.Value,
        };

        problem.Extensions["code"] = error.Code;
        problem.Extensions["requestId"] = ResolveRequestId(context);

        if (error.Fields is { Count: > 0 })
        {
            problem.Extensions["errors"] = error.Fields;
        }

        return problem;
    }

    /// <summary>
    /// Writes the unified error body directly to the response, for the places that have no
    /// <see cref="IResult"/> to return: the exception handler, and an authentication handler's
    /// challenge.
    /// </summary>
    /// <remarks>
    /// The correlation header is re-applied rather than assumed. <c>UseExceptionHandler</c> clears
    /// the response before re-executing, which drops the header the middleware set, and a 401 or a
    /// 500 is exactly when the caller needs it most. The content type is passed to the writer
    /// instead of being assigned first, because <c>WriteAsJsonAsync</c> overwrites
    /// <c>Response.ContentType</c> otherwise.
    /// </remarks>
    public static async Task WriteAsync(HttpContext context, AppError error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var problem = Create(error, context);

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.Headers[RequestId.HeaderName] = (string)problem.Extensions["requestId"]!;

        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }

    /// <summary>Result helper for endpoints that return an <see cref="AppError"/> instead of throwing.</summary>
    public static IResult ToResult(AppError error, HttpContext context)
    {
        var problem = Create(error, context);
        return Results.Problem(problem);
    }

    private static string ResolveRequestId(HttpContext context) =>
        context.Items.TryGetValue(RequestId.ItemsKey, out var value) && value is string id
            ? id
            : context.TraceIdentifier;
}
