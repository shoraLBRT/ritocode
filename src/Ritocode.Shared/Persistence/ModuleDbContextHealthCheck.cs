using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ritocode.Shared.Persistence;

/// <summary>
/// Readiness check for one module's database schema. Runs a trivial query rather than opening a
/// connection and closing it, so a database that accepts connections but cannot serve queries —
/// exhausted connection slots, a schema the role cannot see — is reported unhealthy.
/// </summary>
public sealed class ModuleDbContextHealthCheck<TContext>(TContext dbContext) : IHealthCheck
    where TContext : ModuleDbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The message is not surfaced by the probe response writer, which keeps dependency
            // detail out of a publicly reachable endpoint; it reaches the logs instead.
            return HealthCheckResult.Unhealthy($"{dbContext.Schema} schema is not reachable.", exception);
        }
    }
}
