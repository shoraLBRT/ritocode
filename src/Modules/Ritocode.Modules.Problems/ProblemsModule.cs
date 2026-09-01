using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Problems;

/// <summary>
/// Problem catalog, problem versions and problem package resolution.
/// </summary>
/// <remarks>
/// No behaviour yet — the skeleton exists so the module boundary is in place before the domain
/// logic lands. Endpoints and services arrive with issues #8, #9, #42.
/// </remarks>
public sealed class ProblemsModule : IModule
{
    public string Name => "Problems";

    public string RoutePrefix => "problems";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty: this module owns no services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
