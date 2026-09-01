using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Users;

/// <summary>
/// User accounts, profiles and account-level settings.
/// </summary>
/// <remarks>
/// No behaviour yet — the skeleton exists so the module boundary is in place before the domain
/// logic lands. Endpoints and services arrive with issues #25.
/// </remarks>
public sealed class UsersModule : IModule
{
    public string Name => "Users";

    public string RoutePrefix => "users";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty: this module owns no services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
