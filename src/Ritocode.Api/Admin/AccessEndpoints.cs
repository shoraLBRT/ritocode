using Ritocode.Shared.Identity;

namespace Ritocode.Api.Admin;

/// <summary>
/// <c>GET /api/v1/admin/access</c>: whether the caller's session is an admin's, for the edge to ask before it
/// lets a request through to a tool that has no sign-in of its own — the log viewer at <c>/admin/logs/</c>
/// (ADR 0020 decision 2, #295), through Caddy's <c>forward_auth</c>. An admin gets 204; a signed-in non-admin the
/// 404 of an unknown address (<see cref="AdminPolicy"/>); a signed-out caller the 401 of every API address.
/// The edge passes any refusal to the visitor as it is.
/// </summary>
public static class AccessEndpoints
{
    public static IEndpointRouteBuilder MapAccessEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/admin/access", () => Results.NoContent())
            .WithName("AdminCheckAccess")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        return endpoints;
    }
}
