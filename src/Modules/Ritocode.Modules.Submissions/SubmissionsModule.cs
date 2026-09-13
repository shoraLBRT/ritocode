using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Submissions.Lifecycle;
using Ritocode.Modules.Submissions.Persistence;
using Ritocode.Modules.Submissions.Queue;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Submissions;

/// <summary>
/// Submission lifecycle, attempt history and report retrieval.
/// </summary>
/// <remarks>
/// Owns the <c>submissions</c> schema and the frozen input trees under
/// <c>evaluation-artifacts/submissions/</c>. Submitting a workspace, reading an attempt and the attempt
/// history exist (#14); the queue worker arrives with #15 and the report with #16.
/// </remarks>
public sealed class SubmissionsModule : IModule
{
    public string Name => "Submissions";

    public string RoutePrefix => "submissions";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<SubmissionsDbContext>(configuration, SubmissionsDbContext.SchemaName);

        services.AddScoped<ISubmissionLifecycle, SubmissionLifecycle>();
        services.AddScoped<IValidator<SubmitRequest>, SubmitRequestValidator>();

        // The queue (#15, ADR 0009). No hosted loop drains it yet: that arrives with the runner in stage 5.
        services.AddOptions<SubmissionQueueOptions>()
            .Bind(configuration.GetSection(SubmissionQueueOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ISubmissionDispatcher, SubmissionDispatcher>();

        // TryAdd, as the other modules do: the clock is host infrastructure.
        services.TryAddSingleton(TimeProvider.System);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup(RoutePrefix)
            .MapSubmissionEndpoints();
    }
}
