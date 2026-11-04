using Microsoft.AspNetCore.Http;

namespace Ritocode.Shared.Http;

/// <summary>
/// The response headers the API is responsible for (docs/SPEC.md §10.1). The proxy in front of it
/// adds the ones that belong to the site as a whole — HSTS and the pages' content policy — in #135.
/// </summary>
/// <remarks>
/// <para>
/// Every response here is JSON or a redirect, never a document to render, so the policy is the
/// strictest there is: nothing may load, nothing may frame it, the type is never sniffed and no
/// referrer leaves. Responses carry a learner's own data, so nothing may be cached unless an endpoint
/// says otherwise.
/// </para>
/// <para>
/// Written when the response starts rather than on the way in: the exception handler clears the
/// headers before it writes a 500, and this way the error body carries them too.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";

            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
