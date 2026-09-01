namespace Ritocode.Shared.Http;

/// <summary>Names shared by the request-correlation middleware, logging and error responses.</summary>
public static class RequestId
{
    /// <summary>Inbound and outbound header carrying the correlation id.</summary>
    public const string HeaderName = "X-Request-Id";

    /// <summary>Key under which the id is stored in <c>HttpContext.Items</c>.</summary>
    public const string ItemsKey = "ritocode.request_id";

    /// <summary>Log scope property name, so every log line of a request carries the id.</summary>
    public const string LogPropertyName = "RequestId";

    /// <summary>Maximum accepted length of a client-supplied id; longer values are replaced.</summary>
    public const int MaxLength = 128;
}
