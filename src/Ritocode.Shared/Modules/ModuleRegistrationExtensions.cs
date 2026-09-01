using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ritocode.Shared.Modules;

public static class ModuleRegistrationExtensions
{
    /// <summary>
    /// Registers each module's services and publishes the module list itself, so later stages
    /// (endpoint mapping, diagnostics) can resolve the same instances.
    /// </summary>
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(modules);

        var duplicate = modules.GroupBy(m => m.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module name '{duplicate.Key}' is registered more than once.");
        }

        services.AddSingleton(modules);

        foreach (var module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    /// <summary>Maps every registered module's endpoints under <paramref name="root"/>.</summary>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder root, IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.MapEndpoints(root);
        }

        return root;
    }
}
