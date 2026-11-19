using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Admin;

/// <summary>
/// The admin area's system page (#297) under <c>/api/v1/admin</c>, for admins only: a signed-in non-admin gets
/// the 404 of an unknown address (<see cref="AdminPolicy"/>). Host-level, as it reads every module and belongs
/// to none.
/// </summary>
public static class SystemEndpoints
{
    public static IServiceCollection AddSystemPage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SystemOptions>().Bind(configuration.GetSection(SystemOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(SystemReader.UmamiClient, http => http.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<SystemReader>();

        return services;
    }

    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/admin/system", ReadAsync)
            .WithName("AdminReadSystem")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        return endpoints;
    }

    private static async Task<IResult> ReadAsync(SystemReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(cancellationToken));
}
