using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ritocode.Api.Setup;
using Ritocode.Modules.Problems.Ingest;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Validation;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real host wiring (<see cref="ApiSetupExtensions"/>) on an in-memory server, then adds
/// a few probe endpoints. Testing through the production composition root means the middleware
/// order under test is the one that ships; the probes only exist to trigger behaviour no real
/// endpoint provides yet.
/// </summary>
/// <remarks>
/// Shared by every fixture in this assembly, so the fixtures differ only in the one setting they
/// exist to vary - see <see cref="TestApi"/> and <see cref="AnonymousTestApi"/>.
/// </remarks>
internal sealed class TestApiHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApiHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<TestApiHost> StartAsync(string connectionString, bool developmentIdentityEnabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.WebHost.UseTestServer();

        // Module DbContexts validate their settings at startup, and the readiness probe queries
        // each schema, so the host needs a real database even for tests that never touch one.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = connectionString,
            // Retries would turn an unreachable database into a slow failure rather than an
            // immediate, legible one.
            ["Database:MaxRetryCount"] = "0",
            // The API project's appsettings.Development.json is copied into this assembly's output
            // and the environment above is Development, so content seeding arrives switched on.
            // Off here explicitly: a seeding test host would need MinIO to start, and today only a
            // content directory that happens not to exist beside the test binaries prevents it.
            [$"{ProblemContentOptions.SectionName}:SeedOnStartup"] = "false",
            // Explicit in both directions, for the same reason: that same file switches the
            // development identity on, so a fixture wanting the anonymous case has to say so rather
            // than inherit it.
            [$"{DevelopmentIdentityOptions.SectionName}:Enabled"] = developmentIdentityEnabled ? "true" : "false",
        });

        builder.AddRitocodeApi();
        builder.Services.AddScoped<IValidator<EchoRequest>, EchoRequestValidator>();

        var app = builder.Build();
        app.UseRitocodeApi();

        MapProbeEndpoints(app);

        await app.StartAsync();

        return new TestApiHost(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private static void MapProbeEndpoints(WebApplication app)
    {
        app.MapGet("/__probe/unhandled", IResult () => throw new InvalidOperationException("probe failure"))
            .AllowAnonymous();

        app.MapGet("/__probe/app-error", IResult () =>
                throw new AppException(AppError.NotFound("probe_not_found", "Probe resource is missing.")))
            .AllowAnonymous();

        app.MapPost("/__probe/echo", (EchoRequest request) => Results.Ok(request))
            .WithValidation<EchoRequest>()
            .AllowAnonymous();

        // No AllowAnonymous, deliberately: this is the shape every workspace and submission
        // endpoint from stage 3 on will have, and it is what proves the host's fallback policy
        // protects an endpoint that says nothing about authorisation at all.
        app.MapGet("/__probe/current-user", (ICurrentUser currentUser) =>
            Results.Ok(new CurrentUserProbe(currentUser.RequireId())));
    }

    private sealed class EchoRequestValidator : AbstractValidator<EchoRequest>
    {
        public EchoRequestValidator()
        {
            RuleFor(r => r.Title).NotEmpty().MaximumLength(10);
            RuleFor(r => r.Count).GreaterThan(0);
        }
    }
}

public sealed record EchoRequest(string Title, int Count);

public sealed record CurrentUserProbe(Guid UserId);
