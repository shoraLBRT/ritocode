using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Ritocode.Shared.Identity;

/// <summary>
/// Reads the current user off the request's <see cref="ClaimsPrincipal"/>, which the
/// authentication middleware has already established.
/// </summary>
/// <remarks>
/// Host infrastructure rather than a module's logic — it answers "who is calling", which no module
/// owns — so it lives beside <c>IObjectStore</c>'s implementation in <c>Ritocode.Shared</c> for the
/// same reason. What authenticates the request is a module's business: the scheme itself belongs to
/// the Auth module, and ADR 0007 §5 keeps cross-module <em>contracts</em> — which this is not —
/// implemented by their owning module.
/// </remarks>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Id
    {
        get
        {
            var principal = accessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = principal.FindFirstValue(RitocodeClaimTypes.UserId);

            // A principal that is authenticated but carries no usable identifier is anonymous as
            // far as this platform is concerned. Returning null rather than throwing keeps a
            // malformed token a 401 at the endpoint's authorisation check instead of a 500 at the
            // first line of code that wanted a user.
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }
    }
}
