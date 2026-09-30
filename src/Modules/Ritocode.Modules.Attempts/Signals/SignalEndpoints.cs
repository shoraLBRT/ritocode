using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Modules.Attempts.Lifecycle;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Validation;

namespace Ritocode.Modules.Attempts.Signals;

/// <summary>
/// <c>POST /api/v1/signals</c> (docs/SPEC.md §9.3), for the owner of the attempt. Closed to an anonymous
/// caller by the host's fallback policy; another user's attempt is a 404, as a missing one is.
/// </summary>
internal static class SignalEndpoints
{
    public static IEndpointRouteBuilder MapSignalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/signals", SendAsync)
            .WithName("SendSignal")
            .WithTags("Attempts")
            .WithValidation<SendSignalRequest>();

        return endpoints;
    }

    /// <summary>201 with the signal. It has no address of its own: its sender never reads it back alone.</summary>
    private static async Task<IResult> SendAsync(
        SendSignalRequest request,
        ISignalSender sender,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // An id is an opaque string to a client (ADR 0003): a malformed one is an attempt this caller does not have.
        if (!Guid.TryParse(request.Attempt, out var attemptId))
        {
            return ApiProblem.ToResult(AttemptLifecycle.AttemptNotFound(), context);
        }

        var result = await sender.SendAsync(userId, attemptId, request.Card!, request.Comment, cancellationToken);

        return result.Match(signal => Results.Created((string?)null, signal), error => ApiProblem.ToResult(error, context));
    }
}
