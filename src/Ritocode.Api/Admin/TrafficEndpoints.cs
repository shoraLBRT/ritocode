using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Admin;

/// <summary>
/// The admin overview's traffic panel (#306) under <c>/api/v1/admin/stats/traffic</c>, for admins only: a
/// signed-in non-admin gets the 404 of an unknown address (<see cref="AdminPolicy"/>). Host-level, as it reads
/// Umami and belongs to no module.
/// </summary>
public static class TrafficEndpoints
{
    public static IServiceCollection AddTrafficStats(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SystemOptions>().Bind(configuration.GetSection(SystemOptions.SectionName));
        services.AddOptions<TrafficOptions>().Bind(configuration.GetSection(TrafficOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(TrafficReader.UmamiClient, http => http.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<TrafficReader>();

        return services;
    }

    public static IEndpointRouteBuilder MapTrafficEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/admin/stats/traffic", ReadAsync)
            .WithName("AdminReadTraffic")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        return endpoints;
    }

    /// <summary><c>?from=&amp;to=</c> as UTC dates (<c>yyyy-MM-dd</c>, both included; the last 30 days by default), as the usage over time reads them.</summary>
    private static async Task<IResult> ReadAsync(
        TrafficReader reader,
        TimeProvider clock,
        HttpContext context,
        string? from,
        string? to,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var (first, last) = ActivityEndpoints.ParseRange(from, to, today, fields);

        if (fields.Count > 0)
        {
            return ApiProblem.ToResult(AppError.Validation("Invalid query parameters.", fields), context);
        }

        return Results.Ok(await reader.ReadAsync(first, last, cancellationToken));
    }
}
