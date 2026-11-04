using Ritocode.Shared.Errors;

namespace Ritocode.Shared.Http;

/// <summary>
/// What an address under the API that serves nothing answers: a 404 in the unified error body
/// (ADR 0003), code <c>not_found</c>.
/// </summary>
/// <remarks>
/// Routing alone answers an unknown address with an empty 404; <see cref="RoutingRefusals"/> gives it
/// this body. The admin area refuses a signed-in non-admin with exactly this error, so the two cannot
/// be told apart (docs/SPEC.md §6.2). An unknown address has no endpoint and so says nothing about
/// authorisation; the host's fallback policy puts a 401 in front of it for an anonymous caller — as the
/// admin policy does.
/// </remarks>
public static class NoSuchAddress
{
    public const string Code = "not_found";

    public static AppError Error() => AppError.NotFound(Code, "Nothing is served at this address.");
}
