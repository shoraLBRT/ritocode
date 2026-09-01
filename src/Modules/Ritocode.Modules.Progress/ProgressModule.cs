using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Progress;

/// <summary>
/// User progress, XP calculation and leaderboards.
/// </summary>
/// <remarks>
/// No behaviour yet — the skeleton exists so the module boundary is in place before the domain
/// logic lands. Endpoints and services arrive with issues #24, #25.
/// </remarks>
public sealed class ProgressModule : IModule
{
    public string Name => "Progress";

    public string RoutePrefix => "progress";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty: this module owns no services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
