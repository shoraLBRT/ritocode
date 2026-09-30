using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Identity;

/// <summary>
/// Refuses a state-changing request authenticated by the session cookie unless it repeats the
/// session's CSRF token in <see cref="HeaderName"/> (docs/SPEC.md §6.1, ADR 0012). Another site can
/// make a browser send the cookie; it cannot read the token to put it in a header.
/// </summary>
/// <remarks>
/// It applies only to a principal carrying <see cref="RitocodeClaimTypes.CsrfToken"/>: a request with
/// no ambient credential — anonymous, or the development identity — has nothing to forge.
/// </remarks>
public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF-Token";

    public const string InvalidCode = "csrf_token_invalid";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expected = context.User.FindFirst(RitocodeClaimTypes.CsrfToken)?.Value;

        if (expected is not null && !IsSafe(context.Request.Method) && !Matches(context.Request.Headers[HeaderName].ToString(), expected))
        {
            await ApiProblem.WriteAsync(
                context,
                AppError.Forbidden(InvalidCode, $"A state-changing request must repeat the session's CSRF token in {HeaderName}."),
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private static bool Matches(string presented, string expected) =>
        presented.Length > 0
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));
}
