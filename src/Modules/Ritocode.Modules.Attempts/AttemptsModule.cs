using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Attempts.Contracts;
using Ritocode.Modules.Attempts.Lifecycle;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Progress;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Modules.Attempts.Signals;
using Ritocode.Shared.Contracts.Attempts;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Attempts;

/// <summary>
/// A learner's attempts at tasks, their scoring and progress, and the signals sent from their reviews
/// (docs/SPEC.md §4.7, §4.8, §5).
/// </summary>
/// <remarks>
/// Owns the <c>attempts</c> schema. Reads a task and its answer key through
/// <see cref="Shared.Contracts.Content.ITaskForAttemptLookup"/>, and answers the task catalogue's
/// solved flags through <see cref="ISubmittedTaskLookup"/>.
/// </remarks>
public sealed class AttemptsModule : IModule
{
    public string Name => "Attempts";

    public string RoutePrefix => "attempts";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<AttemptsDbContext>(configuration, AttemptsDbContext.SchemaName);

        services.AddOptions<ScoringParameters>()
            .Bind(configuration.GetSection(ScoringParameters.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AttemptRateLimitOptions>()
            .Bind(configuration.GetSection(AttemptRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SignalRateLimitOptions>()
            .Bind(configuration.GetSection(SignalRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IAttemptLifecycle, AttemptLifecycle>();
        services.AddScoped<IValidator<StartAttemptRequest>, StartAttemptRequestValidator>();
        services.AddScoped<IValidator<RecordStepRequest>, RecordStepRequestValidator>();
        services.AddScoped<IValidator<SubmitAttemptRequest>, SubmitAttemptRequestValidator>();

        services.AddScoped<ISubmittedTaskLookup, SubmittedTaskLookup>();
        services.AddScoped<IProgressReader, ProgressReader>();
        services.AddScoped<ISignalSender, SignalSender>();
        services.AddScoped<IValidator<SendSignalRequest>, SendSignalRequestValidator>();

        // TryAdd, as the other modules do: the clock is host infrastructure.
        services.TryAddSingleton(TimeProvider.System);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup(RoutePrefix).MapAttemptEndpoints();
        endpoints.MapProgressEndpoints();
        endpoints.MapSignalEndpoints();
    }
}
