# 0019 — Feedback becomes a GitHub issue, opened by the API

- Status: Accepted
- Date: 2026-11-14
- Relates to: [#243](https://github.com/shoraLBRT/ritocode/issues/243); the real check waits on
  [#134](https://github.com/shoraLBRT/ritocode/issues/134) and [#135](https://github.com/shoraLBRT/ritocode/issues/135)
- Builds on: [ADR 0003](0003-api-conventions.md), [ADR 0011](0011-release-images.md); [SPEC.md](../SPEC.md) §9.3, §9.6

## Context

Visitors need a way to send feedback without an account, and each message should land where the
backlog lives: a GitHub issue in this repository with the `feedback` label. The maintainer's
Transliterator does this through a small Cloudflare function that checks Turnstile and a per-address
limit and opens the issue. Something has to hold a GitHub token, and the form is open to anyone, so
it needs protection against abuse.

## Decision

**The Ritocode API opens the issue:** `POST /api/v1/feedback`, anonymous, `{ message, page, captchaToken }`
→ `201 { number }`. It is a host-level endpoint beside `/meta/modules` (`src/Ritocode.Api/Feedback/`):
it belongs to no module and keeps nothing in the database.

- **The tracker is behind `IFeedbackIssues`.** `GitHubFeedbackIssues` calls
  `POST https://api.github.com/repos/{Feedback:Repository}/issues` with `Feedback:GitHubToken`, a
  fine-grained token with *Issues: write* on this repository only, from the server's secrets
  (`FEEDBACK_GITHUB_TOKEN` in `production.env`). With no token or repository, the endpoint answers
  `503 feedback_not_configured`, not a crash. The local stack sets `Feedback:Sink: Log`
  (`appsettings.Development.json`), which writes the issue to the log; tests replace the interface
  with a fake.
- **The issue** has the message's first line as its title (`Feedback: …`, cut at 70 characters) and
  the message in a code fence longer than any run of backticks in it, so nothing a visitor writes is
  rendered: no `@mention` notifies anyone, no image is fetched, no link is made. Below it, the page
  path (only a local path of at most 300 characters, else `unknown`) and the user id, or `anonymous`.
- **Abuse protection**, checked in this order:
  1. a length cap — empty or whitespace `400 feedback_empty`, over `Feedback:MaxLength` (4000)
     `400 feedback_too_long`;
  2. a per-address limit — `Feedback:MaxPerWindow` (5) in any sliding `Feedback:Window` (1 hour),
     `429 feedback_rate_limited`. It is kept in memory: a restart forgets it, which is acceptable
     for one API instance (ADR 0011); it stops a script, the captcha does the rest. Behind the proxy
     the address is the forwarded one (`Api:BehindProxy`);
  3. a captcha behind `ICaptchaVerifier` — Cloudflare Turnstile's `siteverify` with
     `Feedback:TurnstileSecretKey`, `403 feedback_not_verified` on failure. An unreachable Turnstile
     fails closed. With no secret, no captcha is asked for (the local stack). The page renders the
     challenge only when built with `VITE_TURNSTILE_SITE_KEY`; the secret and the site key are set
     together, or every message is refused.
- GitHub unreachable is `503 feedback_unreachable`; GitHub refusing the request (a bad token, a
  missing label permission) is `500 feedback_failed`, logged with GitHub's status.

## Consequences

- No second deployable (a worker) and no second place for secrets; the token lives where the OAuth
  secrets already do.
- **Unverified, and part of the real check left for #134/#135:** whether the Russian VPS reaches
  `api.github.com` and `challenges.cloudflare.com`, and whether Turnstile's challenge loads for
  visitors in Russia. If GitHub is not reachable from the VPS, the sink can move to a worker outside
  Russia behind the same `IFeedbackIssues` without touching the endpoint or the page; if Turnstile is
  not reachable, another verifier (Yandex SmartCaptcha) goes behind `ICaptchaVerifier`.
- The site's CSP (`deploy/Caddyfile`) allows Turnstile's script and frame. Building the web image
  with `VITE_TURNSTILE_SITE_KEY` needs the release workflow to pass it as a build argument, which is
  a change to `.github/workflows/` left to the maintainer; until then the captcha stays off in
  production and the limit and the length cap protect the form alone.
