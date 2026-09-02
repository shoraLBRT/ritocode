using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Users;

/// <summary>
/// User accounts, profiles and account-level settings.
/// </summary>
/// <remarks>
/// Owns the <c>users</c> schema. No endpoints yet — those arrive with issue #25.
/// </remarks>
public sealed class UsersModule : IModule
{
    public string Name => "Users";

    public string RoutePrefix => "users";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<UsersDbContext>(configuration, UsersDbContext.SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
