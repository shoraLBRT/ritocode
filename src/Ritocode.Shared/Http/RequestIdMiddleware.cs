using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ritocode.Shared.Http;

/// <summary>
/// Assigns every request a correlation id: honours a well-formed inbound
/// <see cref="RequestId.HeaderName"/>, otherwise generates one. The id is echoed back on the
/// response, pushed into the log scope, and surfaced in error payloads so a user-reported
/// failure can be traced to its logs.
/// </summary>
public sealed class RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context);

        context.Items[RequestId.ItemsKey] = requestId;
        context.TraceIdentifier = requestId;
        context.Response.Headers[RequestId.HeaderName] = requestId;

        using (logger.BeginScope(new Dictionary<string, object> { [RequestId.LogPropertyName] = requestId }))
        {
            await next(context);
        }
    }

    private static string ResolveRequestId(HttpContext context)
    {
        var inbound = context.Request.Headers[RequestId.HeaderName].ToString();
        return IsAcceptable(inbound) ? inbound : Guid.NewGuid().ToString("n");
    }

    /// <summary>
    /// A client-supplied id is echoed into headers and logs, so only a conservative character
    /// set is accepted. Anything else is discarded in favour of a generated id.
    /// </summary>
    private static bool IsAcceptable(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > RequestId.MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            var allowed = char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
