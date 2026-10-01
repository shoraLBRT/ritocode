using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// What an address under the API that serves nothing answers: a 404 in the unified error body
/// (ADR 0003), code <c>not_found</c>.
/// </summary>
/// <remarks>
/// Routing alone answers an unknown address with an empty 404. The fallback below gives it the
/// unified body, and the admin area refuses a signed-in non-admin with exactly this error, so the two
/// cannot be told apart (docs/SPEC.md §6.2). The fallback says nothing about authorisation, so the
/// host's fallback policy puts a 401 in front of it for an anonymous caller — as the admin policy does.
/// </remarks>
public static class NoSuchAddress
{
    public const string Code = "not_found";

    public static AppError Error() => AppError.NotFound(Code, "Nothing is served at this address.");

    /// <summary>Answers every address under <paramref name="api"/> that no endpoint serves.</summary>
    public static IEndpointConventionBuilder MapNoSuchAddressFallback(this IEndpointRouteBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        return api.MapFallback((HttpContext context) => ApiProblem.ToResult(Error(), context));
    }
}
