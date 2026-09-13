using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Evaluations.Validators;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Evaluations;

/// <summary>
/// Evaluation orchestration, validator plugins and verdict aggregation.
/// </summary>
/// <remarks>
/// Owns no schema: per ADR 0009 it answers one command — evaluate this input — and the Submissions
/// module records the outcome. The validator plugin interface, its result schema and the registry exist
/// (#18); no plugin is registered until the compile and test validators of #19, and the orchestrator
/// and its contract arrive with #17, the verdict rules with #20.
/// </remarks>
public sealed class EvaluationsModule : IModule
{
    public string Name => "Evaluations";

    public string RoutePrefix => "evaluations";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Plugins register themselves as IValidatorPlugin beside this, one line each, from #19 on.
        services.AddSingleton<IValidatorPluginRegistry, ValidatorPluginRegistry>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: evaluation is reached through the Submissions module, never over HTTP.
    }
}
