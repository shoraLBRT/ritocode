using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Users.Identity;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Users;

/// <summary>
/// User accounts, profiles and account-level settings.
/// </summary>
/// <remarks>
/// Owns the <c>users</c> schema, and with it the row behind the seeded development identity the
/// Auth module's scheme asserts. No endpoints yet — those arrive with issue #25.
/// </remarks>
public sealed class UsersModule : IModule
{
    public string Name => "Users";

    public string RoutePrefix => "users";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<UsersDbContext>(configuration, UsersDbContext.SchemaName);

        services.AddHostedService<DevelopmentIdentitySeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
