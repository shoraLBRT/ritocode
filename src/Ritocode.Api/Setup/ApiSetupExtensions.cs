using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Ritocode.Api.Configuration;
using Ritocode.Api.Endpoints;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
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

        // One line per request, which its request id finds: the method, the path, the status and
        // the time taken. No headers, query or bodies — they carry credentials and personal data.
        // The level is the category's, `Microsoft.AspNetCore.HttpLogging` in appsettings.json.
        builder.Services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });

        builder.Services
            .AddOptions<ApiOptions>()
            .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Enums go on the wire as their names, not their ordinals. A numeric difficulty would make
        // every client keep a copy of this enum's member order, and a member inserted in the middle
        // would silently change what existing clients read. The database stores these as text for
        // the same reason; camelCase matches how a problem manifest writes them.
        // Timestamps go out in UTC with an explicit Z (ADR 0003), not the serializer's +00:00.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.SerializerOptions.Converters.Add(new UtcTimestampJsonConverter());
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<AppExceptionHandler>();

        // A request an endpoint cannot bind — a body that is not UTF-8 JSON, a query value of the
        // wrong type, the wrong content type — throws to the handler above, which answers it in the
        // unified error body. The framework throws only in Development and otherwise writes a bare
        // 400 with no body at all, which ADR 0003 does not allow.
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        builder.Services.AddHealthChecks();

        // The identity seam is host infrastructure: ICurrentUser is what every
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
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            // The admin area (SPEC §6.2). Who is an admin is the Users module's handler's to say.
            .AddPolicy(AdminPolicy.Name, AdminPolicy.Build());

        // Validators are registered by the module that owns the request type, inside
        // IModule.RegisterServices. WithValidation<T>() resolves IValidator<T> from the container,
        // so no assembly scanning is needed here.

        builder.Services.AddModules(builder.Configuration, ModuleRegistry.All);

        var apiOptions = builder.Configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();

        // A body larger than any request the product makes is refused by the server before it is read.
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = apiOptions.MaxRequestBodyBytes);

        // Behind the production proxy: its scheme and the client's address are the request's.
        if (apiOptions.BehindProxy)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // The proxy's address on the Compose network is not fixed; the network is the boundary.
                forwarded.KnownIPNetworks.Clear();
                forwarded.KnownProxies.Clear();
            });
        }

        // A key ring that outlives the container, so a release does not fail sign-ins in flight.
        if (!string.IsNullOrWhiteSpace(apiOptions.DataProtectionKeysDirectory))
        {
            builder.Services.AddDataProtection()
                .SetApplicationName("ritocode")
                .PersistKeysToFileSystem(new DirectoryInfo(apiOptions.DataProtectionKeysDirectory));
        }

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

        // Before anything reads the scheme or the client's address.
        if (options.BehindProxy)
        {
            app.UseForwardedHeaders();
        }

        // Correlation runs first so every later log line and error body carries the request id.
        app.UseMiddleware<RequestIdMiddleware>();

        // Registered before the exception handler, so the headers reach a 500 too.
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();

        // Routing's own 405 and 415, and an unknown address under the API, get the unified error body
        // as well: they are written before any endpoint runs, so the exception handler never sees them.
        app.UseRoutingRefusalBodies(options.BasePath);

        if (options.AllowedOrigins.Count > 0)
        {
            app.UseCors(CorsPolicyName);
        }

        // Added explicitly rather than left to WebApplication's automatic insertion, so the order
        // is readable here: after CORS, because a rejected preflight must not depend on a
        // credential, and before any endpoint runs.
        app.UseAuthentication();

        // Every line from here on names the caller's user id; the one-line summary of the request
        // below carries it and the request id, and comes before authorisation so a refused request
        // is logged too.
        app.UseMiddleware<UserLogScopeMiddleware>();
        app.UseHttpLogging();

        // After authentication, which says whether the request rode on the session cookie, and
        // before any endpoint: a forged state-changing request never reaches one.
        app.UseMiddleware<CsrfProtectionMiddleware>();

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
        app.MapModuleHostEndpoints(ModuleRegistry.All);

        return app;
    }
}
