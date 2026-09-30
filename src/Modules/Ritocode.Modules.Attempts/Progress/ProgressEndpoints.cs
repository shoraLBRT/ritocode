using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Attempts.Progress;

/// <summary>
/// <c>GET /api/v1/me/progress</c> (docs/SPEC.md §9.3): the caller's progress. Closed to an anonymous
/// caller by the host's fallback policy.
/// </summary>
internal static class ProgressEndpoints
{
    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me/progress", async (IProgressReader progress, ICurrentUser currentUser, CancellationToken cancellationToken) =>
                Results.Ok(await progress.GetAsync(currentUser.RequireId(), cancellationToken)))
            .WithName("GetProgress")
            .WithTags("Attempts");

        return endpoints;
    }
}
