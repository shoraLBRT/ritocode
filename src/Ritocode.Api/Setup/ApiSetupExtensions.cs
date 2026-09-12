using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Ritocode.Api.Configuration;
using Ritocode.Api.Endpoints;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Modules;
using Ritocode.Shared.Storage;

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

        // Enums go on the wire as their names, not their ordinals. A numeric difficulty would make
        // every client keep a copy of this enum's member order, and a member inserted in the middle
        // would silently change what existing clients read. The database stores these as text for
        // the same reason; camelCase matches how a problem manifest writes them.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<AppExceptionHandler>();

        builder.Services.AddHealthChecks();

        // Storage is host infrastructure, like the database: registered once here so a module takes
        // IObjectStore as a dependency instead of building a client. No readiness check yet —
        // adding one would make `dotnet test` and a bare `dotnet run` require MinIO, which is the
        // property #37 spent a session buying back. It belongs with the first endpoint that reads
        // an object.
        builder.Services.AddObjectStorage(builder.Configuration);

        // The identity seam is host infrastructure like storage: ICurrentUser is what every
        // endpoint takes its user from, and the development identity settings below are read by two
        // modules that may not reference each other. What authenticates a request is the Auth
        // module's business and is registered there.
        builder.Services.AddRitocodeIdentity(builder.Configuration);

        // Authenticated by default, anonymous by exception. A fallback policy applies to every
        // endpoint that states no authorisation requirement of its own, so a workspace or
        // submission endpoint added later is protected because nobody remembered to protect it —
        // which is the failure mode worth designing against. Endpoints meant to stay open say
        // AllowAnonymous where they are mapped, and every one that exists today already does.
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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

        // Added explicitly rather than left to WebApplication's automatic insertion, so the order
        // is readable here: after CORS, because a rejected preflight must not depend on a
        // credential, and before any endpoint runs.
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthEndpoints();

        if (options.EnableOpenApi)
        {
            // The document describes the API; reading it is not a protected action, and the
            // fallback policy would otherwise put a 401 in front of it.
            app.MapOpenApi().AllowAnonymous();
        }

        var api = app.MapGroup(options.BasePath);
        api.MapMetaEndpoints();
        api.MapModules(ModuleRegistry.All);

        return app;
    }
}
