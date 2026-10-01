# Project State

**The living state of the project: what exists, what is next, and how to verify a change.** Update it
in the same pull request as the work it describes — a session that skips this makes the next one
start from nothing.

- **Last updated:** 2026-10-01
- **Current stage:** S7 · Production — its engineering can be built against a local stack (#135's
  Compose file, #136's release command, #34's monitor, #41); the real checks wait on the VPS and domain of #134.
  S6 closed with #133: `/` and `/problems` read without JavaScript (#132), and every listed event
  reached a local Umami, which set no cookie. S5 closed with #35:
  its exit criterion was shown in #130 (progress by class and card, a signal sent from a review
  appearing in the admin area and resolved there, the admin lists of users and attempts), and the
  security baseline has a test for each of its items. S4's exit criterion is shown
  against a stand-in provider (#127); the round trip against real GitHub and Google waits on the
  OAuth apps of #134, and the privacy page (#128) — built over a placeholder — on its text. The
  content track of S2 ([#124](https://github.com/shoraLBRT/ritocode/issues/124),
  [#42](https://github.com/shoraLBRT/ritocode/issues/42)) stays open and is the maintainer's — see
  [ROADMAP.md](ROADMAP.md)
- **Board:** <https://github.com/users/shoraLBRT/projects/3> — issues are the source of truth for
  what is done
- **What the product is:** [CONCEPT.md](CONCEPT.md); what gets built: [SPEC.md](SPEC.md)

---

## Session workflow

The maintainer's general `session` command carries the loop; it lives outside this repository and
reads what is specific to Ritocode from here and from [AGENTS.md](../AGENTS.md). In short:

1. `git fetch origin main`, then read `CLAUDE.md`, `AGENTS.md`, this file and `ROADMAP.md` from
   `origin/main` — in a worktree `main` is checked out elsewhere.
2. Take the first open issue of the lowest open stage in `ROADMAP.md` whose dependencies are met.
   `type:content` issues and the maintainer's own [#134](https://github.com/shoraLBRT/ritocode/issues/134)
   are not taken unless the maintainer asks.
3. Before building, check [SPEC.md](SPEC.md) §13 and [Open questions](#open-questions). A decision
   they do not make goes to the maintainer, not into code.
4. Branch off `origin/main`, one issue per branch. Build it with its tests; run everything under
   [Verification](#verification).
5. Open a PR naming the issue — `Closes #N` only when it is finished — comment on the issue, and
   update this file in the same PR: **What exists**, **Next up**, **Last updated**, the baseline,
   the smoke checks if endpoints changed. When a stage's exit criterion has been shown to work, move
   **Current stage** on.

The maintainer merges, unless they have started the session with a command that allows merging.

---

## What exists

The repository holds the platform the new product keeps and the first module built for it,
Content.

| Part | State | Where |
| --- | --- | --- |
| Modular monolith | Solution, module boundaries enforced by tests, options validated at startup, request id, unified error body, health endpoints, `/api/v1/meta/modules` | `src/`, `tests/Ritocode.Architecture.Tests` |
| API conventions | [ADR 0003](adr/0003-api-conventions.md): RFC 9457 errors, pagination envelope, validation filter. A request that cannot be read — a body that is not UTF-8 JSON, a query value of the wrong type, a body over the cap or without a JSON content type — is `400 request_invalid`, `413 request_too_large` or `415 unsupported_media_type` in every environment, not a 500. What routing refuses before any endpoint runs gets the same body (`RoutingRefusals`): a wrong method is `405 method_not_allowed` with `Allow`, a body in a non-JSON content type `415 unsupported_media_type`, an unknown address under `/api/v1` `404 not_found` | `src/Ritocode.Shared` |
| Persistence | PostgreSQL, EF Core per module, one schema each, migrations applied by `Ritocode.DbMigrator`, drift check in CI ([ADR 0004](adr/0004-persistence-and-migrations.md)) | `src/Ritocode.DbMigrator`, `scripts/` |
| Test harness | One PostgreSQL container per test assembly, one migrated database per test class | `tests/Ritocode.TestSupport` |
| Identity seam | `ICurrentUser`, real authentication schemes, authenticated by default ([ADR 0008](adr/0008-authentication-seam.md), Accepted). `GET /api/v1/me` answers the caller (id, username) or 401 | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth` |
| Sessions ([#6](https://github.com/shoraLBRT/ritocode/issues/6)) | [ADR 0012](adr/0012-sessions.md): an opaque token in `__Host-ritocode-session` (Secure, HttpOnly, SameSite=Lax), the session in `auth.sessions` by the token's SHA-256 — the token is never stored — with its expiry (30 days, `Auth:Session:Lifetime`), revocation and CSRF token. A request with the cookie is the `Session` scheme's, any other the development identity's (off outside development). Every state-changing request under a session repeats its CSRF token — from the readable `__Host-ritocode-csrf` cookie — in `X-CSRF-Token`, or gets `403 csrf_token_invalid` (`CsrfProtectionMiddleware`); `ApiClient` sends it. `POST /auth/logout` (outside `/api/v1`, through the new `IModule.MapHostEndpoints`) revokes the session and clears both cookies. `ISessionIssuer.StartAsync` + `SessionCookies.Write` are what #7's sign-in calls. Sign-in (#7) issues one; the header has no sign-in or sign-out button yet | `src/Modules/Ritocode.Modules.Auth/Session`, `src/Ritocode.Shared/Identity/CsrfProtectionMiddleware.cs` |
| Sign-in ([#7](https://github.com/shoraLBRT/ritocode/issues/7)) | `GET /auth/login/{github,google}?returnUrl=` sends the browser to the provider through ASP.NET's OAuth handler — state with its correlation cookie, PKCE (S256) — and the provider returns to `/auth/callback/{provider}`. There `AccountLinker` resolves the user: an identity already linked signs in as its user (the stored login follows a rename); a new one with a **verified** address joins the user with that address or a new user made for it (`IUserAccounts`, answered by Users; the username from the GitHub login or the address's local part, made unique); a new one without a verified address reaches nobody. GitHub's address is the **primary** one of `/user/emails`; Google's is userinfo's `email` with `email_verified`. Then a session starts (`ISessionIssuer`, `SessionCookies.Write`) and the browser returns to `returnUrl`, accepted only as a local path (`400` otherwise); a refusal or a provider failure returns there with `?signInError=email_unverified`, `provider_already_linked` or `provider_failed`. A provider is offered only when `Auth:GitHub` / `Auth:Google` (`ClientId`, `ClientSecret`) is configured. `Auth:SignIn:AppOrigin` prefixes the return path where the pages are served from another origin (Vite's `http://localhost:5173` in development; empty in production). Tested against a fake provider through the real handler. **Not yet**: a real round trip against GitHub and Google, which needs the OAuth apps of #134 | `src/Modules/Ritocode.Modules.Auth/SignIn` |
| Signed-out solving ([#127](https://github.com/shoraLBRT/ritocode/issues/127)) | SPEC §4.6 in the pages. The task screen keeps the answer — picks, leaves, step — in the tab's `sessionStorage` as it changes (`pages/task/draft.ts`), restored on a reload and removed once checked; a kept answer is restored only if every card is one the task offers and every leaf one of the tree. *Check* signed out opens a prompt with GitHub and Google, each returning to `/tasks/{slug}?check=1`; back from the provider, the answer is restored, submitted on the learner's attempt and the review opens. A `check` with no answer that fits opens the task at its start with a note; a sign-in that failed restores the answer and checks nothing. `AppLayout` reads `check` and `signInError` once, on load, and takes them out of the address (`session/signInReturn.ts`); a `signInError` shows above the page in words, until dismissed. The header has *Войти* (a menu of both providers returning to the current page) and, signed in, *Выйти* (`POST /auth/logout`, then `/me` again); a page closed to a visitor offers the providers too. `ApiClient` now sends `credentials: 'include'` — the session cookie never reached the API from Vite's origin before — and the default API address is `http://localhost:5199/api/v1`, the pages' host, so the page can read the CSRF cookie. Return paths are checked on the client by the server's rule (`isLocalPath`). Checked in Chromium at 1280 and 375 px against a stand-in API with a provider that signs in at once: signed-out answer, GitHub, review of that answer; sign-out; the header menu; a `signInError` | `frontend/src/pages/task`, `frontend/src/session`, `frontend/src/components/AppLayout.tsx` |
| Privacy page ([#128](https://github.com/shoraLBRT/ritocode/issues/128)) | SPEC §6.1 and §10.4. `/privacy` renders `frontend/src/site/privacy.ru.md` — Markdown built into the bundle (`?raw`) and prerendered with `/` and `/problems` (`privacy.html`, in the sitemap). Today the file is a **clearly marked placeholder** saying what the site receives at sign-in and keeps; the maintainer's text replaces it (#134), and nothing else changes when it does. `SignInLinks` — the header menu, the task's *Check* prompt, a closed page — now carries the notice under the providers: what signing in hands over, linking `/privacy`. The footer links it on every page. `Markdown` learned `##` and `###` headings for it. The header's sign-in menu is as wide as its content (at most 24 rem), and spans the session's row at phone width; following the privacy link closes it. Checked at 1280 and 375 px: no horizontal scroll, the menu fits. **Not yet**: the maintainer's text | `frontend/src/pages/PrivacyPage.tsx`, `frontend/src/site/privacy.ru.md`, `frontend/src/session/SignInLinks.tsx` |
| Users | The `users` table, `IUserLookup`, and `IUserAccounts` (find by address, create) for sign-in. `xp` and `trust_level` removed in #119 | `src/Modules/Ritocode.Modules.Users` |
| Ownership rule | An architecture test reading compiled IL: a user's rows are reached only where the owner is in the query. It guards the Attempts module's context; the allowances are `OwnedAttempts` (every lookup by owner) and the creation in `AttemptLifecycle.StartAsync`. Its reader is proved against a test-only context | `tests/Ritocode.Architecture.Tests/OwnershipRuleTests.cs` |
| Content ([#120](https://github.com/shoraLBRT/ritocode/issues/120), [#121](https://github.com/shoraLBRT/ritocode/issues/121)) | The format of [CONTENT_FORMAT.md](CONTENT_FORMAT.md) parsed and validated — every rule of §7 tested — and `content validate` in CI (job *Validate content*). The `content` schema — taxonomy, cards, materials, tasks — and an ingest that validates first, writes in one transaction stamped with the commit, upserts by slug, retires cards and unpublishes tasks that left `content/`, and derives the material overview and the easy-task shortlist. A development host seeds `content/` on start. The public reads of SPEC §9.3 ([#9](https://github.com/shoraLBRT/ritocode/issues/9)): `GET /problems` (every live card in full, with the classes), `GET /treatments`, `GET /tasks` (a page, easy first) and `GET /tasks/{slug}` (context, brief, material with its overview, the cards to pick from — name, summary and keywords only, the shortlist for an easy task — and the other tasks over the same material). No answer key and no card weight leave the server; a test serialises a task and looks for them | `src/Modules/Ritocode.Modules.Content`, `src/Ritocode.ContentTool`, `content/` |
| Authoring ([#122](https://github.com/shoraLBRT/ritocode/issues/122), [#123](https://github.com/shoraLBRT/ritocode/issues/123)) | The `author-card` skill: drafts a card from a name, reading the live catalogue so the summary is delimited from its neighbours; checks it with `content validate`; never overwrites a card. The catalogue of [#124](https://github.com/shoraLBRT/ritocode/issues/124) was written with it, a class per pull request, to the plan in [the issue's comment](https://github.com/shoraLBRT/ritocode/issues/124#issuecomment-5935789258): **56 cards**, nine or ten per class, every agent-specific problem of SPEC §3.2 among them. They are drafts the maintainer edits. A keyword YAML reads as a pair (`type: ignore`) must be quoted, or `content validate` fails with an unhelpful message about `System.String`. The `author-task` skill: writes a material and one task per context from the maintainer's idea, validates, and runs the **blind smoke test** — `content learner-view <task>` renders the task as the task screen receives it (no key, notes, lesson, weight or card sections; a test holds it to that), and a separate `claude -p` session with no tools, run from an empty directory outside the repository, answers it from that alone; every difference from the key is reported. One easy task made with it, `flower-shop-daily-revenue` over `flower-shop-revenue`, whose smoke answer matched the key | `.claude/skills/author-task`, `src/Modules/Ritocode.Modules.Content/Authoring`, `content/materials`, `content/tasks` |
| Attempts ([#20](https://github.com/shoraLBRT/ritocode/issues/20), [#125](https://github.com/shoraLBRT/ritocode/issues/125)) | **Scoring**: `DiagnosisScoring.Score`, a pure function of the answer, the key with the card weights, and the parameters of SPEC §5.2 (`Attempts:Scoring`, validated on start); the total floored at zero, the maximum, whether the answer is correct, and a line per card — found with its treatment, missed or extra — with the author's leaves for every card of the key. **Attempts**: the `attempts` schema; `POST /attempts` (a published task; 201), `PATCH /attempts/{id}` (the step reached, forward only), `POST /attempts/{id}/submit` (validated against the cards the task offers and the leaves of the tree, scored, stored with the content revision; the first submitted attempt at a task counts, later ones are practice, a partial unique index settles a race), `GET /attempts/{id}`, `GET /attempts?task=` (a page, newest first). A submitted attempt is never changed; a test re-ingests changed content and compares the stored result byte for byte. Submitting is capped per user (`Attempts:RateLimit`, 10 in 10 minutes, `429 attempt_rate_limited`). The key and weights come from Content through `ITaskForAttemptLookup`; `GET /tasks` carries `solved` for a signed-in caller through `ISubmittedTaskLookup`, which Attempts answers | `src/Modules/Ritocode.Modules.Attempts`, `src/Ritocode.Shared/Contracts` |
| Progress ([#24](https://github.com/shoraLBRT/ritocode/issues/24), [#30](https://github.com/shoraLBRT/ritocode/issues/30)) | `GET /api/v1/me/progress`: from the caller's **first** attempts only, computed on read by a pure `ProgressCalculator` — per class (every class, in taxonomy order): findings met, found, found and treated right; per card: met, found, missed, picked when absent, treated right; and how many tasks it is built from. Classes and cards carry their names (#30): the class of each card and both names come from Content through `ICardClassLookup` (retired cards included; a card Content does not know is named by its slug, with no class). **The page** `/progress`, signed in, in the header for a signed-in learner: the six classes with their counts, then a table of the cards grouped by class, each linking to `/problems#<slug>`; the table scrolls in its own region on a phone, the card column staying put. A learner with no first attempt is pointed to `/tasks`. Checked at 1280 and 375 px, with and without attempts A test submits first and practice attempts and another user's, and shows practice and others change nothing | `src/Modules/Ritocode.Modules.Attempts/Progress`, `frontend/src/pages/ProgressPage.tsx` |
| Signals ([#129](https://github.com/shoraLBRT/ritocode/issues/129)) | The `attempts.signals` table and `POST /api/v1/signals` (`{ attempt, card, comment? }`, 201): sent only from an **extra pick** of the caller's own **submitted** attempt — another user's attempt is a 404 like a missing one, an open attempt is `409 attempt_not_submitted` whatever the card (so it cannot probe the key), a card that is not an extra pick is `400` on `card`, a second signal for the same pick is `409 signal_already_sent` (a unique index settles a race). The comment is trimmed, blank is none, at most 500 characters. Capped per user (`Attempts:SignalRateLimit`, 10 in 10 minutes, `429 signal_rate_limited`). The attempt is read untracked and never written; `GET /attempts/{id}` lists `signalledCards`. Reached through `OwnedSignals`, the ownership rule's new allowance. **In the review**, each extra pick has *Я уверен, что это здесь*: a one-line comment, send or cancel, then "sent, the score does not change"; a pick signalled before shows as sent. Checked in a browser at 1280 and 375 px | `src/Modules/Ritocode.Modules.Attempts/Signals`, `frontend/src/pages/task/SignalControl.tsx` |
| Admin area ([#130](https://github.com/shoraLBRT/ritocode/issues/130)) | SPEC §6.2. Admins are named by address in `Users:Admin:Emails` (the development identity in `appsettings.Development.json`). **The policy**: `AdminPolicy` in Shared, its requirement met by the Users module's `AdminAuthorizationHandler` (the caller's stored address against the list); `GET /me` carries `admin` from the same policy. A signed-in non-admin gets the **same `404 not_found`** as any address under `/api/v1` that serves nothing — the API now has a fallback giving such an address the unified body (`NoSuchAddress`), and `AdminRefusalAsNotFound` returns that very result; signed out, 401. **Endpoints**: `GET /admin/signals?status=open\|resolved` and `POST /admin/signals/{id}/resolve` (idempotent; `404 signal_not_found`) and `GET /admin/attempts?status=all\|open\|submitted&user=` in Attempts — every user's rows through `EveryonesRows`, the ownership rule's new allowance; `GET /admin/users` in Users — newest first, with providers from Auth (`ILinkedProviderLookup`) and attempts and tasks solved from Attempts (`IAttemptTallyLookup`); learners' addresses reach Attempts through `IUserContactLookup`. All paged; a bad filter is `400` naming every bad parameter. **Pages** `/admin/signals`, `/admin/users`, `/admin/attempts` under `RequireAdmin` (anyone else sees the not-found page); filters and page in the address; a user's attempt count links to their attempts; an open attempt shows the step it stopped at. *Админка* in the header for an admin. Checked in a browser at 1280 and 375 px: a signal sent from a review listed and resolved, an attempt abandoned at step 2 found in the open attempts | `src/Modules/Ritocode.Modules.Attempts/Admin`, `src/Modules/Ritocode.Modules.Users/Admin`, `src/Ritocode.Shared/Identity/AdminPolicy.cs`, `frontend/src/pages/admin` |
| Security baseline ([#35](https://github.com/shoraLBRT/ritocode/issues/35)) | SPEC §10.1, one test per item: the ownership rule guards attempts and signals (`AttemptsAndSignals_AreBothGuarded`); the submit and signal caps (`RateLimitTests`, `SignalRateLimitTests`); **every** state-changing endpoint the host maps, read from its routing, refuses a session request without the CSRF token (`EveryStateChangingEndpoint_RefusesASessionRequestWithoutTheCsrfToken`); the return address only as a local path (`SignInTests`); every `/api/v1/admin` endpoint carries the admin policy and a non-admin gets the unknown-address 404 (`EveryAdminAddress_IsBehindTheAdminPolicy`, `NonAdminEndpointTests`); answers bounded to offered cards, existing leaves, at most 100 picks and 50 leaves, a leaf at most 129 characters, a comment at most 500. New here: `SecurityHeadersMiddleware` — `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` unless an endpoint sets its own — on every response, a 500 included; and a request body cap, `Api:MaxRequestBodyBytes` (64 KiB), on Kestrel | `src/Ritocode.Shared/Http/SecurityHeadersMiddleware.cs`, `src/Ritocode.Api/Setup/ApiSetupExtensions.cs`, `tests/Ritocode.Api.Tests/Endpoints/SecurityBaselineTests.cs` |
| Analytics ([#133](https://github.com/shoraLBRT/ritocode/issues/133)) | SPEC §8. Umami's script is added by `main.tsx` only when the build names both `VITE_UMAMI_SCRIPT_URL` and `VITE_UMAMI_WEBSITE_ID` (the web image takes them as build arguments, from the repository variables `UMAMI_SCRIPT_URL` and `UMAMI_WEBSITE_ID`); otherwise `track` is a no-op and no script reaches the page. Page views, route changes included, are Umami's own. Events (`src/analytics`): `task-opened` (task, difficulty), `step-2-reached`, `check-signed-out`, `attempt-submitted` (task), `signal-sent` (card), `catalogue-search` (once a visit, nothing typed), `sign-in-completed` (provider — the provider link notes it in the tab's `sessionStorage`, the page that comes back signed in counts it). No user id, address or text in any event. Events sent before the script loads wait for it. Checked against a local Umami (its Docker image on the compose network, the site registered by a row in its database): all seven events and the page views arrived with only those values, and no response set a cookie | `frontend/src/analytics`, `deploy/web.Dockerfile` |
| Production stack ([#135](https://github.com/shoraLBRT/ritocode/issues/135)) | `deploy/compose.production.yml`: Caddy at the edge (`deploy/Caddyfile` — Let's Encrypt for `SITE_DOMAIN` and `UMAMI_DOMAIN`, HTTP → HTTPS, HSTS and the pages' content policy, `/api/*` `/auth/*` `/health/*` to the API, the rest to the web image), the API and web images at `RITOCODE_VERSION`, PostgreSQL 17 on a volume, Umami with its own role and database (`deploy/postgres-init`), a one-off `migrate` service; every service on the `json-file` log driver with a cap. Secrets in `production.env` on the server (`deploy/production.env.example`; git-ignored). The API learned two settings for it: `Api:BehindProxy` (trust the edge's `X-Forwarded-Proto`/`-For`, so the sign-in callback is `https://`) and `Api:DataProtectionKeysDirectory` (the key ring on the `api-keys` volume). Smoke-run locally with `SITE_DOMAIN=localhost` (Caddy's own CA). **Not yet**: the real checks — the site on its domain with a valid certificate, sign-in with GitHub and Google there, Umami receiving production events — which need the VPS, domain and OAuth apps of #134 | `deploy/`, `src/Ritocode.Api/Setup/ApiSetupExtensions.cs` |
| Release and backups ([#136](https://github.com/shoraLBRT/ritocode/issues/136)) | `RITOCODE_SERVER=… ./scripts/release.sh [commit]` from a clean checkout: sends that commit's `deploy/` to the server and runs `deploy/release.sh` — pull, migrate, **ingest** the `content/` shipped in the API image stamped with the commit (the migrator's new `ingest <root> <revision>` command), start, wait for health; rollback is releasing the previous commit. `deploy/backup.sh` (daily, cron): both databases dumped, seven days kept, each copied with `rclone` to `BACKUP_REMOTE` — an S3-compatible bucket at a Russian provider in another region (the decision of #136). `deploy/restore.sh`: into an empty database, then the API restarted. Images from `RITOCODE_REGISTRY` (GHCR by default). Release and restore performed locally against a local registry: release, dump, every volume removed, restore, the site served from the restored database. **Not yet**: the same on the server of #134, and the bucket | `deploy/`, `scripts/release.sh`, `src/Ritocode.DbMigrator/ContentIngestCommand.cs` |
| Monitoring ([#34](https://github.com/shoraLBRT/ritocode/issues/34)) | [ADR 0013](adr/0013-uptime-monitoring.md), **Proposed**: the *Monitor* workflow runs every five minutes from GitHub against the addresses in the repository variable `MONITOR_ORIGINS` (empty: it does nothing). `scripts/monitor-check.sh`: the site's `/health/ready` answers `Healthy`, every address's certificate is valid and more than 14 days from expiry, a failed request retried twice. `scripts/monitor-alert.sh`: one open issue labelled `monitoring-alert` mentioning the owner, a new comment only when what fails changes, closed on recovery; the run succeeds when it has reported, so GitHub sends no e-mail per failed run. `scripts/monitor-test.sh` tests the alert against a fake `gh` (CI job *Test the alert*, on pull requests touching it). Checked locally: a running API passes, the stopped API fails; certificates of `github.com` pass and `expired.badssl.com` fails. **Not yet**: the site on its domain, the variable set, and the API stopped there producing an alert issue — which needs the VPS and domain of #134 | `.github/workflows/monitor.yml`, `scripts/monitor-*.sh`, [RUNBOOK.md](RUNBOOK.md#monitoring) |
| Runbook ([#41](https://github.com/shoraLBRT/ritocode/issues/41)) | `docs/RUNBOOK.md`: the checklist before each release, first setup of a server, release, roll back, restore a backup, rotate each secret, read logs, add an admin, publish new content — commands for the scripts of #135 and #136. Written before a server exists: the first real release will show what it misses | `docs/RUNBOOK.md` |
| Landing ([#131](https://github.com/shoraLBRT/ritocode/issues/131)) | `/`: what Ritocode is and who it is for, from [CONCEPT.md](CONCEPT.md) — the lead, how a task goes in three steps, proportion, the defect in the brief, the catalogue — and the ways in: a **demo task**, `/tasks`, `/problems`. The demo is an easy task named at build time by `VITE_DEMO_TASK` (default `flower-shop-daily-revenue`), read in `main.tsx` and handed down as `SiteConfig`, as the API address is. The page asks the API for nothing, so #132 can prerender it; the shell's old proof-of-life call to `/meta/modules` is gone from it. Checked at 1280 and 375 px; the demo opens signed out | `frontend/src/pages/HomePage.tsx`, `frontend/src/site` |
| Prerendered public pages ([#132](https://github.com/shoraLBRT/ritocode/issues/132)) | `content export <file> [path]` writes the problem catalogue exactly as `GET /problems` serves it — one mapping, `ProblemCatalogueMapping`, now behind both, and a test holds the export to the API's body. `npm run build:static` (`CONTENT_EXPORT`, `SITE_ORIGIN`) renders `/` and `/problems` with React's server renderer into `dist/index.html` and `dist/problems.html` — every card in full with its anchor — each with its title, description and canonical address, plus `spa.html` (the shell for every other route), `sitemap.xml` and `robots.txt`. Titles and descriptions come from one table (`site/pageMeta.ts`) that the application also applies on every route change. The application renders over the static markup once loaded. CI job *Prerender public pages* runs the export and the build and checks every card is on the page. Checked in a browser: the raw HTML holds every card and anchor, and the application takes over and scrolls to `#money-in-float` | `src/Ritocode.ContentTool`, `frontend/src/prerender`, `frontend/scripts/prerender.mjs` |
| Frontend shell ([#26](https://github.com/shoraLBRT/ritocode/issues/26)) | React, Vite and TypeScript; the API client that owns the error envelope; layout, routes, loading, error and empty states. A **translation catalogue** of its own (`src/i18n`: typed dotted keys, `{name}` placeholders, Russian plurals through `Intl.PluralRules`, `<html lang="ru">`), every string moved into it, and an ESLint rule that fails on text written in JSX. The **signed-in state** from `/me` (`src/session`: loading, signed in, signed out on a 401, error) in the header, and `RequireSignIn`, a layout route for pages that need a learner. A **phone-width layout**, checked at 375 px: no horizontal scroll, the header wraps | `frontend/` |
| Catalogue pages ([#27](https://github.com/shoraLBRT/ritocode/issues/27)) | `/tasks`: title, difficulty, the time from the difficulty (SPEC §3.4), and for a signed-in learner a solved mark; filters by difficulty and — signed in — by solved; no class tags; the API's order. Read in one request (`pageSize=100`) and filtered in the page. `/problems`: one page, cards grouped by class in the taxonomy's order and shown in full, their sections rendered by `Markdown` — the project's own renderer for the subset cards use, React elements only, no `innerHTML`; search over name, summary and keywords, ignoring case and ё; an anchor per card, and `/problems#<slug>` scrolls to it once the cards arrive. Both checked at 1280 and 375 px | `frontend/src/pages`, `frontend/src/components/Markdown.tsx` |
| Task screen ([#126](https://github.com/shoraLBRT/ritocode/issues/126)) | `/tasks/{slug}`: context and brief (rendered as Markdown), the material — overview, file tree with line counts, the selected file with line numbers and Python highlighting from the project's own tokenizer (`components/python.ts`) — and the answer, side by side on a desktop and three tabs below 64 rem. Step 1: the offered cards by name and summary, grouped by class (`GET /tasks/{slug}` now carries the class names, so the screen never asks for `/problems`), with search; step 2: the whole tree per picked card, branches as disclosure widgets, leaves as checkboxes; back to step 1 keeps the picks; *Check* needs a leaf on every picked card and a signed-in learner. A signed-in learner works in the newest open attempt at the task, or a new one; reaching step 2 is recorded on it. Checking lands on the review. Solved end to end at 1280 and 375 px | `frontend/src/pages/task` |
| Review ([#29](https://github.com/shoraLBRT/ritocode/issues/29)) | `/tasks/{slug}/attempts/{id}`, owner only: the score in points with its composition (found of present, extra picks, matched treatments); every finding of the key, found or missed, with the learner's leaves beside the author's, the author's note and the full card on expanding; every extra pick with its card; the lesson; the other tasks over the same material; *Try again*. A clean task says its outcome in words. Found, missed and extra each carry a mark and a word. The notes and the lesson are kept with the attempt on submit (`attempts.review`), so the review matches the key it was scored against | `frontend/src/pages/task/ReviewPage.tsx` |
| End-to-end test ([#39](https://github.com/shoraLBRT/ritocode/issues/39)) | `e2e/`, Playwright with Chromium: a visitor opens the demo task signed out, picks a card and a leaf, presses *Check*, signs in with GitHub, lands on the review of that answer (`Найдено 1 из 2 …`) and finds it in `/progress`, card by card. Playwright starts three servers: `fake-provider.mjs`, a stand-in for GitHub's OAuth app that sends the browser straight back with a code, checks the PKCE verifier, and makes a new person for every sign-in; `start-api.mjs`, which creates and migrates a database of its own (`ritocode_e2e`) in the compose PostgreSQL, then runs the API in Development with the development identity off and GitHub pointed at the fake through the new `Auth:GitHub:AuthorizationEndpoint`, `TokenEndpoint` and `UserInformationEndpoint` (empty — always in production — means the provider's own); and the production build of the pages under `vite preview`. Texts come from the pages' catalogue. CI job *Learning flow* (`e2e.yml`) on every pull request and push to `main`. Shown to fail at the right step when the provider reports an unverified address | `e2e/`, `.github/workflows/e2e.yml`, `src/Modules/Ritocode.Modules.Auth/SignIn` |
| CI | Backend build, test, formatting, migrations and drift; content validation; frontend lint, build and test; the prerendered pages; the monitor's alert test; the end-to-end learning flow. The *Monitor* workflow is not CI: it checks the production site on a schedule | `.github/workflows/` |
| Release images ([#31](https://github.com/shoraLBRT/ritocode/issues/31)) | On every push to `main`, `ghcr.io/shoralbrt/ritocode-api` (the API, with the migrator at `migrator/Ritocode.DbMigrator.dll`) and `ghcr.io/shoralbrt/ritocode-web` (the static build of #132, served by Caddy on port 80 with its `try_files` rule), tagged with the full commit and `main`, public — [ADR 0011](adr/0011-release-images.md), **Proposed**. Built without pushing on every pull request. The web image takes the site's address from the repository variable `SITE_ORIGIN`, a placeholder until #134 | `deploy/`, `.github/workflows/release-images.yml` |
| Logs ([#33](https://github.com/shoraLBRT/ritocode/issues/33)) | The API logs one JSON object per line (the console's `json` formatter, UTC, scopes included) outside Development. Every line of a request carries its request id — the `X-Request-Id` — and, signed in, the user id (`UserLogScopeMiddleware`, after authentication), nothing else about the person. One summary line per request from ASP.NET's HTTP logging: method, path, status, duration — no headers, query or body — before authorisation, so a refused request is logged too. SQL is quiet in production (`Warning`), verbose in development. Levels from `Logging:LogLevel`. How to find a request's lines, and the rotation #135 must configure: [deploy/README.md](../deploy/README.md). Tests capture the host's log lines (`CapturedLogs`) and find a request's by its id | `src/Ritocode.Shared`, `src/Ritocode.Api/appsettings.json`, `deploy/README.md` |

Removed in #119: the Workspaces, Evaluations, Submissions and Progress modules, the sandbox runner,
`spikes/`. Removed in #121: the Problems module, the old package format and its C# packages, object
storage with MinIO, and the frontend's old problem pages. All of it remains readable at the tag
`pre-diagnosis`.

## Next up

From [ROADMAP.md](ROADMAP.md), in order:

1. S5 and S6 are done. In S7 the production stack (#135), the release with backups
   ([#136](https://github.com/shoraLBRT/ritocode/issues/136)) and monitoring
   ([#34](https://github.com/shoraLBRT/ritocode/issues/34)) exist, each open until its real check
   runs on the VPS and domain of #134; the runbook (#41) says how to run them. The end-to-end test of
   S8 ([#39](https://github.com/shoraLBRT/ritocode/issues/39)) runs in CI. What is left for a coding
   session waits on #134 or on content; the trial of S8 (#137) waits on the catalogue and tasks. Left in
   S4: the real round trip against GitHub and Google (the OAuth apps of #134), and the policy's text
   in the privacy page ([#128](https://github.com/shoraLBRT/ritocode/issues/128)), which with its
   sign-in notice exists over a placeholder. Maintainer-provided
   resources (OAuth apps, VPS, domain, policy text) block only an issue's final real check, not its
   engineering: build with fakes, a local `docker compose` or a placeholder, and name the real check
   as left.
2. S4 · Accounts: sessions (#6), sign-in (#7), signed-out solving (#127) and the privacy page with
   its sign-in notice (#128) exist. Left: the policy's text, which replaces the placeholder in
   `frontend/src/site/privacy.ru.md` (#134).
3. The content track, with the two skills: 55–60 cards
   ([#124](https://github.com/shoraLBRT/ritocode/issues/124)) and the 20 tasks
   ([#42](https://github.com/shoraLBRT/ritocode/issues/42)). On 2026-10-01 the maintainer handed
   the catalogue to a `session-full` run, which drafted all 56 cards and merged them on green CI;
   the maintainer edits them from here. The same run then took the tasks of #42: 5 of 20 exist —
   the demo task, `team-reminders-for-myself` and `team-reminders-for-support` (easy, over
   `team-reminders`), `clinic-booking-city-network` and `clinic-booking-dentist` (medium, over
   `clinic-booking`). Each new one passed a blind smoke test, reported in its pull request.

The maintainer's own [#134](https://github.com/shoraLBRT/ritocode/issues/134) — domain, VPS, OAuth
apps, privacy text — runs in parallel and gates S7.

## Open questions

Decisions the specification left open are listed in [SPEC.md](SPEC.md) §13. Add here anything a
future session would otherwise have to rediscover.

- **Umami's server and site id** (#133, for #135): Umami runs beside the site (SPEC §9.5); its script
  address and the site's id become the repository variables `UMAMI_SCRIPT_URL` and `UMAMI_WEBSITE_ID`,
  which the web image is built with — empty, the site has no analytics. The pages' content policy
  (#35's note below) has to allow that script and its `/api/send`. A local Umami for trying events:
  `ghcr.io/umami-software/umami:postgresql-latest` on the `ritocode_default` network with
  `DATABASE_URL` pointing at a database of its own in the compose PostgreSQL; register the site with a
  row in its `website` table, and set the two `VITE_UMAMI_*` values in `frontend/.env.local`.
- **What the proxy adds, not the API** (#35, for #135): HSTS, and the content policy of the static
  pages — the API's `default-src 'none'` is right for JSON and would break the pages. The API's body
  cap is Kestrel's; if the proxy buffers bodies, give it a cap no smaller than `Api:MaxRequestBodyBytes`.
- **An unknown address under `/api/v1` answers `404 not_found` with the unified body** (#130),
  where routing alone gave an empty 404: the admin area's refusal has to look exactly like it. It was
  first a catch-all fallback endpoint, which outranked routing's 405 and 415 — `GET /api/v1/signals`
  was a 404. It is now answered by status-code pages when no endpoint matched (`RoutingRefusals`), so
  a known path with the wrong method is `405 method_not_allowed` again. Do not bring back a
  `MapFallback` under the API: the tests for the 405 and the 415 under `/api/v1` fail with it.
- **Admins are read from configuration on every admin request** (#130): the handler loads the
  caller's address and compares it with `Users:Admin:Emails`, ignoring case. Adding or removing an
  admin is a configuration change and a restart; production sets the list through the environment
  (`Users__Admin__Emails__0`), which #135 must do for the maintainer's address.
- **The admin lists read every user's rows with no index for their order** (#130):
  `GET /admin/attempts` sorts all attempts by `started_at`, `GET /admin/signals` all signals by
  `created_at`. At the MVP's few users that is nothing; add an index on `attempts (started_at)` if the
  list ever slows.

- **A new identity without a verified address cannot sign in** (#7). SPEC §6.1 says such an address
  never links; it is also never used to create a user, so every user has a verified address — which
  naming admins by e-mail (§6.2) relies on. A GitHub account whose primary address is unverified is
  refused even if a secondary one is verified. The browser returns with
  `?signInError=email_unverified`, and the page asks the learner to verify their primary address
  (#127).
- **The kept answer is keyed by task, not by content revision** (#127). The issue asks for task and
  revision, but `GET /tasks/{slug}` carries no revision, and the stamp is the whole content commit, so
  keying by it would drop an answer whenever any file under `content/` changed. Instead an answer is
  restored only if it still fits the task as served — every card offered, every leaf in the tree —
  which is what the submit validates. If an answer must also be dropped when a task's key changes,
  the task read needs a per-task revision.
- **The answer lives in `sessionStorage`**, not `localStorage` (#127): the provider returns in the
  same tab, and nothing outlives the tab. A learner who signs in from a second tab, or whose browser
  hands the return to another window, gets the task at its start with the note.
- **Both providers are always offered.** The pages cannot ask which are configured; an unconfigured
  one answers `/auth/login/{provider}` with the JSON `404 provider_not_found`. Production configures
  both (#134). If a deployment ever runs with one, the pages need a list of providers from the API.
- **The development API address is `localhost`, not `127.0.0.1`** (#127): cookies belong to a host,
  not a port, so the page on `localhost:5173` reads the CSRF cookie only of an API on `localhost`. A
  local `.env` from before #127 with `127.0.0.1` needs the same change.
- **One account per provider per user** (the unique index on `linked_accounts`): a second GitHub
  account whose verified address is an existing user's is refused (`provider_already_linked`)
  rather than replacing the first link.
- **Data-protection keys protect the sign-in state** between `/auth/login` and the callback. In
  production they live on the `api-keys` volume (`Api:DataProtectionKeysDirectory`, #135); elsewhere
  the host keeps the default key ring, so a restart during a sign-in fails that sign-in.
- **`Api:BehindProxy` trusts every forwarder** (#135): the proxy's address on the Compose network is
  not fixed, so the known-proxy list is cleared. That is safe only while the API publishes no port of
  its own — as in `compose.production.yml`. Never turn it on for an API reachable directly.
- **Registering the OAuth apps** (#134): the callback addresses are `/auth/callback/github` and
  `/auth/callback/google` on the API's origin (`http://localhost:<api port>` in development); the
  secrets go in `Auth:GitHub:ClientId` / `ClientSecret` and `Auth:Google:…`, through user secrets or
  the environment, never the repository.

- **The end-to-end test reads the demo task** (#39): it picks `secrets-in-repo` with
  `auto.secrets` in `flower-shop-daily-revenue` and expects one of two findings found. Changing that
  task's key, or those cards' names, means changing `e2e/learning-flow.spec.ts` with it. A card's
  checkbox is named by its name and then its summary, and summaries name their neighbours
  (`hardcoded-config` names «Секреты в репозитории»), so the test matches the name from its start. Only GitHub
  is exercised; Google's round trip is the same handler with another profile, covered by `SignInTests`.
- **Playwright starts its web servers before its global setup**, so the database is prepared by
  `e2e/start-api.mjs`, and Playwright waits for the demo task rather than `/health/ready`: content is
  seeded after the host reports ready.
- **The privacy policy lives in the frontend, not in `content/`** (#128): `frontend/src/site/privacy.ru.md`.
  The issue says "with the site's content"; `content/` is the training content, under CC BY-SA and
  read by the content tool's format, and the policy is neither. The site's own text is the frontend's
  — the translation catalogue holds the rest of it — and a file there needs no API and is
  prerendered. Its name carries the locale, so an English policy is `privacy.en.md` beside it.

- **One signal per extra pick** (#129, confirmed by the maintainer on 2026-10-01): a second one for
  the same card of the same attempt is refused, so the author's list counts learners, not clicks. A practice attempt can signal like a first one:
  it is the same key.
- **Two `appsettings.json` race into `Ritocode.Api.Tests`' output** (found in #33): the API's, and
  the migrator's through `Ritocode.TestSupport`. Which one the test host reads depends on build
  order — the API's locally, the migrator's in CI — so a test must not rely on a production setting
  from that file; set what it needs in the fixture, or read `src/Ritocode.Api/appsettings.json`.
- **Registry reachability** (ADR 0011, accepted 2026-10-01): images are on GHCR because a pull needs no account and
  GitHub is reachable from Russian hosting. #135 is where a pull from the VPS is first tried; if it
  is slow or fails, add a mirror in a Russian registry (needs the maintainer's account).
- **Serving the static build** (#132, for #135): try the path, then the path with `.html`, then
  `spa.html` — Caddy `try_files {path} {path}.html /spa.html`. `index.html` is the rendered landing,
  so it must not be the fallback for other routes. `SITE_ORIGIN` is the production address; CI uses
  a placeholder until the domain exists (#134).
- **The application renders over the prerendered markup** rather than hydrating it: `/problems`
  fetches its catalogue, so the first client render would not match. A visitor with JavaScript
  sees the catalogue give way to the loading state for as long as `GET /problems` takes. Embedding
  the export in the page as initial data would remove that, if it is ever noticed.
- Tasks are ordered by difficulty, then title. SPEC §4.3 says "then publication", which needs a
  first-published timestamp the schema does not keep yet; add it if the order starts to matter.
- **The review's notes and lesson are kept with the attempt on submit** (#29), as the key is, so a
  later edit to the task never changes an old review. The full cards it expands are the catalogue's
  current ones: a card is general, not part of any key. Attempts submitted before #29 have no
  kept notes, and their review shows none.
- **Every `POST /attempts` starts a new attempt**, so the task screen resumes the newest open attempt
  at the task (`GET /attempts?task=&pageSize=1`) before starting one. Under React's StrictMode in
  development the lookup runs twice and can start two; production renders once.
- **An unpublished task can still be submitted** by someone who started it before it left
  `content/`, and cannot be started. "Solved" on the catalogue means at least one submitted attempt.
- **Ingest in production is the release's** (#136): the API image carries `content/`, and the
  release runs `migrate ingest content <commit>`. A development host still seeds `content/` on start,
  stamped `development`.
- **An empty `ADMIN_EMAIL` names nobody** (found in #136's smoke run): Compose passes the variable
  even when empty, and a blank entry in `Users:Admin:Emails` used to stop the API at startup.
- **Summaries name their neighbours by name** (#124): «Проверка и действие врозь» is named in two
  other summaries, «Ослабленный тест» in two. Renaming a card means searching `content/problems` for
  its old name in «…» and changing those summaries with it; nothing checks it.
- **Three cards of the full catalogue can be read in the demo task's material**: `hardcoded-config`
  (the host in `DATABASE_URL`), `missing-timeout` (`psycopg.connect` with no timeout) and
  `naive-datetime` (`created_at::date` takes the database's time zone); the first two are on its
  shortlist. Neither is in its key; whether either belongs there, with `accept.fits-context` or otherwise,
  is the maintainer's call. The smoke test against the full catalogue will show whether a learner
  picks them.
- **Smoke tests go stale as the catalogue grows.** A task is smoke-tested against the cards that
  exist when it is written; an easy task's shortlist and any other task's full list change as cards
  are added, and a new card may name a problem an old material already has. Re-run the smoke test
  over existing tasks once #124 has filled the catalogue. `flower-shop-daily-revenue` was re-run
  against the full catalogue on 2026-10-02 (#42's first batch): the answer found both findings and
  also picked `hardcoded-config` (`manual.extract-config`) and `missing-timeout`
  (`accept.fits-context`); `naive-datetime` is not on its shortlist and was named outside the list.
  Its key is unchanged — the maintainer's call, and the e2e test counts its two findings.
- **A card that is fine here is still in the key** (SPEC §3.3, the `author-task` skill, the smoke
  prompt): a present problem is listed with an `accept.*` leaf. `CONTENT_FORMAT.md` §6's example
  says the opposite — a hardcoded SMTP host acceptable in its context is "left out of the key" — and
  should be brought in line.
- A module's test context should be configured as the host configures one. The host's contexts
  retry transient failures, and a retrying strategy refuses a transaction opened by hand — the smoke
  run of #121 caught exactly that, which a test context without retries had passed.

---

## Verification

Run from the repository root. All of these must be clean before opening a PR.

**A Docker daemon has to be running.** Tests that need PostgreSQL start their own container through
Testcontainers. Without Docker, `dotnet test` fails in a way that looks like a code problem.

```bash
dotnet build Ritocode.slnx --warnaserror
```

```bash
dotnet test Ritocode.slnx
```

```bash
dotnet format Ritocode.slnx --verify-no-changes
```

Content, against the committed tree (errors fail, warnings print):

```bash
dotnet run --project src/Ritocode.ContentTool -- validate content
```

Frontend, from `frontend/`:

```bash
npm ci && npm run lint && npm run build && npx vitest run
```

The prerendered pages, from the repository root (CI job *Prerender public pages*):

```bash
dotnet run --project src/Ritocode.ContentTool -- export content-export.json content
```

```bash
cd frontend && CONTENT_EXPORT=../content-export.json SITE_ORIGIN=https://ritocode.example npm run build:static
```

The end-to-end test, from `e2e/`, with the compose PostgreSQL up (`./scripts/dev-up.sh`) and
nothing on ports 5173, 5199 or 5299 — stop a running dev API and Vite first. Once:
`npm ci && npx playwright install chromium`. Then:

```bash
cd e2e && npm test
```

The drift check runs against the compose stack. `db-verify-no-drift.sh` and `dotnet ef` read
`Database__ConnectionString` from the environment; `dev-up` prints it.

```bash
./scripts/dev-up.sh
```

```bash
./scripts/db-verify-no-drift.sh
```

**On Windows, port 55432 can fall inside a range Windows reserves**
(`netsh int ipv4 show excludedportrange protocol=tcp`), and PostgreSQL then fails to start with
"ports are not available". Set another `POSTGRES_PORT` in your local `.env` — it is not committed —
and re-run `dev-up`; the compose container is shared, so every worktree's `.env` needs the same
port.

**Recreate an old development database** (`docker compose down -v`, then `dev-up`): a volume from
before #119 or #121 still holds the schemas of the removed modules, and no migration drops them. The
compose stack is PostgreSQL only.

### Baseline

| Suite | Tests |
| --- | --- |
| `Ritocode.Architecture.Tests` | 14 |
| `Ritocode.Shared.Tests` | 58 |
| `Ritocode.Api.Tests` | 138 |
| `Ritocode.Modules.Content.Tests` | 71 |
| `Ritocode.Modules.Attempts.Tests` | 19 |
| Frontend (vitest) | 193 |
| `scripts/monitor-test.sh` | 9 |
| End-to-end (`e2e/`) | 1 |

The count is a ratchet: if it drops, the PR says which tests went and why.

### Smoke checks on a running host

```bash
dotnet run --project src/Ritocode.Api --no-launch-profile --urls http://127.0.0.1:5199
```

In Development (`ASPNETCORE_ENVIRONMENT=Development`) the host seeds `content/` on start and logs
`Seeded content from … N cards, N materials, N tasks`; content with errors is logged and not written.

| Request | Expected |
| --- | --- |
| `GET /health/live` | `200`, `{"status":"Healthy","checks":[]}` |
| `GET /health/ready` | `200`, one check per module schema |
| `GET /api/v1/meta/modules` | `200`, four modules: Auth, Users, Content, Attempts |
| `GET /api/v1/me` | `200`, `{"id":"0199aa00-…","username":"developer","admin":true}` under the development identity (an admin in Development); `401 unauthenticated` without it |
| `POST /auth/logout` | `204`, two `Set-Cookie` headers clearing `__Host-ritocode-session` and `__Host-ritocode-csrf` |
| `GET /auth/login/github?returnUrl=/tasks` | `302` to GitHub with `state` and `code_challenge` when `Auth:GitHub` is configured; `404 provider_not_found` when it is not; `400` with `errors.returnUrl` for `returnUrl=https://…` |
| `GET /api/v1/problems` | `200`, `{ classes, cards }` — the six classes once content is seeded |
| `GET /api/v1/treatments` | `200`, five branches, leaves as `branch.leaf` |
| `GET /api/v1/tasks?pageSize=1000` | `400`, `code: "validation_failed"`, `errors.pageSize` present |
| `GET /api/v1/tasks/no-such-task` | `404`, `code: "task_not_found"` |
| `POST /api/v1/attempts` with `{"task":"<a seeded task>"}` | `201`, `Location` set, `step: "diagnosis"`, `result: null` |
| `POST /api/v1/attempts/{id}/submit` with `{"picks":[]}` | `200`, `result` with `total`, `maximum` and a line per card |
| `GET /api/v1/tasks` after a submit | `200`, that task has `solved: true` |
| `POST /api/v1/signals` with `{"attempt":"<a submitted attempt>","card":"<one of its extra picks>"}` | `201`, the signal; again → `409`, `code: "signal_already_sent"`; a found card → `400`, `errors.card` |
| `POST /api/v1/signals` with a body that is not JSON (or a comment in cp1251 bytes) | `400`, `code: "request_invalid"`, logged at Information, not as an unhandled exception |
| `POST /api/v1/signals` with `Content-Type: text/plain` | `415`, `code: "unsupported_media_type"` |
| `GET /api/v1/signals` | `405`, `code: "method_not_allowed"`, `Allow: POST` |
| `GET /api/v1/me/progress` | `200`, `{ tasks, classes, cards }` — six classes, cards only once met or picked, each class and card with its `name` |
| `GET /api/v1/admin/signals` | `200`, a page of open signals, each with `cardName` and `learner.email`; `?status=resolved` the resolved ones; `?status=x` → `400`, `errors.status` |
| `POST /api/v1/admin/signals/{id}/resolve` | `200`, the signal with `resolvedAt`; again → the same `resolvedAt`; an unknown id → `404 signal_not_found` |
| `GET /api/v1/admin/users` | `200`, a page of users, newest first, each with `providers`, `attempts`, `tasksSolved` |
| `GET /api/v1/admin/attempts?status=open` | `200`, a page of attempts never submitted, each with the `step` it stopped at |
| any admin address, for a non-admin | `404`, `code: "not_found"`, as `GET /api/v1/no-such-thing` |
| any response | carries an `X-Request-Id` header, and `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Referrer-Policy: no-referrer`, `Cache-Control` with `no-store` |
