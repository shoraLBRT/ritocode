using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Users.Admin;
using Ritocode.Modules.Users.Contracts;
using Ritocode.Modules.Users.Identity;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Users;

/// <summary>
/// User accounts, profiles and account-level settings.
/// </summary>
/// <remarks>
/// Owns the <c>users</c> schema, and with it the row behind the seeded development identity the
/// Auth module's scheme asserts. Answers <c>IUserLookup</c> for the modules that store a user
/// reference, <c>IUserAccounts</c> for the Auth module signing someone in, and
/// <c>IUserContactLookup</c> for the admin area. Says who is an admin — named in configuration by
/// e-mail (docs/SPEC.md §6.2) — by meeting the admin policy's requirement, and serves the admin
/// area's list of users.
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

        // The contract other modules validate a user reference through (ADR 0007). Scoped, like
        // the context it reads.
        services.AddScoped<IUserLookup, UserLookup>();
        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddScoped<IUserContactLookup, UserContactLookup>();

        services.AddOptions<AdminOptions>()
            .Bind(configuration.GetSection(AdminOptions.SectionName))
            .Validate(
                options => options.Emails.All(email => !string.IsNullOrWhiteSpace(email) && email.Contains('@', StringComparison.Ordinal)),
                $"{AdminOptions.SectionName}:Emails must hold e-mail addresses only.")
            .ValidateOnStart();

        // Scoped: it reads the caller's address from this module's context.
        services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<DevelopmentIdentitySeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapAdminUserEndpoints();
    }
}
