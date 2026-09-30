using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Modules;

namespace Ritocode.Modules.Attempts;

/// <summary>
/// A learner's attempts at tasks and their scoring (docs/SPEC.md §5); later, progress and signals.
/// </summary>
/// <remarks>
/// So far only the scoring function and its parameters (<see href="https://github.com/shoraLBRT/ritocode/issues/20">#20</see>).
/// The <c>attempts</c> schema, the answer key from Content and the endpoints are
/// <see href="https://github.com/shoraLBRT/ritocode/issues/125">#125</see>.
/// </remarks>
public sealed class AttemptsModule : IModule
{
    public string Name => "Attempts";

    public string RoutePrefix => "attempts";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ScoringParameters>()
            .Bind(configuration.GetSection(ScoringParameters.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty: the attempt endpoints are #125.
    }
}
