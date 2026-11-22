using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Modules.Errors.Recording;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Modules.Errors.Browser;

/// <summary>
/// What the browser's reporter sends: the error, the page's path and the commit the bundle was built from —
/// never what the learner typed. Every field is optional and cut to its column's length.
/// </summary>
public sealed record ClientErrorRequest(string? Type, string? Message, string? Stack, string? Page, string? Commit);

/// <summary>
/// <c>POST /api/v1/client-errors</c> (ADR 0020, #294): an error in a visitor's browser, recorded like a server
/// one. Anonymous, so it is limited per address and in total, and its body is capped before it is read.
/// </summary>
/// <remarks>
/// It takes JSON only, so another site's page cannot send it without a CORS preflight the API refuses; a
/// signed-in visitor's report carries the session's CSRF token like every other write. The address is used
/// for the limit, in memory, and never stored. The answer is 202 with no body: the reporter does not wait
/// on it, and the error page reads the report's request id from its header.
/// </remarks>
internal static class ClientErrorEndpoints
{
    public const string RateLimitedCode = "client_errors_rate_limited";

    public static IEndpointRouteBuilder MapClientErrorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/client-errors", ReportAsync)
            .WithName("ReportClientError")
            .WithTags("Errors")
            .Accepts<ClientErrorRequest>("application/json")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ReportAsync(
        HttpContext context,
        ClientErrorRateLimiter limiter,
        ErrorRecorder recorder,
        IOptions<ErrorsOptions> options,
        IOptions<JsonOptions> json,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return ApiProblem.ToResult(RoutingRefusals.UnsupportedMediaType(), context);
        }

        var maxBytes = options.Value.Browser.MaxBodyBytes;
        if (context.Request.ContentLength > maxBytes)
        {
            return ApiProblem.ToResult(TooLarge(), context);
        }

        if (!limiter.TryAcquire(context.Connection.RemoteIpAddress?.ToString() ?? "unknown"))
        {
            return ApiProblem.ToResult(AppError.RateLimited(RateLimitedCode, "Too many error reports. Try again later."), context);
        }

        // Read to the cap and one byte past it, so a body without a length is held to the same limit.
        var buffer = new byte[maxBytes + 1];
        var read = 0;
        int chunk;
        while (read < buffer.Length && (chunk = await context.Request.Body.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false)) > 0)
        {
            read += chunk;
        }

        if (read > maxBytes)
        {
            return ApiProblem.ToResult(TooLarge(), context);
        }

        ClientErrorRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ClientErrorRequest>(buffer.AsSpan(0, read), json.Value.SerializerOptions);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null)
        {
            return ApiProblem.ToResult(
                new AppError(ErrorType.Validation, "request_invalid", "The request could not be read: its body is not valid UTF-8 JSON, or a parameter is malformed."),
                context);
        }

        var requestId = context.Items.TryGetValue(RequestId.ItemsKey, out var value) ? value as string : null;
        var report = ErrorReports.FromBrowser(request.Type, request.Message, request.Stack, request.Page, request.Commit, requestId);

        await recorder.RecordAsync(report, report.Stack, cancellationToken).ConfigureAwait(false);

        return Results.Accepted();
    }

    private static AppError TooLarge() =>
        new(ErrorType.PayloadTooLarge, "request_too_large", "The request body is larger than the server accepts.");
}

/// <summary>
/// At most <see cref="BrowserErrorOptions.MaxPerAddress"/> reports per address, and
/// <see cref="BrowserErrorOptions.MaxTotal"/> from all of them, in any sliding window. In memory, like the
/// feedback widget's: one API instance runs (ADR 0011), and a restart forgets it.
/// </summary>
internal sealed class ClientErrorRateLimiter(IOptions<ErrorsOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _byAddress = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _all = new();

    public bool TryAcquire(string address)
    {
        var settings = options.Value.Browser;
        var now = clock.GetUtcNow();
        var windowStart = now - settings.Window;
        var times = _byAddress.GetOrAdd(address, _ => new Queue<DateTimeOffset>());

        lock (_all)
        {
            Expire(_all, windowStart);
            Expire(times, windowStart);

            if (_all.Count >= settings.MaxTotal || times.Count >= settings.MaxPerAddress)
            {
                return false;
            }

            _all.Enqueue(now);
            times.Enqueue(now);

            // Addresses gone quiet are dropped now and then, so the map does not grow for ever.
            if (_byAddress.Count > 10_000)
            {
                foreach (var (key, queue) in _byAddress)
                {
                    Expire(queue, windowStart);
                    if (queue.Count == 0)
                    {
                        _byAddress.TryRemove(key, out _);
                    }
                }
            }
        }

        return true;
    }

    private static void Expire(Queue<DateTimeOffset> times, DateTimeOffset windowStart)
    {
        while (times.Count > 0 && times.Peek() <= windowStart)
        {
            times.Dequeue();
        }
    }
}
