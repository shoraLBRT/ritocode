using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Auth;

/// <summary>
/// Authentication, session issuance and linked provider accounts.
/// </summary>
/// <remarks>
/// Owns the <c>auth</c> schema. No endpoints yet — those arrive with issues #6 and #7.
/// </remarks>
public sealed class AuthModule : IModule
{
    public string Name => "Auth";

    public string RoutePrefix => "auth";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<AuthDbContext>(configuration, AuthDbContext.SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
