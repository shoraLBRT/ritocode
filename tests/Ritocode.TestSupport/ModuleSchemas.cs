using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Api.Setup;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.TestSupport;

/// <summary>
/// The PostgreSQL schemas the composed modules own, read from the registrations the modules
/// themselves publish. A hand-written list here would be a second source of truth that goes stale
/// the first time a module is added.
/// </summary>
public static class ModuleSchemas
{
    public static IReadOnlyList<string> All { get; } = Resolve();

    private static IReadOnlyList<string> Resolve()
    {
        // Registration alone never opens a connection — the connection string is validated on
        // host start, which this deliberately does not do — so an empty configuration is enough.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddModules(new ConfigurationBuilder().Build(), ModuleRegistry.All);

        return
        [
            .. services
                .Where(descriptor => descriptor.ServiceType == typeof(ModuleDbContextRegistration))
                .Select(descriptor => ((ModuleDbContextRegistration)descriptor.ImplementationInstance!).Schema)
        ];
    }
}
