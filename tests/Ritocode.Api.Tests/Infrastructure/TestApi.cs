using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ritocode.Api.Setup;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Validation;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real host wiring (<see cref="ApiSetupExtensions"/>) on an in-memory server, then
/// adds a few probe endpoints. Testing through the production composition root means the
/// middleware order under test is the one that ships; the probes only exist to trigger
/// behaviour no real endpoint provides yet.
/// </summary>
public sealed class TestApi : IAsyncLifetime
{
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.WebHost.UseTestServer();

        builder.AddRitocodeApi();
        builder.Services.AddScoped<IValidator<EchoRequest>, EchoRequestValidator>();

        _app = builder.Build();
        _app.UseRitocodeApi();

        MapProbeEndpoints(_app);

        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static void MapProbeEndpoints(WebApplication app)
    {
        app.MapGet("/__probe/unhandled", IResult () => throw new InvalidOperationException("probe failure"));

        app.MapGet("/__probe/app-error", IResult () =>
            throw new AppException(AppError.NotFound("probe_not_found", "Probe resource is missing.")));

        app.MapPost("/__probe/echo", (EchoRequest request) => Results.Ok(request))
            .WithValidation<EchoRequest>();
    }

    public sealed record EchoRequest(string Title, int Count);

    private sealed class EchoRequestValidator : AbstractValidator<EchoRequest>
    {
        public EchoRequestValidator()
        {
            RuleFor(r => r.Title).NotEmpty().MaximumLength(10);
            RuleFor(r => r.Count).GreaterThan(0);
        }
    }
}
