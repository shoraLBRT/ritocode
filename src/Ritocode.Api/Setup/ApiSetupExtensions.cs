using Microsoft.Extensions.Options;
using Ritocode.Api.Configuration;
using Ritocode.Api.Endpoints;
using Ritocode.Shared.Http;
using Ritocode.Shared.Modules;

namespace Ritocode.Api.Setup;

/// <summary>
/// Composition root wiring. Kept out of <c>Program.cs</c> so the startup sequence reads as two
/// steps — build services, build the pipeline — and so tests can reuse the exact same wiring.
/// </summary>
public static class ApiSetupExtensions
{
    /// <summary>Named CORS policy applied to module endpoints when origins are configured.</summary>
    public const string CorsPolicyName = "ritocode-frontend";

    public static WebApplicationBuilder AddRitocodeApi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<ApiOptions>()
            .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<AppExceptionHandler>();

        builder.Services.AddHealthChecks();

        // Validators are registered by the module that owns the request type, inside
        // IModule.RegisterServices. WithValidation<T>() resolves IValidator<T> from the container,
        // so no assembly scanning is needed here.

        builder.Services.AddModules(builder.Configuration, ModuleRegistry.All);

        var apiOptions = builder.Configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();

        if (apiOptions.AllowedOrigins.Count > 0)
        {
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
                .WithOrigins([.. apiOptions.AllowedOrigins])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders(RequestId.HeaderName)));
        }

        if (apiOptions.EnableOpenApi)
        {
            builder.Services.AddOpenApi();
        }

        return builder;
    }

    public static WebApplication UseRitocodeApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;

        // Correlation runs first so every later log line and error body carries the request id.
        app.UseMiddleware<RequestIdMiddleware>();
        app.UseExceptionHandler();

        if (options.AllowedOrigins.Count > 0)
        {
            app.UseCors(CorsPolicyName);
        }

        app.MapHealthEndpoints();

        if (options.EnableOpenApi)
        {
            app.MapOpenApi();
        }

        var api = app.MapGroup(options.BasePath);
        api.MapMetaEndpoints();
        api.MapModules(ModuleRegistry.All);

        return app;
    }
}
