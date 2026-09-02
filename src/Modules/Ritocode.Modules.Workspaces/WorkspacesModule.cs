using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Workspaces;

/// <summary>
/// Workspace lifecycle: creation from a problem version, file tree, drafts and resets.
/// </summary>
/// <remarks>
/// Owns the <c>workspaces</c> schema. No endpoints yet — those arrive with issues #10 to #13 and #43.
/// </remarks>
public sealed class WorkspacesModule : IModule
{
    public string Name => "Workspaces";

    public string RoutePrefix => "workspaces";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<WorkspacesDbContext>(configuration, WorkspacesDbContext.SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
