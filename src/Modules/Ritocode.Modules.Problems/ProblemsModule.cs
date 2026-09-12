using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Problems.Catalog;
using Ritocode.Modules.Problems.Contracts;
using Ritocode.Modules.Problems.Ingest;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Problems;

/// <summary>
/// Problem catalog, problem versions and problem package resolution.
/// </summary>
/// <remarks>
/// Owns the <c>problems</c> schema, the problem package format in <c>Packaging</c>
/// (docs/PROBLEM_PACKAGE_SPEC.md), the ingest that turns a package into a published version, and
/// the read side that serves it.
/// </remarks>
public sealed class ProblemsModule : IModule
{
    public string Name => "Problems";

    public string RoutePrefix => "problems";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<ProblemsDbContext>(configuration, ProblemsDbContext.SchemaName);

        services.AddOptions<ProblemContentOptions>()
            .Bind(configuration.GetSection(ProblemContentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IProblemCatalog, ProblemCatalog>();
        services.AddScoped<IProblemIngest, ProblemIngestService>();

        // The contract other modules read a problem version through (ADR 0007).
        services.AddScoped<IProblemVersionLookup, ProblemVersionLookup>();

        // TryAdd: the clock is host infrastructure that any module may want, and the first module
        // to ask for it should not be the one that decides nobody else may register it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<ProblemContentSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup(RoutePrefix).MapProblemCatalogEndpoints();
    }
}
