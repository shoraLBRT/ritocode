# Project State

**The living state of the project: what exists, what is next, and how to verify a change.** Update it
in the same pull request as the work it describes — a session that skips this makes the next one
start from nothing.

- **Last updated:** 2026-11-22
- **Current stage:** S7 · Production — its engineering can be built against a local stack (#135's
  Compose file, #136's release command, #34's monitor, #41); the real checks wait on the VPS and domain of #134.
  S6 closed with #133: `/` and `/problems` read without JavaScript (#132), and every listed event
  reached a local Umami, which set no cookie. S5 closed with #35:
  its exit criterion was shown in #130 (progress by class and card, a signal sent from a review
  appearing in the admin area and resolved there, the admin lists of users and attempts), and the
  security baseline has a test for each of its items. S4's exit criterion is shown
  against a stand-in provider (#127); the round trip against real GitHub and Google waits on the
  OAuth apps of #134, and the privacy page (#128) — built over a placeholder — on its text. The
  content track of S2 is drafted in full: 56 cards ([#124](https://github.com/shoraLBRT/ritocode/issues/124))
  and 20 tasks ([#42](https://github.com/shoraLBRT/ritocode/issues/42)), each new task through a
  blind smoke test — drafts the maintainer edits; S2's exit criterion was shown by them. S8's
  trial (#137) now waits only on S7
- **Board:** <https://github.com/users/shoraLBRT/projects/3> — issues are the source of truth for
  what is done
- **What the product is:** [CONCEPT.md](CONCEPT.md); what gets built: [SPEC.md](SPEC.md)

---

## Session workflow

The [habze](https://github.com/shoraLBRT/habze) skills carry the loop — `session` (one issue, no
merge), `session-reserve` and `session-full` (issue after issue, merging each PR on green CI). They
live outside this repository and read what is specific to Ritocode from here and from
[AGENTS.md](../AGENTS.md). In short:

1. `git fetch origin main`, then read `CLAUDE.md`, `AGENTS.md` and this file from `origin/main` —
   in a worktree `main` is checked out elsewhere — and the open milestones on GitHub.
2. Take the first open issue of the lowest open milestone that is not blocked, in the order its
   description gives ([AGENTS.md](../AGENTS.md) › Where the work comes from). `type:content` issues and the maintainer's own [#134](https://github.com/shoraLBRT/ritocode/issues/134)
   are not taken unless the maintainer asks.
3. Before building, check [SPEC.md](SPEC.md) §13 and [Open questions](#open-questions). A decision
   they do not make goes to the maintainer, not into code.
4. Branch off `origin/main`, one issue per branch. Build it with its tests; run everything under
   [Verification](#verification).
5. Open a PR naming the issue — `Closes #N` only when it is finished — comment on the issue, and
   update this file in the same PR: **What exists**, **Where things stand**, **Last updated**, the
   baseline, the smoke checks if endpoints changed. When a stage's exit criterion has been shown to
   work, close its milestone and move **Current stage** on. Work found along the way becomes an
   issue, never a list in a document.

The maintainer merges, unless the session was started with `session-reserve` or `session-full`;
even then a PR touching `.github/workflows/`, `.claude/`, `CLAUDE.md` or `AGENTS.md` waits for the
maintainer ([AGENTS.md](../AGENTS.md)).

### habze adoption

Ritocode is where the way of working was born, and [habze](https://github.com/shoraLBRT/habze) now
holds it for every project. Adoption is [shoraLBRT/habze#13](https://github.com/shoraLBRT/habze/issues/13),
blocked by habze's own `STANDARD.md`, skills and `adopt-standard`. Measured against habze's SPEC §2–3
on 2026-10-01:

| Requirement | Ritocode | Left |
| --- | --- | --- |
| Documents: README, CLAUDE, AGENTS, product description, ROADMAP, PROJECT_STATE | All but ROADMAP: the maintainer moved the backlog wholly into GitHub on 2026-11-04 — milestone descriptions hold the goals, exit criteria and order | `CLAUDE.md` names no standard version — none exists yet; habze's SPEC still lists a ROADMAP |
| Rules of §3–4 in `AGENTS.md`: eligibility, trust, stop and ask, protected paths, branch names, merging | Written | — |
| Stages as milestones `S<n> · <name>` | Yes | — |
| Board fields Status, Priority, Size | Priority and Size used ([board](https://github.com/users/shoraLBRT/projects/3)) | Not checked from a session: the cloud proxy blocks Projects v2 (#146) |
| Priority as the board field only | Also labels `priority:P0…P2` | Drop the labels when the board takes over the order |
| Dependencies as native "blocked by" | Yes, since 2026-11-04 | — |
| Labels `type:bug`, `needs:maintainer`, `accepted` | Missing (`type:research` exists) | Create from habze's labels file (habze#2) |
| Issue forms and PR template | None | From habze (habze#2) |
| `main` protected, CI required | Not checked from a session | Check, or let habze's conformance check say (habze#10) |
| The skills reach cloud sessions | No — they live on the maintainer's machine only, removed from here in #145 | Delivery (habze#8); a cloud session here cannot run `/session-full` until then |
| Cloud routine | Findings in [#146](https://github.com/shoraLBRT/ritocode/issues/146) | habze's routine guide (habze#9) |

---

## What exists

The repository holds the platform the new product keeps and the first module built for it,
Content.

| Part | State | Where |
| --- | --- | --- |
| Modular monolith | Solution, module boundaries enforced by tests, options validated at startup, request id, unified error body, health endpoints, `/api/v1/meta/modules` | `src/`, `tests/Ritocode.Architecture.Tests` |
| API conventions | [ADR 0003](adr/0003-api-conventions.md): RFC 9457 errors, pagination envelope, validation filter. A request that cannot be read — a body that is not UTF-8 JSON, a query value of the wrong type, a body over the cap or without a JSON content type — is `400 request_invalid`, `413 request_too_large` or `415 unsupported_media_type` in every environment, not a 500. What routing refuses before any endpoint runs gets the same body (`RoutingRefusals`): a wrong method is `405 method_not_allowed` with `Allow`, a body in a non-JSON content type `415 unsupported_media_type`, an unknown address under `/api/v1` `404 not_found` | `src/Ritocode.Shared` |
| Persistence | PostgreSQL, EF Core per module, one schema each, migrations applied by `Ritocode.DbMigrator`, drift check in CI ([ADR 0004](adr/0004-persistence-and-migrations.md)) | `src/Ritocode.DbMigrator`, `scripts/` |
| Test harness | One PostgreSQL container per test assembly, one migrated database per test class. Before the container it waits up to two minutes for the Docker engine to answer (`DockerEngine`); the container is started once per assembly and a failed start is removed and reported to every caller rather than retried per test class ([#315](https://github.com/shoraLBRT/ritocode/issues/315)) | `tests/Ritocode.TestSupport` |
| Identity seam | `ICurrentUser`, real authentication schemes, authenticated by default ([ADR 0008](adr/0008-authentication-seam.md), Accepted). `GET /api/v1/me` answers the caller (id, username, `admin`, the join date `createdAt` from Users through `IUserLookup`, the linked `providers` through `ILinkedProviderLookup` — #268) or 401 | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth` |
| Sessions ([#6](https://github.com/shoraLBRT/ritocode/issues/6)) | [ADR 0012](adr/0012-sessions.md): an opaque token in `__Host-ritocode-session` (Secure, HttpOnly, SameSite=Lax), the session in `auth.sessions` by the token's SHA-256 — the token is never stored — with its expiry (30 days, `Auth:Session:Lifetime`), revocation and CSRF token. A request with the cookie is the `Session` scheme's, any other the development identity's (off outside development). Every state-changing request under a session repeats its CSRF token — from the readable `__Host-ritocode-csrf` cookie — in `X-CSRF-Token`, or gets `403 csrf_token_invalid` (`CsrfProtectionMiddleware`); `ApiClient` sends it. `POST /auth/logout` (outside `/api/v1`, through the new `IModule.MapHostEndpoints`) revokes the session and clears both cookies. `ISessionIssuer.StartAsync` + `SessionCookies.Write` are what #7's sign-in calls. Sign-in (#7) issues one; the header has no sign-in or sign-out button yet | `src/Modules/Ritocode.Modules.Auth/Session`, `src/Ritocode.Shared/Identity/CsrfProtectionMiddleware.cs` |
| Sign-in ([#7](https://github.com/shoraLBRT/ritocode/issues/7)) | `GET /auth/login/{github,google}?returnUrl=` sends the browser to the provider through ASP.NET's OAuth handler — state with its correlation cookie, PKCE (S256) — and the provider returns to `/auth/callback/{provider}`. There `AccountLinker` resolves the user: an identity already linked signs in as its user (the stored login follows a rename); a new one with a **verified** address joins the user with that address or a new user made for it (`IUserAccounts`, answered by Users; the username from the GitHub login or the address's local part, made unique); a new one without a verified address reaches nobody. GitHub's address is the **primary** one of `/user/emails`; Google's is userinfo's `email` with `email_verified`. Then a session starts (`ISessionIssuer`, `SessionCookies.Write`) and the browser returns to `returnUrl`, accepted only as a local path (`400` otherwise); a refusal or a provider failure returns there with `?signInError=email_unverified`, `provider_already_linked` or `provider_failed`. A provider is offered only when `Auth:GitHub` / `Auth:Google` (`ClientId`, `ClientSecret`) is configured. `Auth:SignIn:AppOrigin` prefixes the return path where the pages are served from another origin (Vite's `http://localhost:5173` in development; empty in production). Tested against a fake provider through the real handler. **Not yet**: a real round trip against GitHub and Google, which needs the OAuth apps of #134 | `src/Modules/Ritocode.Modules.Auth/SignIn` |
| Signed-out solving ([#127](https://github.com/shoraLBRT/ritocode/issues/127)) | SPEC §4.6 in the pages. The task screen keeps the answer — picks, leaves, step — in the tab's `sessionStorage` as it changes (`pages/task/draft.ts`), restored on a reload and removed once checked; a kept answer is restored only if every card is one the task offers and every leaf one of the tree. *Check* signed out opens a prompt with GitHub and Google, each returning to `/tasks/{slug}?check=1`; back from the provider, the answer is restored, submitted on the learner's attempt and the review opens. A `check` with no answer that fits opens the task at its start with a note; a sign-in that failed restores the answer and checks nothing. `AppLayout` reads `check` and `signInError` once, on load, and takes them out of the address (`session/signInReturn.ts`); a `signInError` shows above the page in words, until dismissed. The header has *Войти* (a menu of both providers returning to the current page) and, signed in, *Выйти* (`POST /auth/logout`, then `/me` again); a page closed to a visitor offers the providers too. `ApiClient` now sends `credentials: 'include'` — the session cookie never reached the API from Vite's origin before — and the default API address is `http://localhost:5199/api/v1`, the pages' host, so the page can read the CSRF cookie. Return paths are checked on the client by the server's rule (`isLocalPath`). Checked in Chromium at 1280 and 375 px against a stand-in API with a provider that signs in at once: signed-out answer, GitHub, review of that answer; sign-out; the header menu; a `signInError` | `frontend/src/pages/task`, `frontend/src/session`, `frontend/src/components/AppLayout.tsx` |
| Privacy page ([#128](https://github.com/shoraLBRT/ritocode/issues/128)) | SPEC §6.1 and §10.4. `/privacy` renders `frontend/src/site/privacy.ru.md` — Markdown built into the bundle (`?raw`) and prerendered with `/` and `/problems` (`privacy.html`, in the sitemap). Today the file is a **clearly marked placeholder** saying what the site receives at sign-in and keeps; the maintainer's text replaces it (#134), and nothing else changes when it does. `SignInLinks` — the header menu, the task's *Check* prompt, a closed page — now carries the notice under the providers: what signing in hands over, linking `/privacy`. The footer links it on every page. `Markdown` learned `##` and `###` headings for it. The header's sign-in menu is as wide as its content (at most 24 rem), and spans the session's row at phone width; following the privacy link closes it. Checked at 1280 and 375 px: no horizontal scroll, the menu fits. **Not yet**: the maintainer's text | `frontend/src/pages/PrivacyPage.tsx`, `frontend/src/site/privacy.ru.md`, `frontend/src/session/SignInLinks.tsx` |
| Design and the task workspace ([#188](https://github.com/shoraLBRT/ritocode/issues/188)) | [DESIGN.md](DESIGN.md), [ADR 0014](adr/0014-visual-identity-and-task-workspace.md): the look the maintainer chose over three rounds of concepts — the *Indigo* palette on a cool grey ground, **IBM Plex Sans and Mono** served with the site (`@fontsource`), rounded cards; **light by default**, dark on a header toggle kept in `localStorage` (`src/theme`, `data-theme` on the root); a colour per problem class (`k-{class}`). Every colour is a token in `styles.css`; other pages took the tokens as they are. **The task screen and its review are a workspace** (`pages/task/workspace.css`): the steps *Диагноз → Лечение → Разбор* at the centre of the header (`StepBar` through `HeaderSlot`), beside the arena's sections — kept on the workspace since #199 (the search is not), one line from 75 rem, the steps on a line of their own below that. **Since #198, layout A** (DESIGN.md › The task workspace): three columns — the context on the left (`ContextPanel`: title, difficulty, brief, *Весь контекст*), the code in the centre with a file picker opening the tree and buttons to the neighbouring files (`MaterialViewer`), what the step has gathered on the right — and the step's **dock** at the bottom of the code, which rises with the scroll (`dock.ts`: one row and a sliver at rest; its height follows the scroll delta up to the file bar and first line — or until every result is in sight — and only then do the results scroll; a grip to click; since #239; a bar on top hides it down to itself so the code can be read, #240). Step 1's matches are the catalogue's cards in the dock (`rc-pcard--dock`), each with *Подробнее* and its own *Отметить*; an opened card over the code shows only the class, the full summary and the class's description (SPEC §3.2), with ‹ n из N ›. Step 2 in the same dock: the leaves of every branch in columns, the findings as rows on the right. The review in the frame: the score and *Попробовать снова* at the top of the centre, the verdict in full under it, the verdicts on the right. At ≤ 64 rem the context or the code is behind two tabs and the step is a bottom sheet; the review keeps three tabs. Checked at 1440, 1280, 900 and 375 px, light and dark, against the development host: no horizontal scroll. **Search first** (`search.ts`: crude Russian stems, ranked by shared stems, then name, keywords, summary; matches marked): a field with *К лечению* / *Здесь чисто* (step 1) or *Проверить* (step 2) beside it, class or branch filter chips, every leaf saying where its measure lives. The review's chosen line in full — leaves against the author's, note, card, signal — or the code. **Not yet**: keywords for treatment leaves (a content change), the other pages' own layouts | `frontend/src/pages/task`, `frontend/src/theme`, `frontend/src/styles.css`, `docs/DESIGN.md` |
| Users | The `users` table, `IUserLookup`, and `IUserAccounts` (find by address, create) for sign-in. `xp` and `trust_level` removed in #119 | `src/Modules/Ritocode.Modules.Users` |
| Ownership rule | An architecture test reading compiled IL: a user's rows are reached only where the owner is in the query. It guards the Attempts module's context; the allowances are `OwnedAttempts` (every lookup by owner) and the creation in `AttemptLifecycle.StartAsync`. Its reader is proved against a test-only context | `tests/Ritocode.Architecture.Tests/OwnershipRuleTests.cs` |
| Content ([#120](https://github.com/shoraLBRT/ritocode/issues/120), [#121](https://github.com/shoraLBRT/ritocode/issues/121)) | The format of [CONTENT_FORMAT.md](CONTENT_FORMAT.md) parsed and validated — every rule of §7 tested — and `content validate` in CI (job *Validate content*). The `content` schema — taxonomy, cards, materials, tasks — and an ingest that validates first, writes in one transaction stamped with the commit, upserts by slug, retires cards and unpublishes tasks that left `content/`, and derives the material overview and the easy-task shortlist. A development host seeds `content/` on start. The public reads of SPEC §9.3 ([#9](https://github.com/shoraLBRT/ritocode/issues/9)): `GET /problems` (every live card in full, with the classes), `GET /treatments`, `GET /tasks` (a page, easy first) and `GET /tasks/{slug}` (context, brief, material with its overview, the cards to pick from — name, summary and keywords only, the shortlist for an easy task — and the other tasks over the same material). No answer key and no card weight leave the server; a test serialises a task and looks for them | `src/Modules/Ritocode.Modules.Content`, `src/Ritocode.ContentTool`, `content/` |
| Authoring ([#122](https://github.com/shoraLBRT/ritocode/issues/122), [#123](https://github.com/shoraLBRT/ritocode/issues/123)) | The `author-card` skill: drafts a card from a name, reading the live catalogue so the summary is delimited from its neighbours; checks it with `content validate`; never overwrites a card. The catalogue of [#124](https://github.com/shoraLBRT/ritocode/issues/124) was written with it, a class per pull request, to the plan in [the issue's comment](https://github.com/shoraLBRT/ritocode/issues/124#issuecomment-5935789258): **56 cards**, nine or ten per class, every agent-specific problem of SPEC §3.2 among them. They are drafts the maintainer edits. A front-matter value that contains `: ` (`type: ignore`) must be quoted; unquoted, `content validate` names the field, the file's line and the value to quote, for a list item or a text field alike. The `author-task` skill: writes a material and one task per context from the maintainer's idea, validates, and runs the **blind smoke test** — `content learner-view <task>` renders the task as the task screen receives it (no key, notes, lesson, weight or card sections; a test holds it to that), and a separate `claude -p` session with no tools, run from an empty directory outside the repository, answers it from that alone; every difference from the key is reported. One easy task made with it, `flower-shop-daily-revenue` over `flower-shop-revenue`, whose smoke answer matched the key | `.claude/skills/author-task`, `src/Modules/Ritocode.Modules.Content/Authoring`, `content/materials`, `content/tasks` |
| Attempts ([#20](https://github.com/shoraLBRT/ritocode/issues/20), [#125](https://github.com/shoraLBRT/ritocode/issues/125)) | **Scoring**: `DiagnosisScoring.Score`, a pure function of the answer, the key with the card weights, and the parameters of SPEC §5.2 (`Attempts:Scoring`, validated on start); the total floored at zero, the maximum, whether the answer is correct, and a line per card — found with its treatment, missed or extra — with the author's leaves for every card of the key. **Attempts**: the `attempts` schema; `POST /attempts` (a published task; 201), `PATCH /attempts/{id}` (the step reached, forward only), `POST /attempts/{id}/submit` (validated against the cards the task offers and the leaves of the tree, scored, stored with the content revision; the first submitted attempt at a task counts, later ones are practice, a partial unique index settles a race), `GET /attempts/{id}`, `GET /attempts?task=` (a page, newest first). A submitted attempt is never changed; a test re-ingests changed content and compares the stored result byte for byte. Submitting is capped per user (`Attempts:RateLimit`, 10 in 10 minutes, `429 attempt_rate_limited`). The key and weights come from Content through `ITaskForAttemptLookup`. **The answer hidden until revealed** ([#229](https://github.com/shoraLBRT/ritocode/issues/229), [ADR 0017](adr/0017-a-result-without-the-answer.md)): a submitted attempt carries its `outcome` — total, maximum, present, found, treated, missed, extra, solved — while `answer`, `result`, `review` and `signalledCards` stay empty until `POST /attempts/{id}/reveal` (from a submitted attempt; `409 attempt_not_submitted` otherwise) writes the learner's row for the task in `attempts.reveals`, once and for good, through `OwnedReveals`. *Solved*: every finding found with a key leaf and nothing extra (a clean task: nothing picked) — `AttemptOutcome.Of`, on read. Each attempt carries `revealed` and `taskStatus`; `GET /tasks` carries `status` — `new`, `tried`, `solved`, `gaveUp` (`TaskStatuses.Of`: solved no later than the reveal stays solved) — for a signed-in caller through `ITaskStatusLookup`, which replaced `ISubmittedTaskLookup`. Tested at the API (`AnswerRevealTests`): before the reveal no card, leaf, note or lesson of the key or the answer is in the body | `src/Modules/Ritocode.Modules.Attempts`, `src/Ritocode.Shared/Contracts` |
| Progress ([#24](https://github.com/shoraLBRT/ritocode/issues/24), [#30](https://github.com/shoraLBRT/ritocode/issues/30), [#235](https://github.com/shoraLBRT/ritocode/issues/235)) | `GET /api/v1/me/progress`: from the caller's **first** attempts only, computed on read by a pure `ProgressCalculator` — per class (every class, in taxonomy order): findings met, found, found and treated right; per card: met, found, missed, picked when absent, treated right — **only from the tasks whose answer the caller has revealed** (#235, ADR 0017; the classes and the task count take every first attempt); how many tasks it is built from; and `notRevealed`, how many of them the cards leave out. Classes and cards carry their names (#30): the class of each card and both names come from Content through `ICardClassLookup` (retired cards included; a card Content does not know is named by its slug, with no class). **The page** `/profile` (#261; `/progress` redirects there), signed in, reached from the avatar's menu and the header's streak: the person (the letter, the username, «На Ritocode с марта 2026 г.» from `/me`'s `createdAt`, the streak and the linked providers as chips — #268) and the six classes (found of met as a bar, treated right) on the left; tasks solved as a ring and by difficulty (from `GET /tasks`), three diagnosis tiles, a year of activity (53 weeks, attempts and cards studied, active days, the longest streak — `yearActivity` in `pages/home/arena.ts`) **Изучение** (#281: the published cards marked studied of all and per class in the class colours, and a hand-SVG step chart of the count over time from the first mark — `studyProgress`/`chartPath` in `pages/profile/profile.ts`, `StudyCard.tsx`; no mark: zeros and a button to `/problems`) and a panel with the ten recent submitted attempts and, under `?tab=cards`, the per-card table with a class filter, zeros muted and the note counting the tasks not yet counted per card until their answer is revealed (#235). A learner with no attempt sees the same frame and an invitation to `/tasks`. Each card loads and fails on its own. Checked at 1280 and 375 px, with and without attempts A test submits first and practice attempts and another user's, and shows practice and others change nothing; another submits a task, finds none of its cards in the response before the reveal and all of them after (`ProgressBeforeTheRevealEndpointTests`) | `src/Modules/Ritocode.Modules.Attempts/Progress`, `frontend/src/pages/profile` |
| Signals ([#129](https://github.com/shoraLBRT/ritocode/issues/129)) | The `attempts.signals` table and `POST /api/v1/signals` (`{ attempt, card, comment? }`, 201): sent only from an **extra pick** of the caller's own **submitted** attempt whose answer is **revealed** (#229: before it, any card is `409 answer_not_revealed`) — another user's attempt is a 404 like a missing one, an open attempt is `409 attempt_not_submitted` whatever the card (so it cannot probe the key), a card that is not an extra pick is `400` on `card`, a second signal for the same pick is `409 signal_already_sent` (a unique index settles a race). The comment is trimmed, blank is none, at most 500 characters. Capped per user (`Attempts:SignalRateLimit`, 10 in 10 minutes, `429 signal_rate_limited`). The attempt is read untracked and never written; `GET /attempts/{id}` lists `signalledCards`. Reached through `OwnedSignals`, the ownership rule's new allowance. **In the review**, each extra pick has *По-моему, это здесь есть* (gender-neutral since #217): a one-line comment, send or cancel, then "sent, the score does not change"; a pick signalled before shows as sent. Checked in a browser at 1280 and 375 px | `src/Modules/Ritocode.Modules.Attempts/Signals`, `frontend/src/pages/task/SignalControl.tsx` |
| Study marks ([#280](https://github.com/shoraLBRT/ritocode/issues/280), [ADR 0018](adr/0018-studying-problems.md)) | The `attempts.study_marks` table (user, card, `studied_at`; one per user and card) and `GET /api/v1/me/studied` (`{ cards: [{ card, studiedAt }] }`, oldest first), `PUT /api/v1/me/studied/{card}` (200 with the mark; marking again keeps the first date; `404 card_not_found` for a card Content does not publish) and `DELETE` (204, idempotent), signed in only. The card is checked through a new contract, `IPublishedCardLookup` (Content); a mark on a card later retired stays stored, is left out of the list, and shows again if the card returns. Reached only through `OwnedStudyMarks` (ownership allowance). On `/problems` the open card has *Изучено*, a toggle (`aria-pressed`, optimistic, put back if the API refuses); a folded card shows a check and *Изучено* on its last line (the check alone under 30rem); each class heading says *изучено N из M*. A guest marks too: kept in `localStorage` (`ritocode:studied`) with a note saying so, sent nowhere, not carried over on sign-in. The marks are not shown until the session is known, so the prerendered page carries none. Since #281 the marks count as practice: a day with a submitted attempt **or** a mark is active and its count is both, in the streak and activity of the profile, the home page and the header (`activity`/`yearActivity` take the marks; `listPracticeSince` reads attempts and `GET /me/studied` together); a mark taken back is no longer served, so no longer counted; the header reads its streak again on the `ritocode:study-marks-changed` window event `useStudyMarks` sends after the API stores or removes a mark. The profile's *Изучение* is under Progress | `src/Modules/Ritocode.Modules.Attempts/Studying/`, `src/Ritocode.Shared/Contracts/Content/IPublishedCardLookup.cs`, `frontend/src/hooks/useStudyMarks.ts`, `frontend/src/pages/ProblemsPage.tsx` |
| Feedback ([#243](https://github.com/shoraLBRT/ritocode/issues/243)) | SPEC §9.6, [ADR 0019](adr/0019-feedback-as-github-issues.md). `POST /api/v1/feedback`, anonymous: the message becomes a GitHub issue with the `feedback` label through `IFeedbackIssues` (`GitHubFeedbackIssues` as a GitHub App, `Feedback:GitHubApp:AppId`/`PrivateKey` through `GitHubAppCredential` (#285), or with `Feedback:GitHubToken`; `Log` on the local stack; a fake in tests), its text in a code fence so nothing in it is rendered. A length cap, an in-memory per-address limit, and Turnstile behind `ICaptchaVerifier` (off with no secret). The widget (`frontend/src/feedback/`) is in `AppLayout` on every page; Turnstile renders only with `VITE_TURNSTILE_SITE_KEY` (in the release images from the repository variable `TURNSTILE_SITE_KEY`). Deploy: `FEEDBACK_GITHUB_APP_ID` and `FEEDBACK_GITHUB_APP_PRIVATE_KEY` (or `FEEDBACK_GITHUB_TOKEN`), `TURNSTILE_SECRET_KEY` in `production.env`, Turnstile in the site CSP | `src/Ritocode.Api/Feedback/`, `frontend/src/feedback/`, `tests/Ritocode.Api.Tests/Endpoints/FeedbackEndpointTests.cs`, `tests/Ritocode.Api.Tests/Feedback/GitHubFeedbackIssuesTests.cs` |
| Admin area ([#130](https://github.com/shoraLBRT/ritocode/issues/130)) | SPEC §6.2. Admins are named by address in `Users:Admin:Emails` (the development identity in `appsettings.Development.json`). **The policy**: `AdminPolicy` in Shared, its requirement met by the Users module's `AdminAuthorizationHandler` (the caller's stored address against the list); `GET /me` carries `admin` from the same policy. A signed-in non-admin gets the **same `404 not_found`** as any address under `/api/v1` that serves nothing — the API now has a fallback giving such an address the unified body (`NoSuchAddress`), and `AdminRefusalAsNotFound` returns that very result; signed out, 401. **Endpoints**: `GET /admin/signals?status=open\|resolved` and `POST /admin/signals/{id}/resolve` (idempotent; `404 signal_not_found`) and `GET /admin/attempts?status=all\|open\|submitted&user=` in Attempts — every user's rows through `EveryonesRows`, the ownership rule's new allowance; `GET /admin/users` in Users — newest first, with providers from Auth (`ILinkedProviderLookup`) and attempts and tasks solved from Attempts (`IAttemptTallyLookup`); learners' addresses reach Attempts through `IUserContactLookup`. All paged; a bad filter is `400` naming every bad parameter. **Pages** `/admin` (the overview, #262), `/admin/signals`, `/admin/users`, `/admin/attempts` under `RequireAdmin` (anyone else sees the not-found page); filters and page in the address; a user's attempt count links to their attempts; an open attempt shows the step it stopped at. *Админка* in the header for an admin. Checked in a browser at 1280 and 375 px: a signal sent from a review listed and resolved, an attempt abandoned at step 2 found in the open attempts | `src/Modules/Ritocode.Modules.Attempts/Admin`, `src/Modules/Ritocode.Modules.Users/Admin`, `src/Ritocode.Shared/Identity/AdminPolicy.cs`, `frontend/src/pages/admin` |
| Admin system page ([#297](https://github.com/shoraLBRT/ritocode/issues/297)) | SPEC §6.2 › System. `GET /api/v1/admin/system` (behind `AdminPolicy`, so a non-admin gets the unknown-address 404) is **host-level** (`src/Ritocode.Api/Admin`): `SystemReader` composes it without a module referencing another — each module's migrations through its own context (`ModuleDbContextRegistration`, `GetMigrations` against `GetAppliedMigrationsAsync`: `upToDate`, `pending` with the names, or `failing`), the content revision and its time through the Shared contract `IContentRevisionLookup` (answered by Content from the taxonomy row every ingest rewrites), the database's size (`pg_database_size`), Umami's `/api/heartbeat` at `System:UmamiUrl` (`http://umami:3000` in the production Compose file; unset → `notConfigured`), and the feedback App's credential (`FeedbackCredentialCheck`: an installation token through `GitHubAppCredential`, cached as messages do; no App, or the log sink → `notConfigured`). A part that fails is `failing` and the report is still made. `backup` is the heartbeat `deploy/backup.sh` leaves (#296) at `System:BackupHeartbeatPath` (`/backup-heartbeat/last-good` in the production Compose file, `BACKUP_DIR/heartbeat` mounted read-only), `null` when none is recorded. The API's version and commit are its informational version (`0.1.0+<commit>`): SourceLink stamps it from git locally, the image from the build argument `SOURCE_COMMIT` (`-p:SourceRevisionId`). The web image writes `version.json` (`{"commit": … | null}`) from `SOURCE_COMMIT` in the prerender, served as a file of the build; the page reads it from the site's origin, unknown on the dev server. **Not yet**: the release workflow passing `SOURCE_COMMIT` (second PR) | `src/Ritocode.Api/Admin`, `frontend/src/pages/admin/AdminSystemPage.tsx` |
| Admin usage over time ([#304](https://github.com/shoraLBRT/ritocode/issues/304)) | SPEC §6.2, §8. `GET /api/v1/admin/stats/activity?from=&to=&bucket=day\|week` (behind `AdminPolicy`): UTC dates `yyyy-MM-dd`, both included, the last 30 days by default, at most 366 days; a bad date, `from` after `to`, a longer range or another bucket is a `400` naming the parameter. Per bucket (a UTC day, or seven days counted from `from`, the last possibly shorter): active learners (a submitted attempt or a study mark, as the streak counts), new users, attempts started and submitted, answers revealed, signals sent, cards studied. Over the range: learners active on `to`, in the 7 and 30 days ending on it, the learners active in the range and how many were active again ≥ 7 days after their first active day in it. Computed on read by a host-level composer (`ActivityReader`) over two Shared contracts — `ILearnerActivityLookup` (Attempts, every user's rows through `EveryonesRows`) and `IUserSignupLookup` (Users); the moments are fetched and bucketed in memory, fine at this size. `/admin` shows them under the tiles: the four figures and one hand-drawn SVG bar chart a figure, 7/30/90 days (90 by week), with a note that days are UTC | `src/Ritocode.Api/Admin/Activity*.cs`, `frontend/src/pages/admin/AdminActivity.tsx` |
| Admin traffic from Umami ([#306](https://github.com/shoraLBRT/ritocode/issues/306)) | SPEC §6.2, §8. `GET /api/v1/admin/stats/traffic?from=&to=` (behind `AdminPolicy`; the range is read as the usage over time reads it, `ActivityEndpoints.ParseRange`) asks Umami's API at `System:UmamiUrl` with `Traffic:UmamiToken` (Bearer) for the site `Traffic:UmamiWebsiteId`: `/stats` (visitors, visits, page views — a number in Umami 3, `{value}` in Umami 2, both read), `/metrics?type=referrer` and `type=path` (top 10, a row without a name left out), and `type=event` for the funnel — `task-opened` → `step-2-reached` → `check-signed-out` → `sign-in-completed` → `attempt-submitted`, every step in order, a missing one 0. The funnel is **event counts**, not visitors followed through the steps (Umami's funnel report differs between versions); the panel says so. `status`: `ok`, `notConfigured` (address, token or site id unset), `failing` (no answer, a non-2xx, or a body that is not Umami's), `refused` (401/403 — the token) — never a 500; only `ok` carries figures. `dashboardUrl` is `Traffic:UmamiDashboardUrl` + `/websites/<id>` (`https://UMAMI_DOMAIN` in production). Nothing is stored. Production: `UMAMI_WEBSITE_ID` and `UMAMI_API_TOKEN` in `production.env`; the token is Umami's sign-in token (RUNBOOK › Get an Umami token) — it outlives restarts and ends when that account's password or `UMAMI_APP_SECRET` changes (both seen on the local Umami). `/admin` shows the panel under the usage over time, with its own 7/30/90-day picker. Checked against a local Umami 3 (the `postgresql-latest` image, a database of its own in the compose PostgreSQL): page views, referrers and the five events sent to `/api/send` came back through the API and on the page at 1280 and 375 px, and Umami stopped showed *failing* with the rest of the page working | `src/Ritocode.Api/Admin/Traffic*.cs`, `frontend/src/pages/admin/AdminTraffic.tsx` |
| Admin statistics per task and card ([#305](https://github.com/shoraLBRT/ritocode/issues/305)) | SPEC §6.2 › Statistics, §3.4, §5, §8. In the Attempts module (`Admin/StatsReader.cs`), behind `AdminPolicy`: **`GET /api/v1/admin/stats/tasks`** — every task Content knows (published or not) and every slug an attempt names: title, difficulty, material lines and the difficulty's band (`maxLines`, `minutesFrom`/`minutesTo` — SPEC §3.4's *Whole task*, easy 5–5, medium 10–15, hard 20–25, `ContentRules.TaskMinutes`) through the new Shared contract `ITaskSummaryLookup` (Content); first and practice attempts (submitted); `learners` (with a submitted attempt); `solvedShare` — learners whose status is *solved* by the task list's rule (`TaskStatuses`: an attempt, first or practice, solved it no later than the reveal) — and `revealedShare`, both of `learners`; `time` — **first attempts only**, start to submit, `medianSeconds` and the quartiles by linear interpolation between closest ranks, rounded to the second; `open` — attempts never submitted by the step reached (`diagnosis`, `treatment`); `meanScoreShare` — mean of score ÷ maximum over first attempts; `rarelySolved` — at least 5 learners and a solved share below 20 %. **`GET /api/v1/admin/stats/cards`** — the learner's per-card measures (`ProgressCalculator`) over every learner's first attempts, every one counted as revealed: met, found, missed, picked when absent, treated right, with name and class; `pickedMoreThanFound` when picked when absent > found. Both are whole reports (`{tasks}`, `{firstAttempts, cards}`), not paged collections: the page sorts them. `GET /admin/attempts` takes `?task=` and `?card=` (the submitted attempts whose result names the card, a jsonb containment test). Computed on read in memory. **Page** `/admin/stats` (*Статистика* tab): two tables sorted on the client, a task or card linking to its attempts, the outliers chipped. Checked in a browser at 1280 and 375 px on the shared dev data: both tables, a sort, a card's link to its one attempt, no page scroll on a phone | `src/Modules/Ritocode.Modules.Attempts/Admin/Stats*.cs`, `src/Ritocode.Shared/Contracts/Content/ITaskSummaryLookup.cs`, `frontend/src/pages/admin/AdminStatsPage.tsx` |
| Data export and account deletion ([#299](https://github.com/shoraLBRT/ritocode/issues/299), [ADR 0021](adr/0021-personal-data-export-and-erasure.md)) | 152-FZ. `GET /api/v1/me/export`: a JSON attachment `ritocode-export-<UTC date>.json` — `{ exportedAt, user, modules: { users, attempts, auth } }`, each module's own records; sessions by their dates only (no token, hash or CSRF token); an attempt's `result` and `review` only for a task whose answer the caller revealed (ADR 0017). `DELETE /api/v1/me` with `{"username":"…"}` (trimmed, compared without case — usernames are stored lower-cased; another, empty or missing → `400`, nothing erased): every module erases its own rows through `IPersonalData` (`Ritocode.Shared.PersonalData`, outside `Contracts`, one per module that keeps a `UserId`), the Users module's account row last, then `204` and the session cookies cleared; CSRF required with a session. Each deletion logs event 1210 with the id only. No cross-schema transaction: erasing is idempotent and a failure part-way leaves a deletable account. Admin lists and usage statistics read the same rows, so a deleted user is gone from them. `/profile` ends with a card «Мои данные»: a download link and *Удалить аккаунт*, which asks for the username typed out and goes home signed out. Backups: RUNBOOK › Personal data (7-day bucket lifecycle, a by-hand deletion, re-deleting after a restore); the privacy placeholder says it | `src/Ritocode.Shared/PersonalData/`, `src/Modules/*/PersonalData/`, `src/Modules/Ritocode.Modules.Auth/Session/AccountEndpoints.cs`, `frontend/src/pages/profile/AccountCard.tsx`, `tests/Ritocode.Api.Tests/Endpoints/AccountEndpointTests.cs`, `tests/Ritocode.Architecture.Tests/PersonalDataTests.cs` |
| Error reporting ([#294](https://github.com/shoraLBRT/ritocode/issues/294)) | ADR 0020 decisions 1 and 4. A fifth module, **Errors**, owns the `errors` schema (`error_groups`, `error_occurrences`; DATABASE_SCHEMA.md). `AppExceptionHandler` hands every unexpected exception to `IServerErrorRecorder` (Shared/Diagnostics, host infrastructure like `IPersonalData`) with the route template and the request id; the recorder writes in its own scope, never throws, and waits at most 5 s. **Fingerprint**: source, type and the top three frames (server: methods without file or line; browser: function names, plus the message with digits taken out). The count and the reopening are single statements, so concurrent occurrences count twice and reopen once; a race on the first one is settled by `ux_error_groups_fingerprint`. **Pruning** on every write and every 6 h: 30 days, the latest 50 per group (`Errors:*`). **Browser**: `POST /api/v1/client-errors` — anonymous, JSON only, 16 KiB read at most, 10 a minute per address and 120 in all (in memory), the page as a path without query or fragment, `202` with the report's request id in its header; `frontend/src/errors/reporter.ts` (installed in `main.tsx`) sends `window.onerror`, `unhandledrejection` and the route table's `ErrorBoundary`, `RouteErrorPage`, which shows *Что-то сломалось* and the report's number; once per error per page load, 10 at most. **Admin**: `GET /admin/errors?status=open\|resolved`, `GET /admin/errors/{id}` (the kept occurrences), `POST /admin/errors/{id}/resolve`; the page `/admin/errors` (*Ошибки*). **Alerts**: a new or reopened group queues one message, sent by a background service through the Bot API as plain text — source, type, location, count, the link; no message, stack, request id or user id — at most 20 an hour; `Errors:Telegram:BotToken`/`ChatId` (`TELEGRAM_BOT_TOKEN`/`TELEGRAM_CHAT_ID`), the client factory's logging off because the token is in the address. RUNBOOK › An error was reported | `src/Modules/Ritocode.Modules.Errors`, `src/Ritocode.Shared/Diagnostics/IServerErrorRecorder.cs`, `frontend/src/errors`, `frontend/src/pages/admin/AdminErrorsPage.tsx` |
| Security baseline ([#35](https://github.com/shoraLBRT/ritocode/issues/35)) | SPEC §10.1, one test per item: the ownership rule guards attempts, signals, reveals and study marks (`AttemptsSignalsRevealsAndStudyMarks_AreGuarded`, study marks since #280); the submit and signal caps (`RateLimitTests`, `SignalRateLimitTests`); **every** state-changing endpoint the host maps, read from its routing, refuses a session request without the CSRF token (`EveryStateChangingEndpoint_RefusesASessionRequestWithoutTheCsrfToken`); the return address only as a local path (`SignInTests`); every `/api/v1/admin` endpoint carries the admin policy and a non-admin gets the unknown-address 404 (`EveryAdminAddress_IsBehindTheAdminPolicy`, `NonAdminEndpointTests`); answers bounded to offered cards, existing leaves, at most 100 picks and 50 leaves, a leaf at most 129 characters, a comment at most 500. New here: `SecurityHeadersMiddleware` — `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, `Referrer-Policy: no-referrer`, and `Cache-Control: no-store` unless an endpoint sets its own — on every response, a 500 included; and a request body cap, `Api:MaxRequestBodyBytes` (64 KiB), on Kestrel | `src/Ritocode.Shared/Http/SecurityHeadersMiddleware.cs`, `src/Ritocode.Api/Setup/ApiSetupExtensions.cs`, `tests/Ritocode.Api.Tests/Endpoints/SecurityBaselineTests.cs` |
| Analytics ([#133](https://github.com/shoraLBRT/ritocode/issues/133)) | SPEC §8. Umami's script is added by `main.tsx` only when the build names both `VITE_UMAMI_SCRIPT_URL` and `VITE_UMAMI_WEBSITE_ID` (the web image takes them as build arguments, from the repository variables `UMAMI_SCRIPT_URL` and `UMAMI_WEBSITE_ID`); otherwise `track` is a no-op and no script reaches the page. Page views, route changes included, are Umami's own. Events (`src/analytics`): `task-opened` (task, difficulty), `step-2-reached`, `check-signed-out`, `attempt-submitted` (task), `signal-sent` (card), `catalogue-search` (once a visit, nothing typed), `sign-in-completed` (provider — the provider link notes it in the tab's `sessionStorage`, the page that comes back signed in counts it). No user id, address or text in any event. Events sent before the script loads wait for it. Checked against a local Umami (its Docker image on the compose network, the site registered by a row in its database): all seven events and the page views arrived with only those values, and no response set a cookie | `frontend/src/analytics`, `deploy/web.Dockerfile` |
| Production stack ([#135](https://github.com/shoraLBRT/ritocode/issues/135)) | `deploy/compose.production.yml`: Caddy at the edge (`deploy/Caddyfile` — Let's Encrypt for `SITE_DOMAIN` and `UMAMI_DOMAIN`, HTTP → HTTPS, HSTS and the pages' content policy, `/api/*` `/auth/*` `/health/*` to the API, the rest to the web image), the API and web images at `RITOCODE_VERSION`, PostgreSQL 17 on a volume, Umami with its own role and database (`deploy/postgres-init`), a one-off `migrate` service; every service on the `json-file` log driver with a cap. Secrets in `production.env` on the server (`deploy/production.env.example`; git-ignored). The API learned two settings for it: `Api:BehindProxy` (trust the edge's `X-Forwarded-Proto`/`-For`, so the sign-in callback is `https://`) and `Api:DataProtectionKeysDirectory` (the key ring on the `api-keys` volume). Smoke-run locally with `SITE_DOMAIN=localhost` (Caddy's own CA). **Not yet**: the real checks — the site on its domain with a valid certificate, sign-in with GitHub and Google there, Umami receiving production events — which need the VPS, domain and OAuth apps of #134 | `deploy/`, `src/Ritocode.Api/Setup/ApiSetupExtensions.cs` |
| Release and backups ([#136](https://github.com/shoraLBRT/ritocode/issues/136)) | `RITOCODE_SERVER=… ./scripts/release.sh [commit]` from a clean checkout: sends that commit's `deploy/` to the server and runs `deploy/release.sh` — pull, migrate, **ingest** the `content/` shipped in the API image stamped with the commit (the migrator's new `ingest <root> <revision>` command), start, wait for health; rollback is releasing the previous commit. `deploy/backup.sh` (daily, cron): both databases dumped, seven days kept, each copied with `rclone` to `BACKUP_REMOTE` — an S3-compatible bucket at a Russian provider in another region (the decision of #136). `deploy/restore.sh`: into an empty database, then the API restarted. Images from `RITOCODE_REGISTRY` (GHCR by default). Release and restore performed locally against a local registry: release, dump, every volume removed, restore, the site served from the restored database. **Not yet**: the same on the server of #134, and the bucket | `deploy/`, `scripts/release.sh`, `src/Ritocode.DbMigrator/ContentIngestCommand.cs` |
| Maintenance page ([#298](https://github.com/shoraLBRT/ritocode/issues/298)) | The edge (`deploy/Caddyfile`) answers 502/503/504 — nothing behind it answered, or maintenance is on — with its own page, `deploy/maintenance/index.html` (Russian, the light theme's tokens copied in, no script, nothing else requested), mounted read-only at `/srv/maintenance`: always **503**, `Retry-After: 60`, `no-store`, the site's headers and a page-only CSP. A status the API answered itself, its own 503 included, passes through. The switch is the file `deploy/maintenance/on` (git-ignored), read on every request: `deploy/release.sh` creates it after the pull and removes it once `/health/ready` passes; a failed release leaves it on (RUNBOOK › Maintenance, also how to switch by hand). The pages: `ApiClient` reports every outcome to an `ApiAvailability` store — unreachable on a network failure or a 502/503/504 that is not the API's own problem body, reachable on anything else — and `ApiUnavailableBanner` in the layout shows one sentence with *Обновить* while it is unreachable; `ErrorState` then says only that the data did not load. Checked on the local production stack (web image built from the branch, API from `main`): API stopped → 503 with the page on `/api/*` and `/health/ready`, pages still served; `touch maintenance/on` → 503 everywhere, removed → 200; `release.sh` run against a local registry switched it on and off. The banner seen in a browser against an unreachable API; the page at 1280 and 375 px. **Not yet**: the same on the server of #134 | `deploy/Caddyfile`, `deploy/maintenance/`, `deploy/release.sh`, `frontend/src/api/availability.ts`, `frontend/src/components/ApiUnavailableBanner.tsx` |
| Monitoring ([#34](https://github.com/shoraLBRT/ritocode/issues/34)) | [ADR 0013](adr/0013-uptime-monitoring.md), **Proposed**: the *Monitor* workflow runs every five minutes from GitHub against the addresses in the repository variable `MONITOR_ORIGINS` (empty: it does nothing). `scripts/monitor-check.sh`: the site's `/health/ready` answers `Healthy`, every address's certificate is valid and more than 14 days from expiry, a failed request retried twice. `scripts/monitor-alert.sh`: one open issue labelled `monitoring-alert` mentioning the owner, a new comment only when what fails changes, closed on recovery; the run succeeds when it has reported, so GitHub sends no e-mail per failed run. `scripts/monitor-test.sh` tests the alert against a fake `gh` (CI job *Test the alert*, on pull requests touching it). Checked locally: a running API passes, the stopped API fails; certificates of `github.com` pass and `expired.badssl.com` fails. **Not yet**: the site on its domain, the variable set, and the API stopped there producing an alert issue — which needs the VPS and domain of #134 | `.github/workflows/monitor.yml`, `scripts/monitor-*.sh`, [RUNBOOK.md](RUNBOOK.md#monitoring) |
| Server checks ([#296](https://github.com/shoraLBRT/ritocode/issues/296)) | [ADR 0020](adr/0020-observability.md) decisions 3 and 4. `deploy/check.sh`, from cron every five minutes on the server (POSIX sh): free disk on `MONITOR_DISK_PATHS` (below 15 %), available memory (below 10 %; `MemFree` where the kernel has no `MemAvailable`), five-minute load (above 2 per CPU), every long-running service of the production Compose file (missing, not running, unhealthy, or restarted within 15 minutes; `migrate` is one-shot), the product database's size (above 5 GB) and connections (80 % of `max_connections`), and the backup (last good one older than 26 h, or the last run failed); the age of the API's oldest log line is reported, never alerted on. While `maintenance/on` exists the containers and the database are not checked. Every threshold is a `MONITOR_*` variable (shell or `production.env`). `deploy/alert.sh`: Telegram (`TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID`; `TELEGRAM_API_BASE` for a stub) on a change of the FAIL lines and on recovery, the last sent set in `monitor-state` (git-ignored), written only after Telegram took it; the token goes to curl on stdin. `deploy/backup.sh` writes `BACKUP_DIR/heartbeat/last-good` (epoch seconds) only when every dump was copied off the server — `BACKUP_REMOTE` empty now counts as a failed run — and `last-failed` on any failure; `release.sh` makes the directory so Docker does not, as root; the API mounts it read-only for `/admin/system`'s `backup`. `scripts/monitor-alert.sh` also sends Telegram when those two variables are set (the workflow does not pass them yet). Tests: `deploy/check-test.sh` against a fake docker, df, `/proc` and curl (dash and mawk checked in Debian), run from `scripts/monitor-test.sh` and from `DeployScriptTests`, so the backend CI runs it on every PR. Checked on the local production stack (images from `main`, a local Telegram stub, a stand-in `rclone`): a backup to an unreachable remote raised `backup last run`, a good one cleared it, `MONITOR_DISK_FREE_PERCENT=101` raised the disk alert, removing it sent the recovery. **Not yet**: the same on the server of #134 with a real bot and bucket; the system page's backup line seen with an API image built from this branch | `deploy/check.sh`, `deploy/alert.sh`, `deploy/backup.sh`, `src/Ritocode.Api/Admin/BackupHeartbeat.cs`, [RUNBOOK.md](RUNBOOK.md#the-servers-checks) |
| Logs kept and read without SSH ([#295](https://github.com/shoraLBRT/ritocode/issues/295)) | [ADR 0020](adr/0020-observability.md) decision 2. **Retention**: the API and Caddy on `json-file` 10 × 20 MB with the rotated files compressed (`x-logging-kept` in `deploy/compose.production.yml`), the rest 5 × 10 MB — 14 days or the cap, whichever comes first; a release starts the API's log anew; `deploy/check.sh` reports how far back it reaches. **Reading**: Dozzle (`amir20/dozzle:v11`) at `SITE_DOMAIN/admin/logs/` (`DOZZLE_BASE`), actions and shell off, only the `ritocode-production` project's containers, reading Docker through `tecnativa/docker-socket-proxy` (reads of containers, logs, events, info, version; `POST: 0`; the socket mounted read-only). Caddy routes `/admin/logs*` before the API and the pages, with `forward_auth` to the new `GET /api/v1/admin/access` (the admin policy: 204 an admin, 404 a signed-in non-admin, 401 signed out), drops the cookie before Dozzle and flushes its stream. Search by request id and by level is Dozzle's text search (RUNBOOK › Read logs). The privacy placeholder says what the log holds, up to 14 days, admins only. Tests: `AdminAccessEndpointTests` and the endpoint in the admin address lists of `AdminEndpointTests`. **Not yet**: run on the local production stack (`SITE_DOMAIN=localhost`) — a request's lines found by its id in Dozzle, a signed-out visitor refused — Docker was unavailable to the session that built it; then the same on the server of #134 | `deploy/compose.production.yml`, `deploy/Caddyfile`, `src/Ritocode.Api/Admin/AccessEndpoints.cs`, [RUNBOOK.md](RUNBOOK.md#read-logs) |
| Runbook ([#41](https://github.com/shoraLBRT/ritocode/issues/41)) | `docs/RUNBOOK.md`: the checklist before each release, first setup of a server, release, roll back, restore a backup, rotate each secret, read logs, add an admin, publish new content — commands for the scripts of #135 and #136. Written before a server exists: the first real release will show what it misses | `docs/RUNBOOK.md` |
| Home, the arena ([#131](https://github.com/shoraLBRT/ritocode/issues/131), [#190](https://github.com/shoraLBRT/ritocode/issues/190)) | `/` as SPEC §4.1 and DESIGN.md › The arena describe it: the lead (a placeholder until the copy is worked out) with the real counts, the demo task and the ways to `/tasks` and `/problems`; the **daily task** (`pages/home/arena.ts`: a pure function of the Moscow day and the task list, unsolved first, changing at midnight; a countdown to it, and at zero the next task — #201); six suggested tasks — unsolved first, the daily one left out — and four problems to read, both picked in an order fixed by the Moscow day (`suggestedTasks`, `suggestedProblems`; #200), no classes on tasks and no difficulty tabs; for a signed-in learner progress by class (`GET /me/progress`), the **streak** and twelve weeks of **activity**, counted in the browser from `GET /attempts`; for a guest, the sign-in links. Prerendered with the catalogue's counts and the problems to read from the content export (`StaticHomePage`); the export has no tasks, so the suggested tasks are filled in by the application. Checked at 1280 and 375 px, light and dark, signed in and out, against a stub API | `frontend/src/pages/HomePage.tsx`, `frontend/src/pages/home`, `frontend/src/site` |
| Prerendered public pages ([#132](https://github.com/shoraLBRT/ritocode/issues/132)) | `content export <file> [path]` writes the problem catalogue exactly as `GET /problems` serves it — one mapping, `ProblemCatalogueMapping`, now behind both, and a test holds the export to the API's body. `npm run build:static` (`CONTENT_EXPORT`, `SITE_ORIGIN`) renders `/` and `/problems` with React's server renderer into `dist/index.html` and `dist/problems.html` — every card in full with its anchor — each with its title, description and canonical address, plus `sitemap.xml` and `robots.txt`. Since #300 it also writes the shell under every other address of the site — the fixed routes (`SHELL_PATHS`, held to `src/routes.tsx` by `site/addresses.test.ts`) and `tasks/{slug}.html` for each task slug the export now carries — and `404.html`, the not-found page rendered with `noindex`; the sitemap takes `lastmod` from `CONTENT_LASTMOD`. The web image (`deploy/web.Caddyfile`) serves a file for an address and **404 with `404.html` for anything else**, and redirects `/index.html`, any `*.html`, a trailing slash and `/progress` (308) to the one canonical form. `npm run lighthouse` holds the static pages to a budget (accessibility and SEO 90, performance 45). Titles and descriptions come from one table (`site/pageMeta.ts`) that the application also applies on every route change. The application renders over the static markup once loaded. CI job *Prerender public pages* runs the export and the build and checks every card is on the page. Checked in a browser: the raw HTML holds every card and anchor, and the application takes over and scrolls to `#money-in-float` | `src/Ritocode.ContentTool`, `frontend/src/prerender`, `frontend/scripts/prerender.mjs` |
| A page per card and per task ([#301](https://github.com/shoraLBRT/ritocode/issues/301)) | SPEC §4.1, §4.2, §9.4. `content export` now carries each task as `{slug, title, difficulty, context, brief}` in `GET /tasks`' order (`ExportedTask`) — never the key, notes, lesson, weights or cards; `ContentExportTests` holds the export to those five fields and to the API's list. `npm run build:static` prerenders **`/problems/{slug}`** for every card (the card in full in its class's colours, the class, the other cards of the class as links; title `{name} — проблема кода от ИИ · Ritocode`, the summary as description), **`/tasks`** (the task table as a guest sees it; it was a bare shell) and **`/tasks/{slug}`** for every task (`TaskPreviewPage`: title, difficulty, brief, context — it takes a `TaskPreview`, so nothing else can reach it; `prerender.test.tsx` feeds an export with a smuggled key, notes and cards and finds none in any file), each with its canonical address and in `sitemap.xml`. `checkPages` fails the build — and so the CI job *Prerender public pages*, with no workflow change — when a card or task has no page, no heading with its text, or no sitemap entry. In the application `/problems/:slug` is `ProblemPage` (an unknown slug is the not-found page); the open card in the catalogue links to it, and so does a card unfolded without JavaScript; `/problems#slug` still opens the card. `usePageMeta` lets a page set its title from what it loaded — the card page and the task screen do. `npm run lighthouse` also checks `/tasks`, one card page and one task page (all within budget; accessibility and SEO 100). Checked in a browser at 1280 and 375 px with the scripts stripped: card page, its related cards, task page and `/tasks`, no page scroll on a phone. An attempt's address still serves `tasks/{slug}.html`, now the task's prerendered page, which the review renders over | `src/Modules/Ritocode.Modules.Content/Catalogue/ContentExport.cs`, `frontend/src/prerender`, `frontend/src/pages/ProblemPage.tsx`, `frontend/src/pages/task/TaskPreviewPage.tsx` |
| Link previews and structured data ([#302](https://github.com/shoraLBRT/ritocode/issues/302)) | SPEC §4.1. Every prerendered public page (`/`, `/problems`, `/tasks`, `/privacy`, each card and task page) carries `og:type/site_name/locale/url/title/description/image` (with type, 1200×630 size and alt) and `twitter:card` `summary_large_image` with title, description and image; the page's title and description are the ones `pageMeta.ts` gives, `og:url` is its canonical address from `SITE_ORIGIN`. JSON-LD (`src/prerender/seo.ts`): `WebSite` + `Organization` on `/`, `DefinedTermSet` with every card as a `DefinedTerm` on `/problems`, a `DefinedTerm` in that set on each card page, an `ItemList` on `/tasks`, a `LearningResource` (difficulty as `educationalLevel`, `timeRequired` PT5M/PT15M/PT25M, free) on each task page, `WebPage` on `/privacy`; serialised with `<`, `>`, `&` escaped so a card's text cannot close the script. `404.html` has neither. **Preview images**: 1200×630 PNG under `dist/og/` — `home` (also `/privacy`'s), `problems`, `tasks`, and one per card (its class's colour and name) and per task (difficulty and time, the brief) — drawn as SVG from the page's text (`preview.ts`, width estimated from Plex's advances, titles wrapped to three lines then shrunk, then cut with an ellipsis) and rasterised by `scripts/previews.mjs` with `@resvg/resvg-js` (dev dependency, prebuilt binaries for Windows, glibc and musl) and IBM Plex Sans from `@fontsource`, unpacked from WOFF and renamed per subset and weight because resvg's glyph fallback ignores weight. 79 images, about 3.6 MB, a few seconds of build. `checkPages` also fails the build when a public page lacks its preview tags, its image or JSON-LD that parses. Lighthouse SEO stays 100. **Not yet**: the Telegram preview checked for real (needs #134's domain); an online schema.org validator run | `frontend/src/prerender/seo.ts`, `frontend/src/prerender/preview.ts`, `frontend/scripts/previews.mjs` |
| Hydrated public pages, split bundle ([#310](https://github.com/shoraLBRT/ritocode/issues/310)) | `/`, `/problems`, `/problems/{slug}`, `/tasks` and `/privacy` carry the data they were rendered from (`<script type="application/json" id="rc-prerendered">` after `#root`; `site/prerendered.ts`, `prerenderedData` in `prerender.tsx`) and `main.tsx` **hydrates** them (`hydrateRoot`) when that data is for the address and there is no query; a task page, an attempt's address, `404.html` and any query are rendered afresh. The prerender renders the application's own pages (the `Static*` variants are gone). `useApiResource` takes `initial` data: nothing is fetched for the catalogue; `/tasks` refreshes in the background for a learner's statuses. Each page carries only what it shows — never a task's key (`prerender.test.tsx`). Hydration-safe: the theme toggle (`useSyncExternalStore`, light on the server) and the arena's problems to read (the build's day while hydrating, `useHydrated`). The task screen, review, profile and admin pages are `lazy` routes. The page rise animation runs only on a page navigated to (`app--navigated`): on the page landed on it repainted the largest text after the bundle ran. Lighthouse performance (median of 3, guest API from the export, build on `/api/v1`): `/` 99, `/tasks` 100, `/privacy` 100, a card page 99, a task page 80, `/problems` 72; budget 90 with `/problems` 65 and task pages 75 (`EXCEPTIONS`; see Open questions). Checked in a browser at 412 px: no hydration errors on `/`, `/problems`, `/tasks`, `/privacy`; the task screen loads its chunk | `frontend/src/site/prerendered.ts`, `frontend/src/main.tsx`, `frontend/src/routes.tsx`, `frontend/scripts/lighthouse.mjs` |
| Frontend shell ([#26](https://github.com/shoraLBRT/ritocode/issues/26)) | React, Vite and TypeScript; the API client that owns the error envelope; layout, routes, loading, error and empty states. A **translation catalogue** of its own (`src/i18n`: typed dotted keys, `{name}` placeholders, Russian plurals through `Intl.PluralRules`, `<html lang="ru">`), every string moved into it, and an ESLint rule that fails on text written in JSX. The **signed-in state** from `/me` (`src/session`: loading, signed in, signed out on a 401, error) in the header, and `RequireSignIn`, a layout route for pages that need a learner. A **phone-width layout**, checked at 375 px: no horizontal scroll, the header wraps | `frontend/` |
| The arena's header ([#195](https://github.com/shoraLBRT/ritocode/issues/195)) | DESIGN.md › The home page: the brand is the way home (*Главная* is gone), the sections *Задачи · Каталог проблем* (*Прогресс* signed in, *Админка* for an admin), then the **search**, the theme toggle, and the **streak** and **avatar** — or *Войти* and *Начать* (the demo task) for a guest. The search (`components/HeaderSearch.tsx`, ranking in `headerResults.ts` on the task screen's `search.ts`) is a combobox over task titles and problem names, keywords and summaries, five of each; arrow keys, Enter, Escape, `Ctrl K`/`⌘K`; the lists are read on the first focus; a problem opens at its anchor in `/problems`. The streak (`HeaderStreak.tsx`) counts by the home page's rule (`arena.ts`) from `listAttemptsSince`, now shared with the home page, and is read again on every page change, so a submit counts at once. The avatar's menu holds the username and *Выйти*. Both header menus are native disclosures (`.app__menu`). The header's text and the sign-in errors and notice are in «ты». The search steps aside on the task workspace. At phone width the header is one row with a menu sheet and the search an icon over the header, sticky off the workspace, a fuller footer (#263, below). Checked at 1280, 900, 700 and 375 px, light and dark, signed in and out, against a stub API: no horizontal scroll | `frontend/src/components/AppLayout.tsx`, `frontend/src/components/HeaderSearch.tsx`, `frontend/src/components/HeaderStreak.tsx` |
| Catalogue pages ([#27](https://github.com/shoraLBRT/ritocode/issues/27), [#193](https://github.com/shoraLBRT/ritocode/issues/193)) | `/tasks`, in the arena's style since #193 (DESIGN.md › The task list): every task in the arena's task table — status (signed in), title, the time from the difficulty (SPEC §3.4), difficulty — in the API's order, no class tags; in the panel's bar, difficulty tabs and, signed in, *Все / Новые / Начатые / Решённые / Ответ открыт* (#229; a mark per status in the table, `StatusMark`; the home page suggests and picks the daily task among new and tried first), each tab counting what it would show under the other filter. The filters live in the address (`?difficulty=&status=`, an unknown value is ignored, a default leaves the address; the entry is replaced, not pushed), and the home page's *Все задачи* opens it unfiltered. The table (`components/TaskTable.tsx`, with the `Tabs` it filters by) is one component shared by `/` and `/tasks`. Read in one request (`pageSize=100`) and filtered in the page. Checked at 1280 and 375 px, light and dark, signed in and out, against a stub API. `/problems`, as cards since #202 (DESIGN.md › The problem catalogue): grouped by class in the taxonomy's order, a class filter with counts and a search over name, summary and keywords, ignoring case and ё; a folded card shows its class, name and summary (Markdown), and opens as a modal at the centre — grown from its place, none under reduced motion — with every section rendered by `Markdown`, the project's own renderer for the subset cards use, React elements only, no `innerHTML`; ×, Esc or a click outside close it and focus returns to the card. `/problems#<slug>` scrolls to that card and opens it; the address follows the open card. Each card's sections sit in a native `<details>`, so the prerendered page unfolds a card in place without JavaScript (checked). Both checked at 1280 and 375 px | `frontend/src/pages`, `frontend/src/components/Markdown.tsx` |
| Task screen ([#126](https://github.com/shoraLBRT/ritocode/issues/126)) | `/tasks/{slug}`: context and brief (rendered as Markdown), the material — overview, file tree with line counts, the selected file with line numbers and Python highlighting from the project's own tokenizer (`components/python.ts`) — and the answer, side by side on a desktop and three tabs below 64 rem. Step 1: the offered cards by name and summary, grouped by class (`GET /tasks/{slug}` now carries the class names, so the screen never asks for `/problems`), with search; step 2: the whole tree per picked card, branches as disclosure widgets, leaves as checkboxes; back to step 1 keeps the picks; *Check* needs a leaf on every picked card and a signed-in learner. A signed-in learner works in the newest open attempt at the task, or a new one; reaching step 2 is recorded on it. Checking lands on the review. Solved end to end at 1280 and 375 px | `frontend/src/pages/task` |
| Review ([#29](https://github.com/shoraLBRT/ritocode/issues/29), [#229](https://github.com/shoraLBRT/ritocode/issues/229)) | `/tasks/{slug}/attempts/{id}`, owner only. **Before the answer is revealed** (#229): the score, solved or not, *Попробовать снова* first and *Показать ответ* beside it, the code, and on the right the counts drawn small (`OutcomeTally`: a ring of found of present, a row with pips each for found, treated, missed, extra — DESIGN.md › The review); *Показать ответ* asks inline, saying the task becomes «ответ открыт» (or stays solved), then shows the full review; *Не сейчас* gives focus back to *Показать ответ*, and a reveal moves it to the full review's score. Checked in the built-in browser at 1280 and 375 px, light and dark, before and after the reveal, a clean task (0 из 0) included. **Once revealed**: the score in points with its composition (found of present, extra picks, matched treatments); the cards in three lanes — found, missed, not in the answer — each with its treatment leaves marked on its face (#242, `ReviewLanes`); opened, every finding of the key, found or missed, with the learner's leaves beside the author's, the author's note and the full card on expanding; every extra pick with its card; the lesson; the other tasks over the same material; *Try again*. **Next task** (#199), before and after the reveal: *Следующая задача →* beside *Попробовать снова*, to the next task in the order of `GET /tasks` whose status is not *solved*, carrying on from the start past the end, never the task itself, and absent when every other task is solved or the list cannot be read (`pages/task/next.ts`); checked in the built-in browser at 1280 and 375 px, light and dark — the buttons wrap under each other on a phone without overflowing the bar. A clean task says its outcome in words. Found, missed and extra each carry a mark and a word. The notes and the lesson are kept with the attempt on submit (`attempts.review`), so the review matches the key it was scored against | `frontend/src/pages/task/ReviewPage.tsx`, `frontend/src/pages/task/next.ts` |
| End-to-end test ([#39](https://github.com/shoraLBRT/ritocode/issues/39)) | `e2e/`, Playwright with Chromium: a visitor opens the demo task signed out, picks a card and a leaf, presses *Check*, signs in with GitHub, lands on the review of that answer without its answer (the counts, no card named), reveals it after confirming (`Найдено 1 из 2 …`), finds the task marked «ответ открыт» in `/tasks` and the attempt on `/profile?tab=cards`, card by card (#229, #261). Playwright starts three servers: `fake-provider.mjs`, a stand-in for GitHub's OAuth app that sends the browser straight back with a code, checks the PKCE verifier, and makes a new person for every sign-in; `start-api.mjs`, which creates and migrates a database of its own (`ritocode_e2e`) in the compose PostgreSQL, then runs the API in Development with the development identity off and GitHub pointed at the fake through the new `Auth:GitHub:AuthorizationEndpoint`, `TokenEndpoint` and `UserInformationEndpoint` (empty — always in production — means the provider's own); and the production build of the pages under `vite preview`. Texts come from the pages' catalogue. CI job *Learning flow* (`e2e.yml`) on every pull request and push to `main`. Shown to fail at the right step when the provider reports an unverified address | `e2e/`, `.github/workflows/e2e.yml`, `src/Modules/Ritocode.Modules.Auth/SignIn` |
| CI | Backend build, test, formatting, migrations and drift; content validation; frontend lint, build and test; the prerendered pages; the monitor's alert test; the end-to-end learning flow. The *Monitor* workflow is not CI: it checks the production site on a schedule | `.github/workflows/` |
| Release images ([#31](https://github.com/shoraLBRT/ritocode/issues/31)) | On every push to `main`, `ghcr.io/shoralbrt/ritocode-api` (the API, with the migrator at `migrator/Ritocode.DbMigrator.dll`) and `ghcr.io/shoralbrt/ritocode-web` (the static build of #132, served by Caddy on port 80 with its `try_files` rule), tagged with the full commit and `main`, public — [ADR 0011](adr/0011-release-images.md), **Proposed**. Built without pushing on every pull request. The web image takes the site's address from the repository variable `SITE_ORIGIN`, a placeholder until #134 | `deploy/`, `.github/workflows/release-images.yml` |
| Logs ([#33](https://github.com/shoraLBRT/ritocode/issues/33)) | The API logs one JSON object per line (the console's `json` formatter, UTC, scopes included) outside Development. Every line of a request carries its request id — the `X-Request-Id` — and, signed in, the user id (`UserLogScopeMiddleware`, after authentication), nothing else about the person. One summary line per request from ASP.NET's HTTP logging: method, path, status, duration — no headers, query or body — before authorisation, so a refused request is logged too. SQL is quiet in production (`Warning`), verbose in development. Levels from `Logging:LogLevel`. How to find a request's lines, and the rotation #135 must configure: [deploy/README.md](../deploy/README.md). Tests capture the host's log lines (`CapturedLogs`) and find a request's by its id | `src/Ritocode.Shared`, `src/Ritocode.Api/appsettings.json`, `deploy/README.md` |

Removed in #119: the Workspaces, Evaluations, Submissions and Progress modules, the sandbox runner,
`spikes/`. Removed in #121: the Problems module, the old package format and its C# packages, object
storage with MinIO, and the frontend's old problem pages. All of it remains readable at the tag
`pre-diagnosis`.

## Where things stand

**The backlog is on GitHub, not here** ([AGENTS.md](../AGENTS.md) › Where the work comes from):
[milestones](https://github.com/shoraLBRT/ritocode/milestones), native *blocked by* relations, and
the [board](https://github.com/users/shoraLBRT/projects/3). On 2026-11-04 `ROADMAP.md` was retired
and every item left in it, in this section and in SPEC §12 became an issue or a milestone
description. As of that day:

- **S0–S3, S5, S6** are done and their milestones closed.
- **S4, S7, S8** wait on the maintainer's [#134](https://github.com/shoraLBRT/ritocode/issues/134)
  (domain, VPS, OAuth apps, privacy text). Their engineering exists and is checked locally;
  maintainer-provided resources block only an issue's final real check, not its engineering — build
  with fakes, a local `docker compose` or a placeholder, and name the real check as left. The
  CONTENT_FORMAT fix ([#218](https://github.com/shoraLBRT/ritocode/issues/218)), the one takeable
  item there, is done.
- **Arena polish** is what a coding session takes next: the maintainer's feedback of 2026-11-04 on
  the arena and the task screen. Surface contrast
  ([#197](https://github.com/shoraLBRT/ritocode/issues/197)) is built — surface levels in the tokens
  (DESIGN.md › Tokens), applied to the arena and the workspace, AA checked by
  `src/theme/contrast.test.ts` — the maintainer approved its before/after on 2026-11-07, which
  unblocks #198, #200, #202 and #203. The workspace is layout A with a
  rising dock of cards since #198 (DESIGN.md › The task workspace). **#199 is done** (2026-11-12): the score and
  *Попробовать снова* at the top of the review came with #198, the header's sections are on the
  workspace (checked at 1280, 900 and 375 px, light and dark), and *Следующая задача →* sits beside
  trying again — the next unsolved task in the `/tasks` order, wrapping past the end, absent when
  every other task is solved (DESIGN.md › The review). The catalogue is cards since #202; progress
  waits on its mock-up (#203). The arena's hero carries the maintainer's copy since #216 (variant C,
  «Код работает. Что с ним не так?»; checked at 1280 and 375 px); the page's meta title and
  description still carry the old wording ([#232](https://github.com/shoraLBRT/ritocode/issues/232)).
  **#229 is done** (2026-11-12): the result without the answer, *Показать ответ* and the four
  statuses — API, pages, tests, e2e ([ADR 0017](adr/0017-a-result-without-the-answer.md)) — and
  their visual check in the built-in browser at 1280 and 375 px, light and dark, which fixed the
  buttons overflowing the score bar on a phone, the underline on a link drawn as a button, and the
  focus lost on *Не сейчас* and after the reveal. **#235 is done** (2026-11-12): per-card progress
  leaves out the tasks whose answer is not revealed, and `/progress` says how many; the per-class
  counts still take every first attempt, which narrows an unrevealed key (Open questions).
  **#243** (2026-11-14, PR open): the feedback widget and `POST /api/v1/feedback`, built and
  tested with a fake tracker. Left: the real check — an issue in this repository sent from the
  deployed site, which needs #134/#135 and shows whether the Russian VPS reaches `api.github.com` and
  Turnstile (ADR 0019). Since 2026-11-18 the release workflow passes the repository variable
  `TURNSTILE_SITE_KEY` to the web image as `VITE_TURNSTILE_SITE_KEY` (the maintainer allowed the
  protected-path change); the captcha is on in production once that variable and
  `TURNSTILE_SECRET_KEY` are set.
  **#241** (2026-11-14, PR open): on the treatment step the finding being treated is the dock's
  subject — a card in its class colour with the name large and a line saying what the step asks.
  Lint, build and vitest pass; the browser check in both themes at 1280/375 px was not done yet.
  **#239** (2026-11-14, PR open): the dock rises **with** the scroll on both steps — its lift grows
  and shrinks by the wheel, trackpad or finger delta (`followScroll` in `dock.ts`), and only then do
  the results scroll; the grip raises or lowers it all the way, eased over a few frames unless the
  reader asked for reduced motion. Checked in the built-in browser at 1440 px (both steps) and
  375 px (the sheet) with wheel input; that browser reports reduced motion, so the eased frames
  were not seen there, and a real trackpad and touch were not tried — the maintainer's hand check
  of the issue is left.
  **#240** (2026-11-14, PR open): every dock on both steps can be hidden down to a bar
  (*Скрыть* / *Показать* in `DockBar`, `collapsed` in `useRisingDock`) so the whole file can be
  read with a filter on; the search, the filters, the picks, the lift and the results' scroll are
  kept. Checked in the built-in browser at 1280 px (both steps, a class or branch filter and a
  search on, the dock raised) and 375 px (the sheet shrinks to the chips and the bar).
  **#285** (2026-11-18, PR open): the feedback issues are opened as a GitHub App when
  `Feedback:GitHubApp:AppId` and `:PrivateKey` are set (`GitHubAppCredential`: an RS256 JWT, the
  repository's installation, an installation token cached until five minutes before it expires);
  the personal token stays the alternative (ADR 0019). Tested against a fake GitHub; the maintainer
  installs the Transliterator's App on this repository and sets `FEEDBACK_GITHUB_APP_ID` and
  `FEEDBACK_GITHUB_APP_PRIVATE_KEY` (`deploy/README.md`). The real check stays with #243.
  **#232** (2026-11-18): the home page's meta title and description (`meta.home`) follow the
  hero — «Ritocode — тренажёр ревью кода» and «Агент получил задание и собрал рабочий прототип. Твоя
  задача — быстрое ревью: найти в нём high-level проблемы архитектуры и выбрать, как их лечить.»,
  the maintainer's choice; «на настоящих проектах» is gone (SPEC §3.4: the material is generated to
  order). Lint, build, `build:static` (the prerendered `/` carries both) and vitest (475) pass; not
  checked in a browser.
  **#252** (2026-11-17): the unsolved result's line (`review.notSolved`) is now a short offer —
  «Пока не решена. Попробуй снова — или покажи ответ.» — matching the *Попробовать снова* and
  *Показать ответ* buttons, without the sentence about what will not be told. Lint, build and
  vitest (475) pass; not checked in a browser.
  **#268** (2026-11-17): `GET /me` also carries `createdAt` (the join date, UTC; `UserSummary` gained
  it) and `providers` (`github`, `google`; empty for the development identity), and the profile's
  person card shows «На Ritocode с <месяца года>» — the month in the genitive, from `Intl` with the
  day dropped (`monthAndYear` in `pages/profile/profile.ts`) — and the providers as outlined chips.
  No schema change. API tests for a new user, a user with two providers, the development identity
  and the 401; build, format, dotnet tests, lint, build and vitest (475) pass. Not checked in a
  browser.
  **#262** (2026-11-17): the admin area in the arena's style — a page head and section tabs
  (*Обзор · Сигналы · Пользователи · Попытки*, the open signals counted on *Сигналы*), an **overview**
  at `/admin` (four count tiles read as `totalItems`, the five newest open signals), each list one
  `rc-panel` with segmented-tab filters and chips for states, no sideways scroll at 1280 px and up;
  the page as DESIGN.md › The admin area describes it, no new endpoint. Checked in the built-in
  browser at 1440×900 and 375×812, light and dark, against the development API (no horizontal page
  scroll; reduced motion, so final states only); lint, build, build:static and vitest (437) pass.
  **#265** (2026-11-17): the home page is livelier — the hero's counts count up, the cards enter 40 ms
  apart, the progress ring sweeps and its bars fill, the profile is one link away (*Весь профиль →*,
  the activity heading), each activity cell names its day and count and today is outlined, with a
  legend; task rows are whole targets with a chevron (`TaskTable`, so `/tasks` too); the daily card
  has a soft highlight, a line about what a task is and a bottom button; a guest sees a drawn
  preview of the progress. The top cards share a height at 1280 px and up, the aside is 320 px.
  Checked in the built-in browser at 1440, 1920 (layout) and 375 px, light and dark, signed in
  (reduced motion, so final states only; the guest card by tests only, the dev API always signs in);
  lint, build, build:static and vitest (460) pass.
  **#266** (2026-11-17): the two catalogues as list pages. `/tasks` opens with a **summary strip**
  (`components/SolvedSummary.tsx`, counting with the profile's `solvedTasks`): solved of all counting
  up, a three-part bar (a segment per difficulty, as wide as its share of the tasks, filled by its
  solved share), the started and revealed counts — every label a button that sets its filter in the
  address — and for a guest the counts by difficulty and an invitation to sign in. `Tabs` has one
  moving **thumb** (measured offsets, follows a wrapped second line, none until measured); the list
  re-enters on a filter change and its count eases (`useCountUp`). The filter bars of `/tasks` and
  `/problems` stick under the header (`useStuck` marks `data-stuck` for the shadow); **on a phone
  (<= 48 rem) they do not stick** — a deliberate choice, two lines of filters would cover too much.
  `/problems`: a class heading shows its count («3 из 9» under a search), the found count eases, the
  search field has a clear button. `BackToTop` (`/tasks`, `/problems`, the profile) after a screen of
  scroll. Checked in the built-in browser at 1440 and 375 px, light and dark (reduced motion, so
  final states only): bars stuck, a card opened from a scrolled position (centred, over the stuck
  bar), a wrapped tab row, no horizontal scroll; lint, build, build:static (prerendered `/problems`
  keeps every card and the counts) and vitest (469) pass.
  **#267** (2026-11-17): motion in the task workspace and the review, no change of behaviour or
  layout. The step bar's pills transition their state and a finished step's number pops into a check
  (`rc-check-pop`); a step's aside and dock ease in (`rc-step-in`, opacity and 6 px) — the steps
  are different components, so a pick never replays it. A pick answers at once: the control's label
  springs, the card's ring pulses (`rc-pick-ring`), a leaf's box checks and its row
  flashes; the picked row and chips enter with `rc-pop` (they leave without an exit animation); the
  counters count up over 200 ms (`useCountUp`). Dock cards lift 2 px and sink on a press; when the
  matches change the results fade (`pages/task/motion.ts` `useFadeOnChange`, the Web Animations API,
  no remount, so focus and `dock.ts` are untouched), and `CardDialog` fades its face on ‹ ›. The
  dialog and its backdrop use the tokens. The review: the score counts up, the tally's ring sweeps
  (`@property --rc-tally-sweep`), the pips fill 30 ms apart, a solved result gets a check badge that
  pops once with one soft ring (`.review__solved`), the lanes come in 60 ms apart and their cards
  30 ms apart (capped at 400 ms), the reveal confirmation rises. *Проверить*, the reveal's confirm and
  the signal's *Отправить* show work in flight with `.button--busy` + `aria-busy` and hold their
  width (`useHeldWidth`). At phone width a tab switch fades the area in; the sheet's height stays
  `dock.ts`'s. Checked in the built-in browser (reduced motion, so final states only): see the
  session report; lint, build, build:static and vitest (473) pass.
  **#264** (2026-11-17): states — loading is skeletons of the final shape (task table, catalogue
  cards, the home page's daily task, stats, progress, heat map and problems-to-read, admin tables)
  inside `LoadingState`'s `role="status"` region, shown after 150 ms; `EmptyState` is a mark, a
  title, a line and one action (*Сбросить фильтры* on `/tasks`, *Сбросить поиск* on `/problems`);
  `ErrorState` is the same frame with *Повторить* in flight; `NotFoundPage` is a large 404 with the
  brand's cross and three ways out; the workspace's loading is unchanged; DESIGN.md › States.
  Checked in the built-in browser at 1440×900 and 375×812, light and dark, with the API held back
  (skeleton heights within a few pixels of the loaded content; the row counts differ by data) and
  with it unreachable; lint, build, build:static and vitest (456) pass.
  **#263** (2026-11-17): the header is one row of at most 64 px on a phone (brand, search icon,
  streak and avatar or *Войти*, menu button; the sections, the theme and *Начать* in a sheet that
  closes on Escape, a tap outside and navigation, with focus kept), sticky with a blur and a shadow
  once scrolled everywhere but the workspace, with `--header-height` (the profile's sticky column
  offsets by it) and `scroll-padding-top`; the current section is underlined; the footer has the
  brand, the sections, privacy and *Обратная связь* (opens the feedback window). Checked in the
  built-in browser at 1440, 900, 770 and 375 px, light and dark, against the development API (always
  signed in: the signed-out header only by tests; no horizontal page scroll; the header 64 px at
  375; reduced motion, so final states only); lint, build, build:static and vitest (448) pass.
  **#261** (2026-11-17): the profile — `/profile` replaces `/progress` (which redirects), *Прогресс*
  leaves the header's sections, the avatar's menu gains *Профиль* and the streak links there; the
  page as DESIGN.md › The profile describes it, from existing data only. Checked in the built-in
  browser at 1440×900 and 375×812, light and dark, against the development API (no horizontal page
  scroll; reduced motion, so final states only); lint, build, build:static and vitest (426) pass.
  **#260** (2026-11-17): the motion system — tokens `--dur-fast/--dur/--dur-slow` and
  `--ease-out/--ease-in-out/--ease-spring`, one global reduced-motion guard (the three per-component
  blocks are gone), one `:focus-visible` ring, hover and press states on buttons, nav links, tabs,
  chips, table rows, link cards and the header menu, the `rc-rise` page entrance and `rc-pop` popups
  (header menus, search list, feedback window), and two primitives: `Skeleton` (`.rc-skeleton`) and
  `useCountUp`. Checked in the built-in browser on `/tasks`, a task page and `/problems` (no horizontal
  scroll at 375 px; the browser reports reduced motion, so final states only); lint, build, build:static and vitest (411) pass.
  **#258** (2026-11-17): the review opens a card the way *Диагноз* does — one `CardDialog` shared by
  both, over the whole centre column, ‹ n из N › and ← → through the lanes, ×/Esc, focus returned —
  with *В этой задаче* first and the catalogue's sections folded; *← Все карточки* is gone. Checked in
  the built-in browser at 1440 px (dark; a section unfolded, the walk) and 375 px (full screen), and
  *Диагноз*'s opened card unchanged; lint, build and vitest (406) pass.
  **#256** (2026-11-16): a lane card's face is its name and one conclusion — on a found card whether
  the treatment fits, on a missed one the author's first leaf, on an extra pick «Этого в коде нет» —
  and the whole card opens it in full; the class, the summary, the leaf chips and the points left the
  face. Checked in the built-in browser at 1440 px (light and dark, a card opened and closed) and
  375 px (no overflow); lint, build and vitest (404) pass.
  **#253, #254** (2026-11-16, maintainer's feedback on #242): on the review the verdict owns the
  colour — each lane tinted with its verdict under a solid badge, the cards in it neutral, the class a
  grey word — and on a desktop the cards pane scrolls on its own, so a long lane is read in full.
  Checked in the built-in browser at 1440 px, light and dark; lint, build and vitest (402) pass.
  **#242** (2026-11-15, PR open): the revealed review's centre is the task's cards in three lanes —
  found, missed, not in the answer (variant B, the maintainer's choice) — each card with its
  treatment leaves marked on its face (✓ fits, + not in the key, ○ the author's not chosen, a
  neutral dashed chip as recommended); *Подробнее* opens the card in full in their place
  (`ReviewLanes`, `lanes.ts`; DESIGN.md › The review, SPEC §4.5). The right column holds the counts
  and the lesson; the verdict list it had is gone. Lint, build, vitest and build:static pass; the
  browser check at 1280 and 375 px, light and dark, was not done (the usage window ran short).
- **Studying the problems** ([#204](https://github.com/shoraLBRT/ritocode/issues/204)): [ADR 0018](adr/0018-studying-problems.md)
  is *Accepted* (2026-11-18) with option A — the mark «Изучено» on `/problems`, a guest's marks in
  the browser only, a study section with a chart on the profile, marks counting in the streak and
  activity, kept apart from task mastery. CONCEPT §1 and SPEC §4.2/§4.7 say so.
  **#280** (2026-11-19, merged, PR #290): the mark, its API and `/problems` (What exists › Study
  marks). **#281** (2026-11-19, PR open): the profile's *Изучение* with its chart, and marks in the
  streak and activity on the profile, the home page and the header — frontend only, no API change;
  checked signed in in the built-in browser at 1280 and 375 px, light and dark, with marks over
  three weeks and with none. With it ADR 0018's MVP part is built; the drill and snippets wait in
  *Later* (#282, #283).
  **#292** (2026-11-19, PR #307 open): on `/problems` a class's two counts (`rc-catalogue__class-meta`)
  take the line under its name below 30rem instead of the study count wrapping alone; and a
  `#slug` the browser itself sets after a card was closed (a typed address, a plain `#slug` link)
  opens the card again — such hash changes all have the router key `default` and can repeat the
  same location, so the page also counts `hashchange`. Lint, build and vitest (498) pass; checked in
  the built-in browser at 375 and 1280 px, light and dark, against the exported catalogue (the header
  search was not, it needs the API).
- **Operations** (#293–#306): **#300** (2026-11-19, part, PR open) — real 404s, canonical redirects,
  the sitemap's `lastmod` and `npm run lighthouse` are built and checked against the web image
  locally; the CI job that runs the budget is a second PR (`claude/300-lighthouse-ci`), on the
  protected `.github/workflows/`, which also passes `CONTENT_LASTMOD` to the release build. Until the
  owner merges it the issue stays open, and released images have no `lastmod`.
  **#297** (2026-11-19, PR open): the admin system page, `/admin/system` from
  `GET /api/v1/admin/system` (What exists › Admin system page). The images take the commit as the
  build argument `SOURCE_COMMIT`; passing it from the release workflow is a second PR
  (`claude/297-version-ci`, protected `.github/workflows/`). Until the owner merges it the page shows
  both commits as «неизвестен» in production — everything else works without it.
  **#293** (2026-11-22): [ADR 0020](adr/0020-observability.md) **Accepted** by the maintainer as
  written — errors in the product's PostgreSQL on `/admin/errors`, Dozzle behind Caddy for logs,
  threshold checks in `deploy/check.sh`, alerts as the `monitoring-alert` issue plus Telegram. Its
  assumptions stand: a 2 GB RAM / 30 GB VPS, 14 days of logs, 30 of error occurrences. #294, #295 and
  #296 build on it.
  **#301** (2026-11-22, PR open): a prerendered page per problem card and per task, and `/tasks`
  prerendered (What exists › A page per card and per task). SPEC §4.1 names the new public address
  `/problems/{slug}`; the issue asked for §6, which is accounts and administration, so the routes
  table of §4.1 took it.
  **#302** (2026-11-22, PR open, partial): link previews and structured data on every prerendered
  public page (What exists › Link previews and structured data). Left open on the issue: the real
  Telegram preview, which needs the domain (#134); the JSON-LD was checked structurally by tests, not
  with an online schema.org validator.
  **#305** (2026-11-22, PR open): per-task and per-card statistics on `/admin/stats` from
  `GET /api/v1/admin/stats/tasks` and `/cards` (What exists › Admin statistics per task and card).
  The definitions left open by the issue — solved as the task list's status, time over first attempts
  only, quartiles by linear interpolation, *rarely solved* as ≥ 5 learners and < 20 % — are recorded
  there; change them if the calibration (#137) wants others.
  **#294** (2026-11-22, PR open): server exceptions and browser errors grouped in the `errors` schema,
  listed on `/admin/errors`, a new or reopened group sent to Telegram (What exists › Error reporting).
  The real check — a message from the VPS — needs `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` there (#134).
  **#306** (2026-11-22, PR open): the site's traffic from Umami on `/admin` from
  `GET /api/v1/admin/stats/traffic` (What exists › Admin traffic from Umami). The funnel is event
  counts, not a per-visitor funnel; the panel has its own range picker. The real check on the VPS
  needs `UMAMI_API_TOKEN` and `UMAMI_WEBSITE_ID` set there (#134).
  **#304** (2026-11-19, PR open): usage over time on `/admin` from
  `GET /api/v1/admin/stats/activity` (What exists › Admin usage over time). Checked in the browser at
  1280 px light and dark and 375 px dark on the shared dev data; a range with nothing in it is
  covered by the API test, not seen in the browser.
  **#299** (2026-11-22, PR open): data export and account deletion (What exists › Data export and
  account deletion), with [ADR 0021](adr/0021-personal-data-export-and-erasure.md) **Accepted** (2026-11-22) for
  the seam. Left for the maintainer: the
  off-server bucket's 7-day lifecycle rule is set at the provider (#134), and the policy's own text
  (#128/#134) keeps the promise the placeholder now makes.
  **#298** (2026-11-22, PR open): the maintenance page, the switch and the banner (What exists ›
  Maintenance page). Checked on the local production stack, not yet on the server of #134.
  **#295** (2026-11-22, PR open, partial): logs kept for 14 days or the cap and read in Dozzle at
  `/admin/logs/` behind the admin session (What exists › Logs kept and read without SSH). Left open on
  the issue: the acceptance run on the local production stack (Docker was unavailable to the session)
  and the same on the server of #134.
  **#296** (2026-11-22, PR open): the server's checks and their Telegram alert (What exists › Server
  checks). Checked on the local production stack, not yet on the server of #134. Left for the
  owner: the *Monitor* workflow passing the `TELEGRAM_*` secrets to its *Alert* step (a protected
  path), and the bot itself.
- **Later** holds the post-MVP iterations that were SPEC §12; not taken until the maintainer moves one.
  Since 2026-11-11 it also holds the **incident**
  ([#227](https://github.com/shoraLBRT/ritocode/issues/227),
  [ADR 0016](adr/0016-incident-boss-task.md)): a boss task over a whole production stack, in a fixed
  pool of systems, recorded rather than live; three worlds of three or four incidents each to
  start, in Python and Java. Specified in [SPEC](SPEC.md) §14; the maintainer confirmed its defaults on
  2026-11-12 and changed the balance (§14.10: every attempt counts, opening a boss spends its
  price). The economy's numbers (hint costs, evidence points, prices) are balanced last. Nothing
  is built.

**Content.** The content track, with the two skills: 55–60 cards
([#124](https://github.com/shoraLBRT/ritocode/issues/124)) and the 20 tasks
([#42](https://github.com/shoraLBRT/ritocode/issues/42)). On 2026-10-01 the maintainer handed
the catalogue to a `session-full` run, which drafted all 56 cards and merged them on green CI;
the maintainer edits them from here. The same run then took the tasks of #42; they exist —
the demo task, `team-reminders-for-myself` and `team-reminders-for-support` (easy, over
`team-reminders`), `clinic-booking-city-network` and `clinic-booking-dentist` (medium, over
`clinic-booking`), `price-sync-pet-shop` and `price-sync-coursework` (medium, over `price-sync`),
`newsletter-signup-bakery` and `newsletter-signup-agency-kit` (easy, over `newsletter-signup`),
`contact-dedupe-ngo` (easy, clean — no findings), `orders-report-small-shop` and
`orders-report-marketplace` (easy, over `orders-report`), `thumbnail-maker-marketplace` and
`thumbnail-maker-own-photos` (easy, over `thumbnail-maker`), `payment-webhook-online-school` and
`payment-webhook-hackathon` (medium, over `payment-webhook`), `subscription-billing-saas` and
`subscription-billing-climbing-gym` (medium, over `subscription-billing`), `invoice-builder-studio`
and `invoice-builder-last-run` (hard, over `invoice-builder`) — 20 of 20. Each new one passed a
blind smoke test, reported in its pull request. The keys use 34 distinct cards (SPEC §3.2 aims at
25–35); SPEC's "at least twice" holds for all but four, each in one key: `secrets-in-repo` and
`money-in-float` (the demo task), `over-engineering` (a verdict card, absent where the layers are
justified) and `naive-calendar-math` (absent where 30 days is the rule).

**The arena.** On the maintainer's request, outside the stages: the look and the task workspace of
[#188](https://github.com/shoraLBRT/ritocode/issues/188) ([DESIGN.md](DESIGN.md)). On 2026-10-28 the
maintainer chose the home page — an **arena** in the manner of LeetCode — and changed the audience to
junior developers who build with AI agents ([ADR 0015](adr/0015-arena-home-and-junior-audience.md);
CONCEPT §2, SPEC §2.1). Built on its tokens and `rc-` classes: `/`
([#190](https://github.com/shoraLBRT/ritocode/issues/190)), `/tasks`
([#193](https://github.com/shoraLBRT/ritocode/issues/193)) and the header with its search, streak and
avatar ([#195](https://github.com/shoraLBRT/ritocode/issues/195)). DESIGN.md lays out `/problems` and `/profile` too.

## Open questions

Decisions the specification left open are listed in [SPEC.md](SPEC.md) §13. Add here anything a
future session would otherwise have to rediscover.

- **What the two catalogues are called** (the maintainer, 2026-11-04): «Задачи» beside «Каталог
  проблем» feels wrong — perhaps «Каталог задач» and «Каталог проблем», perhaps something else. Not
  decided; the header, the home page's widgets (#200) and the page titles follow whatever is chosen.

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
- **The header reads the attempts on every page change** (#195), one `GET /attempts` page for a
  learner with under a hundred attempts in twelve weeks, so the streak counts a submit as soon as the
  review opens. If that ever shows in the logs, read it again only after a submit instead. On `/` the
  activity card reads the same list once more.
- **A worktree's `.env` may name another port than the shared compose PostgreSQL publishes**: the
  container keeps the port of the `.env` it was created with. The end-to-end test then fails to
  connect; `E2E_DATABASE_CONNECTION_STRING` points it at the right one without touching `.env`.
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
- **Serving the static build** (#132, #300): try the path, then the path with `.html` — Caddy
  `try_files {path} {path}.html` — and answer 404 with `404.html` for anything else; the build writes
  a file for every address (frontend/README.md › The prerendered pages), so a new route goes into
  `SHELL_PATHS`. `index.html` is the rendered landing, so it must not be the fallback for other routes. `SITE_ORIGIN` is the production address; CI uses
  a placeholder until the domain exists (#134).
- **`/problems` is under the performance budget of 90** (#310): its first paint waits for the whole
  document — every card in full, about 64 kB compressed, plus the catalogue again as the hydration
  data, about 50 kB more — 3.8 s on Lighthouse's simulated phone network, and the class tabs move as
  the fonts arrive (cumulative layout shift 0.14). The budget holds it where it stands (`EXCEPTIONS`
  in `scripts/lighthouse.mjs`). Reaching 90 needs a smaller page: the card sections out of
  `/problems` (every card has its own page since #301, but SPEC §4.1 asks for the catalogue in full
  for search engines — the maintainer's call), the hydration data as a file of its own fetched
  before hydrating, and the tabs' fonts preloaded.
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
  `content/`, and cannot be started. Since #229 the catalogue's status is new / tried / solved /
  gave up (ADR 0017); the admin's "tasks solved" still counts tasks with a submitted attempt.
- **Per-class progress still narrows an unrevealed key** (#235): the per-card table waits for the
  reveal, as the maintainer decided, but the classes count every first attempt, as the issue
  recommended. No class names a card (nine or ten cards each), yet against the learner's own picks
  they say which classes an unrevealed key holds, how many findings in each, and — where the picks
  fall in different classes — which picks were found; the classes minus the table are exactly the
  unrevealed tasks' share. Whether the classes (and the home page's ring, which sums them) should
  wait for the reveal too is the maintainer's call; filtering them is a one-line change in
  `ProgressCalculator` ([ADR 0017](adr/0017-a-result-without-the-answer.md) › Consequences).
- **The counts carry what they carry** (#229): before the reveal a clean task reads «0 из 0» and a
  score's maximum says how much the key holds; that is what the issue's counts are.
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
- **A card that is fine here is still in the key** (SPEC §3.3, `CONTENT_FORMAT.md` §6, the
  `author-task` skill, the smoke prompt): a present problem is listed with an `accept.*` leaf. §6's
  example lists its hardcoded SMTP host with `accept.fits-context` since #218. The test fixture
  `ContentFixtures/reference` began as a copy of that example and keeps the older key, without
  `hardcoded-config`: the API tests use the card as the easy task's extra pick. It is test data, not
  an example to copy.
- A module's test context should be configured as the host configures one. The host's contexts
  retry transient failures, and a retrying strategy refuses a transaction opened by hand — the smoke
  run of #121 caught exactly that, which a test context without retries had passed.

---

## Verification

Run from the repository root. All of these must be clean before opening a PR.

**A Docker daemon has to be running.** Tests that need PostgreSQL start their own container through
Testcontainers. Without Docker, `dotnet test` fails in a way that looks like a code problem.
Right after Docker Desktop starts, its engine refuses every call with `500 Internal Server Error`
for up to a minute; the harness waits up to two minutes for it to answer, so a run started in that
window is slower, not red ([#315](https://github.com/shoraLBRT/ritocode/issues/315)). If the first
full run still fails while a second passes, keep the failures (`--logger trx`) and add them to #315
— the cause seen in #280 and #281 was never captured.

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

The Lighthouse budget of those pages (#300, #310), on a build that addresses the API on its own
origin, as the web image's does (in Git Bash prefix `MSYS_NO_PATHCONV=1`, or `/api/v1` becomes a
Windows path), then measured against the API as a guest finds it, answered from the export:

```bash
cd frontend && VITE_API_BASE_URL=/api/v1 CONTENT_EXPORT=../content-export.json SITE_ORIGIN=https://ritocode.example npm run build:static && CONTENT_EXPORT=../content-export.json npm run lighthouse
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
| `Ritocode.Architecture.Tests` | 16 |
| `Ritocode.Shared.Tests` | 58 |
| `Ritocode.Api.Tests` | 278 |
| `Ritocode.Modules.Content.Tests` | 78 |
| `Ritocode.Modules.Attempts.Tests` | 31 |
| Frontend (vitest) | 541 |
| `scripts/monitor-test.sh` | 13, then `deploy/check-test.sh` |
| `deploy/check-test.sh` (also run by `DeployScriptTests` in `Ritocode.Api.Tests`) | 26 |
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
| `GET /api/v1/me` | `200`, `{"id":"0199aa00-…","username":"developer","admin":true,"createdAt":"…+00:00","providers":[]}` under the development identity (an admin in Development, linked to no provider); signed in through a provider, `providers` lists it (`["github","google"]`, in that order); `401 unauthenticated` without it |
| `POST /auth/logout` | `204`, two `Set-Cookie` headers clearing `__Host-ritocode-session` and `__Host-ritocode-csrf` |
| `GET /auth/login/github?returnUrl=/tasks` | `302` to GitHub with `state` and `code_challenge` when `Auth:GitHub` is configured; `404 provider_not_found` when it is not; `400` with `errors.returnUrl` for `returnUrl=https://…` |
| `GET /api/v1/problems` | `200`, `{ classes, cards }` — the six classes once content is seeded |
| `GET /api/v1/treatments` | `200`, five branches, leaves as `branch.leaf` |
| `GET /api/v1/tasks?pageSize=1000` | `400`, `code: "validation_failed"`, `errors.pageSize` present |
| `GET /api/v1/tasks/no-such-task` | `404`, `code: "task_not_found"` |
| `POST /api/v1/attempts` with `{"task":"<a seeded task>"}` | `201`, `Location` set, `step: "diagnosis"`, `result: null` |
| `POST /api/v1/attempts/{id}/submit` with `{"picks":[]}` | `200`, `outcome` with `total`, `maximum` and the counts; `result: null` and `revealed: false` until the answer is revealed |
| `POST /api/v1/attempts/{id}/reveal` | `200`, `revealed: true`, `result` with a line per card; an open attempt → `409 attempt_not_submitted` |
| `GET /api/v1/tasks` after a submit | `200`, that task has `status: "tried"` (`"solved"` if the answer solved it); after a reveal `"gaveUp"`, unless solved before |
| `POST /api/v1/signals` with `{"attempt":"<a submitted attempt>","card":"<one of its extra picks>"}` | `201`, the signal; again → `409`, `code: "signal_already_sent"`; a found card → `400`, `errors.card` |
| `POST /api/v1/signals` with a body that is not JSON (or a comment in cp1251 bytes) | `400`, `code: "request_invalid"`, logged at Information, not as an unhandled exception |
| `POST /api/v1/signals` with `Content-Type: text/plain` | `415`, `code: "unsupported_media_type"` |
| `GET /api/v1/me/export` | `200`, `Content-Disposition: attachment; filename="ritocode-export-<date>.json"`, `{ exportedAt, user, modules: { users, attempts, auth } }` |
| `DELETE /api/v1/me` with `{"username":"<another>"}` | `400`, `code: "validation_failed"`, `errors.username`; nothing erased. With the caller's username → `204`, the session cookies cleared, then `GET /api/v1/me` → `401` (in Development the development identity is gone until the host restarts and seeds it again) |
| `GET /api/v1/signals` | `405`, `code: "method_not_allowed"`, `Allow: POST` |
| `POST /api/v1/feedback` with `{"message":"hello","page":"/tasks"}` | `201`, `{"number":1}` and the issue in the log (`Feedback:Sink: Log` in Development); `{"message":""}` → `400 feedback_empty`; the sixth within an hour → `429 feedback_rate_limited`; outside Development with no token → `503 feedback_not_configured` |
| `GET /api/v1/me/progress` | `200`, `{ tasks, notRevealed, classes, cards }` — six classes, cards only once met or picked in a task whose answer is revealed, each class and card with its `name`; a submitted task not revealed raises `notRevealed` and names no card |
| `GET /api/v1/admin/signals` | `200`, a page of open signals, each with `cardName` and `learner.email`; `?status=resolved` the resolved ones; `?status=x` → `400`, `errors.status` |
| `POST /api/v1/admin/signals/{id}/resolve` | `200`, the signal with `resolvedAt`; again → the same `resolvedAt`; an unknown id → `404 signal_not_found` |
| `GET /api/v1/admin/users` | `200`, a page of users, newest first, each with `providers`, `attempts`, `tasksSolved` |
| `GET /api/v1/admin/attempts?status=open` | `200`, a page of attempts never submitted, each with the `step` it stopped at |
| `GET /api/v1/admin/stats/tasks` | `200`, `{"tasks":[…]}`, every task with `band`, `solvedShare`, `time`, `open`, `rarelySolved` |
| `GET /api/v1/admin/stats/cards` | `200`, `{"firstAttempts":…,"cards":[…]}`, each card with `met`, `found`, `missed`, `pickedWhenAbsent`, `treatedRight` |
| `GET /api/v1/admin/stats/traffic` | `200`, `{"status":"notConfigured",…,"summary":null}` without `Traffic:*`; with a reachable Umami, `"ok"` and `summary`, `referrers`, `pages`, `funnel` |
| `POST /api/v1/client-errors` with `{"type":"Error","message":"x","page":"/"}` | `202`, no body, an `X-Request-Id`; the 11th in a minute from one address `429 client_errors_rate_limited`; over 16 KiB `413 request_too_large` |
| `GET /api/v1/admin/errors` | `200`, a page of error groups, the most recently seen first, each with `count`, `firstSeenAt`, `lastSeenAt`, `latestRequestIds` |
| any admin address, for a non-admin | `404`, `code: "not_found"`, as `GET /api/v1/no-such-thing` |
| any response | carries an `X-Request-Id` header, and `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Referrer-Policy: no-referrer`, `Cache-Control` with `no-store` |
