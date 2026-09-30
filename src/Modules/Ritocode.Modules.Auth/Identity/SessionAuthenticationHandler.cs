using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Auth.Session;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Auth.Identity;

/// <summary>
/// Authenticates a request by its session cookie (ADR 0012): the token's session, if it is active,
/// names the user and carries the CSRF token that <see cref="CsrfProtectionMiddleware"/> checks.
/// </summary>
/// <remarks>
/// A missing, unknown, revoked or expired token authenticates nothing (<see cref="AuthenticateResult.NoResult"/>,
/// not a failure), so the request goes on as anonymous and a protected endpoint answers 401.
/// </remarks>
internal sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ISessionIssuer sessions)
    : ProblemAuthenticationHandler(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionCookies.SessionName, out var token) || string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await sessions.FindActiveAsync(token, Context.RequestAborted);

        if (session is null)
        {
            return AuthenticateResult.NoResult();
        }

        Claim[] claims =
        [
            new(RitocodeClaimTypes.UserId, session.UserId.ToString()),
            new(RitocodeClaimTypes.CsrfToken, session.CsrfToken),
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
