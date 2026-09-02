using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ritocode.Shared.Diagnostics;

namespace Ritocode.Api.Endpoints;

/// <summary>
/// Liveness and readiness probes.
/// <c>/health/live</c> answers "is the process up" and must never touch a dependency — a failing
/// database should not get the container killed. <c>/health/ready</c> answers "can it serve
/// traffic" and includes every check tagged <see cref="HealthCheckTags.Ready"/>.
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // No registered check qualifies: liveness is purely "the host responds".
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        }).WithTags("Health").AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = WriteResponseAsync,
        }).WithTags("Health").AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Emits a small, stable JSON body. Check descriptions and exceptions are omitted: probes are
    /// often reachable from outside the cluster and must not leak dependency detail.
    /// </summary>
    private static async Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
            }),
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
