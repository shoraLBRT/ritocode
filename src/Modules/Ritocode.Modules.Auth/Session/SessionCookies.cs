using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Auth.Session;

/// <summary>A session as the browser receives it: the token, the CSRF token, and when both end.</summary>
public sealed record IssuedSession(string Token, string CsrfToken, DateTimeOffset ExpiresAt);

/// <summary>
/// The two cookies of a session (docs/SPEC.md §6.1, ADR 0012). Both carry the <c>__Host-</c> prefix,
/// which a browser accepts only with <c>Secure</c>, <c>Path=/</c> and no <c>Domain</c> — so no
/// subdomain can set or shadow them.
/// </summary>
public static class SessionCookies
{
    /// <summary>The session token: HTTP-only, so no script reads it; <c>SameSite=Lax</c>, so a link from another site still arrives signed in.</summary>
    public const string SessionName = "__Host-ritocode-session";

    /// <summary>
    /// The session's CSRF token, readable by the page so the frontend can repeat it in
    /// <see cref="CsrfProtectionMiddleware.HeaderName"/>; another site cannot read it.
    /// </summary>
    public const string CsrfName = "__Host-ritocode-csrf";

    public static void Write(HttpResponse response, IssuedSession session)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(session);

        response.Cookies.Append(SessionName, session.Token, Options(httpOnly: true, SameSiteMode.Lax, session.ExpiresAt));
        response.Cookies.Append(CsrfName, session.CsrfToken, Options(httpOnly: false, SameSiteMode.Strict, session.ExpiresAt));
    }

    public static void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(SessionName, Options(httpOnly: true, SameSiteMode.Lax, expires: null));
        response.Cookies.Delete(CsrfName, Options(httpOnly: false, SameSiteMode.Strict, expires: null));
    }

    private static CookieOptions Options(bool httpOnly, SameSiteMode sameSite, DateTimeOffset? expires) => new()
    {
        HttpOnly = httpOnly,
        Secure = true,
        SameSite = sameSite,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}
