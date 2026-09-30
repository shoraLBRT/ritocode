using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Auth.Identity;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.Session;
using Ritocode.Modules.Auth.SignIn;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Auth;

/// <summary>
/// Authentication, sessions and linked provider accounts.
/// </summary>
/// <remarks>
/// Owns the <c>auth</c> schema and the platform's authentication: the session cookie of ADR 0012,
/// the seeded development identity of ADR 0008, <c>/me</c>, <c>/auth/logout</c>, and signing in with
/// GitHub or Google (#7), which links the provider's identity to a user and starts a session.
/// </remarks>
public sealed class AuthModule : IModule
{
    public string Name => "Auth";

    public string RoutePrefix => "auth";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<AuthDbContext>(configuration, AuthDbContext.SchemaName);

        services.AddOptions<SessionOptions>()
            .Bind(configuration.GetSection(SessionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ISessionIssuer, SessionIssuer>();

        services.AddOptions<SignInOptions>()
            .Bind(configuration.GetSection(SignInOptions.SectionName));

        services.AddScoped<AccountLinker>();

        // TryAdd, as the other modules do: the clock is host infrastructure.
        services.TryAddSingleton(TimeProvider.System);

        // The module that owns authentication registers the schemes; the host owns where
        // UseAuthentication sits in the pipeline. The default sends a request that carries the
        // session cookie to the session scheme and any other to the development identity, so a real
        // session wins even where the development identity is on.
        var authentication = services.AddAuthentication(RitocodeAuthenticationSchemes.Default);
        authentication
            .AddPolicyScheme(RitocodeAuthenticationSchemes.Default, displayName: null, options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Cookies.ContainsKey(SessionCookies.SessionName)
                        ? RitocodeAuthenticationSchemes.Session
                        : RitocodeAuthenticationSchemes.DevelopmentIdentity)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                RitocodeAuthenticationSchemes.Session,
                displayName: null,
                configureOptions: null)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentIdentityAuthenticationHandler>(
                RitocodeAuthenticationSchemes.DevelopmentIdentity,
                displayName: null,
                configureOptions: null);

        // GitHub and Google, each only when its client is configured.
        OAuthProviders.Add(authentication, configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMeEndpoints();
    }

    public void MapHostEndpoints(IEndpointRouteBuilder root)
    {
        ArgumentNullException.ThrowIfNull(root);

        root.MapLoginEndpoints();
        root.MapLogoutEndpoints();
    }
}
