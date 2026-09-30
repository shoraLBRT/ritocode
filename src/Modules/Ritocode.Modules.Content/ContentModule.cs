using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Content;

/// <summary>
/// Problem cards, the treatment tree, materials and tasks (docs/SPEC.md §3).
/// </summary>
/// <remarks>
/// Owns the <c>content</c> schema, the content format in <c>Format</c> (docs/CONTENT_FORMAT.md) and
/// the ingest that loads it. The read APIs are #9.
/// </remarks>
public sealed class ContentModule : IModule
{
    public string Name => "Content";

    public string RoutePrefix => "content";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<ContentDbContext>(configuration, ContentDbContext.SchemaName);

        services.AddOptions<ContentSeedOptions>()
            .Bind(configuration.GetSection(ContentSeedOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IContentIngest, ContentIngest>();

        // TryAdd: the clock is host infrastructure any module may want.
        services.TryAddSingleton(TimeProvider.System);

        services.AddHostedService<ContentSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Intentionally empty until the read APIs of #9.
    }
}
