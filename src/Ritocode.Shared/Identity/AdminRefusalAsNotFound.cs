using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Identity;

/// <summary>
/// Answers a signed-in caller refused by <see cref="AdminPolicy"/> with the 404 of an address that
/// serves nothing, instead of a 403 that would confirm the admin area exists (docs/SPEC.md §6.2,
/// ADR 0003). Every other outcome is the framework's.
/// </summary>
/// <remarks>
/// Authorisation runs before the endpoint binds its parameters, so a non-admin learns nothing from a
/// malformed query string either: the refusal comes first.
/// </remarks>
internal sealed class AdminRefusalAsNotFound : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        var refusedAsNonAdmin = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<AdminRequirement>().Any() == true;

        // The very result the fallback for an unknown address returns, so the bodies match field for field.
        return refusedAsNonAdmin
            ? ApiProblem.ToResult(NoSuchAddress.Error(), context).ExecuteAsync(context)
            : _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
