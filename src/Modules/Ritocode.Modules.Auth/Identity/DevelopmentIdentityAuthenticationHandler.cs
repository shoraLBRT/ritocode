using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Auth.Identity;

/// <summary>
/// Authenticates every request as the seeded development identity, when configuration enables it.
/// </summary>
/// <remarks>
/// <para>
/// This is the seeded identity of ADR 0008 — a fixed user instead of a login — implemented as a
/// real authentication scheme rather than as a middleware that sets a user id somewhere. That is
/// what made it substitutable: a request with a session cookie is authenticated by
/// <see cref="SessionAuthenticationHandler"/> instead, and no endpoint, no authorisation policy and
/// no <see cref="ICurrentUser"/> consumer tells the two apart.
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
    : ProblemAuthenticationHandler(options, loggerFactory, encoder)
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
}
