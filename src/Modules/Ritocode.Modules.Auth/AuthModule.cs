using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Auth.Identity;
using Ritocode.Modules.Auth.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Auth;

/// <summary>
/// Authentication, session issuance and linked provider accounts.
/// </summary>
/// <remarks>
/// Owns the <c>auth</c> schema and the platform's authentication scheme. For the slice that scheme
/// is the seeded development identity ADR 0005 allows; login, session issuance and <c>/me</c> are
/// the rest of <see href="https://github.com/shoraLBRT/ritocode/issues/6">#6</see>, and provider
/// linking is <see href="https://github.com/shoraLBRT/ritocode/issues/7">#7</see>.
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

        // The module that owns authentication is the one that registers the scheme, so replacing it
        // in stage two is an edit here rather than in the composition root. The host still owns
        // where UseAuthentication sits in the pipeline, which is a pipeline-ordering decision and
        // not a module's to make.
        services.AddAuthentication(RitocodeAuthenticationSchemes.DevelopmentIdentity)
            .AddScheme<AuthenticationSchemeOptions, DevelopmentIdentityAuthenticationHandler>(
                RitocodeAuthenticationSchemes.DevelopmentIdentity,
                displayName: null,
                configureOptions: null);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
