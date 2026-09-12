using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Auth.Identity;

/// <summary>
/// Authenticates every request as the seeded development identity, when configuration enables it.
/// </summary>
/// <remarks>
/// <para>
/// This is the reduction ADR 0005 allows — a seeded identity instead of a login — implemented as a
/// real authentication scheme rather than as a middleware that sets a user id somewhere. That is
/// what makes it substitutable: stage two replaces this handler with one that reads a session
/// token, and no endpoint, no authorisation policy and no <see cref="ICurrentUser"/> consumer
/// changes.
/// </para>
/// <para>
/// Disabled, it returns <see cref="AuthenticateResult.NoResult"/> rather than a failure: nothing was
/// presented, so nothing was rejected, and the request reaches the authorisation layer as anonymous.
/// </para>
/// </remarks>
internal sealed class DevelopmentIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<DevelopmentIdentityOptions> developmentIdentity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = developmentIdentity.Value;

        if (!identity.Enabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(RitocodeClaimTypes.UserId, identity.UserId.ToString()),
            new(ClaimTypes.Name, identity.Username),
            new(ClaimTypes.Email, identity.Email),
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    /// <summary>
    /// Answers an unauthenticated request with the ADR 0003 error body instead of an empty 401.
    /// </summary>
    /// <remarks>
    /// No <c>WWW-Authenticate</c> header: there is no credential a client could be told to present
    /// yet, and offering one browsers understand would put a native credential prompt in front of a
    /// single-page application. The frontend already branches on this body through
    /// <c>ApiError.isUnauthenticated</c>.
    /// </remarks>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(AppError.Unauthenticated());

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(AppError.Forbidden("forbidden", "You may not perform this action."));

    private Task WriteProblemAsync(AppError error) =>
        // A handler can be invoked after the response has begun — an endpoint that streamed and then
        // failed. There is nothing useful to write at that point and writing throws, so the status
        // the client already received stands.
        Response.HasStarted
            ? Task.CompletedTask
            : ApiProblem.WriteAsync(Context, error, Context.RequestAborted);
}
