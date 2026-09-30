using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Auth.Identity;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Modules.Auth.Session;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Auth;

/// <summary>
/// Authentication, sessions and linked provider accounts.
/// </summary>
/// <remarks>
/// Owns the <c>auth</c> schema and the platform's authentication: the session cookie of ADR 0012,
/// the seeded development identity of ADR 0008, <c>/me</c> and <c>/auth/logout</c>. Signing in with
/// a provider, which starts a session, is <see href="https://github.com/shoraLBRT/ritocode/issues/7">#7</see>.
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

        // TryAdd, as the other modules do: the clock is host infrastructure.
        services.TryAddSingleton(TimeProvider.System);

        // The module that owns authentication registers the schemes; the host owns where
        // UseAuthentication sits in the pipeline. The default sends a request that carries the
        // session cookie to the session scheme and any other to the development identity, so a real
        // session wins even where the development identity is on.
        services.AddAuthentication(RitocodeAuthenticationSchemes.Default)
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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMeEndpoints();
    }

    public void MapHostEndpoints(IEndpointRouteBuilder root)
    {
        ArgumentNullException.ThrowIfNull(root);

        root.MapLogoutEndpoints();
    }
}
