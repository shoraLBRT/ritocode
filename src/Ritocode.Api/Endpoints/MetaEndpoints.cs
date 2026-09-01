using Ritocode.Shared.Modules;

namespace Ritocode.Api.Endpoints;

/// <summary>
/// Diagnostics about the running host itself: which modules are composed in and under which
/// routes. Useful when a deployment is suspected of running a different module set than expected.
/// </summary>
public static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/meta/modules", (IReadOnlyList<IModule> modules) =>
                Results.Ok(modules.Select(m => new ModuleInfo(m.Name, m.RoutePrefix)).ToArray()))
            .WithName("GetModules")
            .WithTags("Meta")
            .AllowAnonymous();

        return endpoints;
    }

    public sealed record ModuleInfo(string Name, string RoutePrefix);
}
