using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Problems;

/// <summary>
/// Problem catalog, problem versions and problem package resolution.
/// </summary>
/// <remarks>
/// Owns the <c>problems</c> schema. No endpoints yet — those arrive with issues #8, #9 and #42.
/// </remarks>
public sealed class ProblemsModule : IModule
{
    public string Name => "Problems";

    public string RoutePrefix => "problems";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<ProblemsDbContext>(configuration, ProblemsDbContext.SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
