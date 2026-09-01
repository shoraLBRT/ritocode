using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Auth;

/// <summary>
/// Authentication, session issuance and linked provider accounts.
/// </summary>
/// <remarks>
/// No behaviour yet — the skeleton exists so the module boundary is in place before the domain
/// logic lands. Endpoints and services arrive with issues #6, #7.
/// </remarks>
public sealed class AuthModule : IModule
{
    public string Name => "Auth";

    public string RoutePrefix => "auth";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty: this module owns no services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
