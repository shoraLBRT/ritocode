using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Evaluations;

/// <summary>
/// Evaluation orchestration, validator plugins and verdict aggregation.
/// </summary>
/// <remarks>
/// Owns no schema: per ADR 0009 it answers one command — evaluate this input — and the Submissions
/// module records the outcome. The validator plugin interface, its result schema and the registry exist
/// (#18), the pipeline that runs a version's validators step by step (#17), and the sandbox runner that
/// runs a step (#21). The pipeline stays unregistered until the evaluation path is wired — its own box in
/// stage 5, after the image (#22), the compile and test validators (#19) and the verdict rules (#20).
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

        // The sandbox runner (#21). Registering it contacts nothing: Docker is first reached by a run.
        services.AddOptions<SandboxRunnerOptions>()
            .Bind(configuration.GetSection(SandboxRunnerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();

        // TryAdd, as the other modules do: the clock is host infrastructure.
        services.TryAddSingleton(TimeProvider.System);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: evaluation is reached through the Submissions module, never over HTTP.
    }
}
