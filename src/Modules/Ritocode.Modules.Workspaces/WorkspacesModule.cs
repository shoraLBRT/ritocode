using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Workspaces;

/// <summary>
/// Workspace lifecycle: creation from a problem version, file tree, drafts and resets.
/// </summary>
/// <remarks>
/// Owns the <c>workspaces</c> schema and the <c>workspace-snapshots</c> objects its rows point at.
/// Opening a workspace and reading it back exist (#10); the file tree, file writes and resets arrive
/// with #11 to #13, and cleanup with #43.
/// </remarks>
public sealed class WorkspacesModule : IModule
{
    public string Name => "Workspaces";

    public string RoutePrefix => "workspaces";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<WorkspacesDbContext>(configuration, WorkspacesDbContext.SchemaName);

        services.AddScoped<IWorkspaceLifecycle, WorkspaceLifecycle>();
        services.AddScoped<IValidator<OpenWorkspaceRequest>, OpenWorkspaceRequestValidator>();

        // TryAdd, as Problems does: the clock is host infrastructure, and whichever module registers
        // it first must not stop another from asking for it.
        services.TryAddSingleton(TimeProvider.System);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup(RoutePrefix).MapWorkspaceEndpoints();
    }
}
