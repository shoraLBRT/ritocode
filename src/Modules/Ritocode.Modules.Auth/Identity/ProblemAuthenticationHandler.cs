using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Modules.Auth.Identity;

/// <summary>
/// A handler that answers a refused request with the ADR 0003 error body instead of an empty 401 or
/// 403, whichever scheme authenticated it.
/// </summary>
/// <remarks>
/// No <c>WWW-Authenticate</c> header: the credential is a cookie a browser gets by signing in, and a
/// scheme browsers understand would put a native credential prompt in front of a single-page
/// application. The frontend branches on this body through <c>ApiError.isUnauthenticated</c>.
/// </remarks>
internal abstract class ProblemAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(AppError.Unauthenticated());

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(AppError.Forbidden("forbidden", "You may not perform this action."));

    private Task WriteProblemAsync(AppError error) =>
        // A handler can be invoked after the response has begun, by an endpoint that streamed and then
        // failed. There is nothing useful to write at that point and writing throws, so the status
        // the client already received stands.
        Response.HasStarted
            ? Task.CompletedTask
            : ApiProblem.WriteAsync(Context, error, Context.RequestAborted);
}
