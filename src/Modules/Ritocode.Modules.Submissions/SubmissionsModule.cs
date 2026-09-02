using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Submissions;

/// <summary>
/// Submission lifecycle, attempt history and report retrieval.
/// </summary>
/// <remarks>
/// Owns the <c>submissions</c> schema. No endpoints yet — those arrive with issues #14, #15 and #16.
/// </remarks>
public sealed class SubmissionsModule : IModule
{
    public string Name => "Submissions";

    public string RoutePrefix => "submissions";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SubmissionsDbContext>(configuration, SubmissionsDbContext.SchemaName);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: this module exposes no endpoints yet.
    }
}
