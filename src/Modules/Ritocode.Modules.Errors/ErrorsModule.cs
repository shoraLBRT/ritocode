using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ritocode.Modules.Errors.Admin;
using Ritocode.Modules.Errors.Alerts;
using Ritocode.Modules.Errors.Browser;
using Ritocode.Modules.Errors.Persistence;
using Ritocode.Modules.Errors.Recording;
using Ritocode.Shared.Diagnostics;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Errors;

/// <summary>
/// The errors that reach the maintainer (ADR 0020 decision 1, #294): unhandled server exceptions and browser
/// errors, grouped by fingerprint, listed on <c>/admin/errors</c>, a new or reopened group told on Telegram.
/// </summary>
/// <remarks>
/// Owns the <c>errors</c> schema. Its rows hold no request body, no address and no user id, so it has no
/// <see cref="Shared.PersonalData.IPersonalData"/> part (ADR 0021). It answers the host's
/// <see cref="IServerErrorRecorder"/>, which the exception handler calls.
/// </remarks>
public sealed class ErrorsModule : IModule
{
    public string Name => "Errors";

    public string RoutePrefix => "client-errors";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<ErrorsDbContext>(configuration, ErrorsDbContext.SchemaName);

        services.AddOptions<ErrorsOptions>()
            .Bind(configuration.GetSection(ErrorsOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.OccurrenceRetention > TimeSpan.Zero && options.PruneInterval > TimeSpan.Zero
                    && options.Browser.Window > TimeSpan.Zero && options.Browser.MaxPerAddress > 0 && options.Browser.MaxTotal > 0
                    && options.Browser.MaxBodyBytes >= 1024 && options.Telegram.MaxPerHour > 0,
                $"{ErrorsOptions.SectionName}: the retention, the intervals and the limits must be positive.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<ErrorRecorder>();
        services.AddSingleton<IServerErrorRecorder>(provider => provider.GetRequiredService<ErrorRecorder>());
        services.AddSingleton<ClientErrorRateLimiter>();
        services.AddHostedService<ErrorPruner>();

        services.AddSingleton<QueuedErrorAlerts>();
        services.AddSingleton<IErrorAlerts>(provider => provider.GetRequiredService<QueuedErrorAlerts>());
        services.AddHostedService<ErrorAlertSender>();

        // Not logged by the client factory: the bot's token is in the address it would write.
        services.AddHttpClient<TelegramErrorAlerts>(http => http.Timeout = TimeSpan.FromSeconds(10)).RemoveAllLoggers();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapClientErrorEndpoints();
        endpoints.MapAdminErrorEndpoints();
    }
}
