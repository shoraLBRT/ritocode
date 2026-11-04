using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ritocode.Shared.Identity;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the identity seam: <see cref="ICurrentUser"/>, the admin policy's refusal, and the development identity
    /// settings the Auth and Users modules both read.
    /// </summary>
    /// <remarks>
    /// Called once from the composition root, because this is host
    /// infrastructure a module consumes rather than something a module owns. Binding the options
    /// here rather than in each module is what keeps one configuration section from being bound —
    /// and validated — twice.
    /// </remarks>
    public static IServiceCollection AddRitocodeIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // A non-admin refused by the admin policy gets the 404 of an unknown address, not a 403.
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AdminRefusalAsNotFound>();

        services.AddOptions<DevelopmentIdentityOptions>()
            .Bind(configuration.GetSection(DevelopmentIdentityOptions.SectionName))
            .ValidateDataAnnotations()
            // An empty identifier would authenticate every request as a user no row can match, and
            // the failure would surface three layers away as a workspace that cannot be created.
            .Validate(
                options => !options.Enabled || options.UserId != Guid.Empty,
                $"{DevelopmentIdentityOptions.SectionName}:UserId must be a non-empty GUID when the development identity is enabled.")
            .ValidateOnStart();

        return services;
    }
}
