using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Submissions;

/// <summary>
/// Submission lifecycle, attempt history and report retrieval.
/// </summary>
/// <remarks>
/// No behaviour yet — the skeleton exists so the module boundary is in place before the domain
/// logic lands. Endpoints and services arrive with issues #14, #15, #16.
/// </remarks>
public sealed class SubmissionsModule : IModule
{
    public string Name => "Submissions";

    public string RoutePrefix => "submissions";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Intentionally empty: this module owns no services yet.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
