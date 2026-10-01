using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// What routing answers on its own, before any endpoint runs, given the unified error body
/// (ADR 0003): a method a route does not take is <c>405 method_not_allowed</c>, a body in a type its
/// endpoint does not read is <c>415 unsupported_media_type</c>, and an address under the API that no
/// route serves is <c>404 not_found</c> (<see cref="NoSuchAddress"/>).
/// </summary>
/// <remarks>
/// <para>
/// Routing's matcher policies write these as an empty response from an endpoint of their own, and no
/// exception is thrown, so <see cref="AppExceptionHandler"/> never sees them. Status-code pages run
/// after the pipeline for a 4xx or 5xx that has no body yet, which is exactly that case; every
/// response that already has a body — any endpoint's own error — is left alone, and so is any other
/// empty status.
/// </para>
/// <para>
/// The unknown address is answered here rather than by a catch-all fallback endpoint. A fallback takes
/// every method and every content type, so it outranked the 405 and the 415: <c>GET /api/v1/signals</c>
/// came back as a 404. With no endpoint at all, the host's fallback authorisation policy still puts a
/// 401 in front of an anonymous caller.
/// </para>
/// </remarks>
public static class RoutingRefusals
{
    public const string MethodNotAllowedCode = "method_not_allowed";

    public const string UnsupportedMediaTypeCode = "unsupported_media_type";

    /// <summary>The route exists; routing's own <c>Allow</c> header names the methods it takes.</summary>
    public static AppError MethodNotAllowed() =>
        new(ErrorType.MethodNotAllowed, MethodNotAllowedCode, "This address does not take this method; the Allow header lists the ones it does.");

    /// <summary>Also what the exception handler answers for a body sent with no content type at all.</summary>
    public static AppError UnsupportedMediaType() =>
        new(ErrorType.UnsupportedMediaType, UnsupportedMediaTypeCode, "The request body must be JSON (application/json).");

    /// <param name="app">The host's pipeline, before anything that could write one of these responses.</param>
    /// <param name="api">The API's base path: only an unknown address under it is answered.</param>
    public static IApplicationBuilder UseRoutingRefusalBodies(this IApplicationBuilder app, PathString api) =>
        app.UseStatusCodePages(context => WriteAsync(context.HttpContext, api));

    private static Task WriteAsync(HttpContext context, PathString api)
    {
        var error = context.Response.StatusCode switch
        {
            StatusCodes.Status405MethodNotAllowed => MethodNotAllowed(),
            StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType(),
            StatusCodes.Status404NotFound when context.GetEndpoint() is null && context.Request.Path.StartsWithSegments(api) =>
                NoSuchAddress.Error(),
            _ => null,
        };

        return error is null ? Task.CompletedTask : ApiProblem.WriteAsync(context, error, context.RequestAborted);
    }
}
