# 0012 — Sessions: an opaque token in a cookie, the session in a table

- Status: Accepted
- Date: 2026-10-01
- Relates to: [#6](https://github.com/shoraLBRT/ritocode/issues/6), [#7](https://github.com/shoraLBRT/ritocode/issues/7), [#35](https://github.com/shoraLBRT/ritocode/issues/35), [#130](https://github.com/shoraLBRT/ritocode/issues/130)
- Builds on: [0008](0008-authentication-seam.md), [SPEC.md](../SPEC.md) §6.1

## Context

[ADR 0008](0008-authentication-seam.md) built the seam and left the token format to the maintainer:
"JWT, or opaque plus a server-side store". [SPEC.md](../SPEC.md) §6.1 has since fixed the carrier: a
secure, HTTP-only cookie, with state-changing requests protected against cross-site request forgery.
Three answers were weighed for what the cookie holds:

- **An opaque token, the session in a table.** A session can be ended from the server: signing out
  revokes it, and the admin area and security baseline (#130, #35) can end a user's sessions. It
  costs one table and one indexed lookup per authenticated request.
- **ASP.NET's encrypted authentication ticket.** No table, but a session cannot be ended before it
  expires; signing out only deletes the cookie on that one browser.
- **A JWT in the cookie.** The same as the ticket, with a format built for a cross-service problem
  this monolith does not have.

## Decision

**An opaque token in the cookie, the session in `auth.sessions`**, decided by the maintainer on
2026-10-01.

- **The token** is 32 random bytes, base64url, in `__Host-ritocode-session`: `Secure`, `HttpOnly`,
  `SameSite=Lax`, `Path=/`. The `__Host-` prefix means no subdomain can set or shadow it.
- **The row** keeps the SHA-256 of the token, never the token itself, so a read of the table signs
  nobody in. It also keeps the user, the times it was created and expires (30 days,
  `Auth:Session:Lifetime`), when it was revoked, and its CSRF token.
- **Authentication.** A request carrying the cookie is authenticated by the `Session` scheme. Any
  other request goes to the development identity of ADR 0008, which is off outside development. An
  unknown, revoked or expired token authenticates nothing.
- **CSRF: a synchronizer token per session.**
  - The session's CSRF token travels in a second cookie, `__Host-ritocode-csrf`, which the page can
    read (`SameSite=Strict`, not `HttpOnly`).
  - Every state-changing request authenticated by the session cookie must repeat it in `X-CSRF-Token`,
    or it is refused with `403 csrf_token_invalid`.
  - Another site can make a browser send the cookie, but cannot read the token.
  - Requests without an ambient credential carry no token and need none: anonymous ones, and those
    under the development identity.
- **Signing out** is `POST /auth/logout`, outside the versioned API as sign-in is (SPEC §9.3). It
  revokes the session and clears both cookies.
- **Signing in** belongs to #7. A provider callback calls `ISessionIssuer.StartAsync` and writes the
  cookies with `SessionCookies.Write`.

## Consequences

- Every authenticated request reads one row by a unique index. At this product's scale that costs
  nothing worth caching.
- Revocation is immediate, and ending all of a user's sessions is one update by `user_id`, which is
  indexed.
- Revoked and expired rows accumulate. A periodic delete of rows expired for some time can come with
  the runbook (#41). Nothing reads them.
- The frontend repeats the CSRF token from its cookie on every non-GET request, in `ApiClient`.
