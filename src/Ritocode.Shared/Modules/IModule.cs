using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ritocode.Shared.Modules;

/// <summary>
/// The contract every Ritocode module implements. A module owns its endpoints, domain types and
/// persistence; the API host only knows this interface. Modules are registered explicitly in the
/// host (no assembly scanning), so the full set of modules is readable in one place.
/// </summary>
public interface IModule
{
    /// <summary>Stable module name, used in logs, health reporting and the module inventory endpoint.</summary>
    string Name { get; }

    /// <summary>Route prefix owned by this module, relative to the API version segment, e.g. <c>problems</c>.</summary>
    string RoutePrefix { get; }

    /// <summary>Registers the module's services into the host container.</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's endpoints under the shared versioned API group.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
