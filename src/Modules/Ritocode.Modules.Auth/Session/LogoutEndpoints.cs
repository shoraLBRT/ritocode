using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ritocode.Modules.Auth.Session;

/// <summary>
/// <c>POST /auth/logout</c>, outside the versioned API as sign-in is (docs/SPEC.md §9.3): ends the
/// caller's session and clears its cookies. Open to anyone — signing out while signed out is a no-op —
/// and, being state-changing, it needs the CSRF token when a session is present.
/// </summary>
internal static class LogoutEndpoints
{
    public static IEndpointRouteBuilder MapLogoutEndpoints(this IEndpointRouteBuilder root)
    {
        root.MapPost("/auth/logout", LogoutAsync)
            .WithName("Logout")
            .WithTags("Auth")
            .AllowAnonymous();

        return root;
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, ISessionIssuer sessions, CancellationToken cancellationToken)
    {
        if (context.Request.Cookies.TryGetValue(SessionCookies.SessionName, out var token) && !string.IsNullOrEmpty(token))
        {
            await sessions.EndAsync(token, cancellationToken);
        }

        SessionCookies.Clear(context.Response);
        return Results.NoContent();
    }
}
