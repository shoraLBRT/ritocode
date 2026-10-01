using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Auth.Session;

/// <summary>The caller, as the frontend shows it: who is signed in.</summary>
/// <param name="Admin">Whether the caller may open the admin area (SPEC §6.2), so the header can offer it.</param>
public sealed record MeView(Guid Id, string Username, bool Admin);

/// <summary>
/// <c>GET /api/v1/me</c> (docs/SPEC.md §9.3): the signed-in caller, or 401 — which is how the
/// frontend learns it is signed out.
/// </summary>
/// <remarks>
/// It reads the caller from <see cref="ICurrentUser"/>, so it answers for whatever identity the host
/// authenticates: the seeded development identity now, the session cookie of
/// <see href="https://github.com/shoraLBRT/ritocode/issues/6">#6</see> later, with no change here.
/// </remarks>
internal static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me", GetMeAsync)
            .WithName("GetMe")
            .WithTags("Auth");

        return endpoints;
    }

    private static async Task<IResult> GetMeAsync(
        ICurrentUser currentUser,
        IUserLookup users,
        IAuthorizationService authorization,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(currentUser.RequireId(), cancellationToken);

        // An identity that names no user is not a signed-in caller, whatever authenticated it.
        if (user is null)
        {
            return ApiProblem.ToResult(AppError.Unauthenticated(message: "The authenticated identity does not name a user."), context);
        }

        // Asked of the admin policy, whose handler the Users module owns, rather than of a list here.
        var admin = await authorization.AuthorizeAsync(context.User, AdminPolicy.Name);

        return Results.Ok(new MeView(user.Id, user.Username, admin.Succeeded));
    }
}
