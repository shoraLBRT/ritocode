# Project State

**The living state of the project: what exists, what is next, and how to verify a change.** Update it
in the same pull request as the work it describes — a session that skips this makes the next one
start from nothing.

- **Last updated:** 2026-09-30
- **Current stage:** S3 · Trainer — S2 closed with #123: a card made with `author-card` and a task
  made with `author-task` pass validation, and the task has passed a blind smoke test. The content
  track of S2 ([#124](https://github.com/shoraLBRT/ritocode/issues/124),
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
| API conventions | [ADR 0003](adr/0003-api-conventions.md): RFC 9457 errors, pagination envelope, validation filter | `src/Ritocode.Shared` |
| Persistence | PostgreSQL, EF Core per module, one schema each, migrations applied by `Ritocode.DbMigrator`, drift check in CI ([ADR 0004](adr/0004-persistence-and-migrations.md)) | `src/Ritocode.DbMigrator`, `scripts/` |
| Test harness | One PostgreSQL container per test assembly, one migrated database per test class | `tests/Ritocode.TestSupport` |
| Identity seam | `ICurrentUser`, a real authentication scheme with a seeded development identity, authenticated by default ([ADR 0008](adr/0008-authentication-seam.md)). `GET /api/v1/me` answers the caller (id, username) or 401 — it reads `ICurrentUser`, so #6's sessions change nothing in it. No real sign-in yet | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth` |
| Users | The `users` table and `IUserLookup`. `xp` and `trust_level` removed in #119 | `src/Modules/Ritocode.Modules.Users` |
| Ownership rule | An architecture test reading compiled IL: a user's rows are reached only where the owner is in the query. It guards the Attempts module's context; the allowances are `OwnedAttempts` (every lookup by owner) and the creation in `AttemptLifecycle.StartAsync`. Its reader is proved against a test-only context | `tests/Ritocode.Architecture.Tests/OwnershipRuleTests.cs` |
| Content ([#120](https://github.com/shoraLBRT/ritocode/issues/120), [#121](https://github.com/shoraLBRT/ritocode/issues/121)) | The format of [CONTENT_FORMAT.md](CONTENT_FORMAT.md) parsed and validated — every rule of §7 tested — and `content validate` in CI (job *Validate content*). The `content` schema — taxonomy, cards, materials, tasks — and an ingest that validates first, writes in one transaction stamped with the commit, upserts by slug, retires cards and unpublishes tasks that left `content/`, and derives the material overview and the easy-task shortlist. A development host seeds `content/` on start. The public reads of SPEC §9.3 ([#9](https://github.com/shoraLBRT/ritocode/issues/9)): `GET /problems` (every live card in full, with the classes), `GET /treatments`, `GET /tasks` (a page, easy first) and `GET /tasks/{slug}` (context, brief, material with its overview, the cards to pick from — name, summary and keywords only, the shortlist for an easy task — and the other tasks over the same material). No answer key and no card weight leave the server; a test serialises a task and looks for them | `src/Modules/Ritocode.Modules.Content`, `src/Ritocode.ContentTool`, `content/` |
| Authoring ([#122](https://github.com/shoraLBRT/ritocode/issues/122), [#123](https://github.com/shoraLBRT/ritocode/issues/123)) | The `author-card` skill: drafts a card from a name, reading the live catalogue so the summary is delimited from its neighbours; checks it with `content validate`; never overwrites a card. Three cards drafted with it — `secrets-in-repo`, `money-in-float`, `god-class` — open the catalogue of [#124](https://github.com/shoraLBRT/ritocode/issues/124). The `author-task` skill: writes a material and one task per context from the maintainer's idea, validates, and runs the **blind smoke test** — `content learner-view <task>` renders the task as the task screen receives it (no key, notes, lesson, weight or card sections; a test holds it to that), and a separate `claude -p` session with no tools, run from an empty directory outside the repository, answers it from that alone; every difference from the key is reported. One easy task made with it, `flower-shop-daily-revenue` over `flower-shop-revenue`, whose smoke answer matched the key | `.claude/skills/author-task`, `src/Modules/Ritocode.Modules.Content/Authoring`, `content/materials`, `content/tasks` |
| Attempts ([#20](https://github.com/shoraLBRT/ritocode/issues/20), [#125](https://github.com/shoraLBRT/ritocode/issues/125)) | **Scoring**: `DiagnosisScoring.Score`, a pure function of the answer, the key with the card weights, and the parameters of SPEC §5.2 (`Attempts:Scoring`, validated on start); the total floored at zero, the maximum, whether the answer is correct, and a line per card — found with its treatment, missed or extra — with the author's leaves for every card of the key. **Attempts**: the `attempts` schema; `POST /attempts` (a published task; 201), `PATCH /attempts/{id}` (the step reached, forward only), `POST /attempts/{id}/submit` (validated against the cards the task offers and the leaves of the tree, scored, stored with the content revision; the first submitted attempt at a task counts, later ones are practice, a partial unique index settles a race), `GET /attempts/{id}`, `GET /attempts?task=` (a page, newest first). A submitted attempt is never changed; a test re-ingests changed content and compares the stored result byte for byte. Submitting is capped per user (`Attempts:RateLimit`, 10 in 10 minutes, `429 attempt_rate_limited`). The key and weights come from Content through `ITaskForAttemptLookup`; `GET /tasks` carries `solved` for a signed-in caller through `ISubmittedTaskLookup`, which Attempts answers | `src/Modules/Ritocode.Modules.Attempts`, `src/Ritocode.Shared/Contracts` |
| Frontend shell ([#26](https://github.com/shoraLBRT/ritocode/issues/26)) | React, Vite and TypeScript; the API client that owns the error envelope; layout, routes, loading, error and empty states. A **translation catalogue** of its own (`src/i18n`: typed dotted keys, `{name}` placeholders, Russian plurals through `Intl.PluralRules`, `<html lang="ru">`), every string moved into it, and an ESLint rule that fails on text written in JSX. The **signed-in state** from `/me` (`src/session`: loading, signed in, signed out on a 401, error) in the header, and `RequireSignIn`, a layout route for pages that need a learner. A **phone-width layout**, checked at 375 px: no horizontal scroll, the header wraps | `frontend/` |
| CI | Backend build, test, formatting, migrations and drift; frontend lint, build and test. Nothing is shipped yet | `.github/workflows/` |

Removed in #119: the Workspaces, Evaluations, Submissions and Progress modules, the sandbox runner,
`spikes/`. Removed in #121: the Problems module, the old package format and its C# packages, object
storage with MinIO, and the frontend's old problem pages. All of it remains readable at the tag
`pre-diagnosis`.

## Next up

From [ROADMAP.md](ROADMAP.md), in order:

1. S3, beside the content track: the catalogue pages
   ([#27](https://github.com/shoraLBRT/ritocode/issues/27)), then the task screen
   ([#126](https://github.com/shoraLBRT/ritocode/issues/126)) over the attempt endpoints, and the
   review screen ([#29](https://github.com/shoraLBRT/ritocode/issues/29)).
2. The content track is the maintainer's, with the two skills: 55–60 cards
   ([#124](https://github.com/shoraLBRT/ritocode/issues/124)) and the 20 tasks
   ([#42](https://github.com/shoraLBRT/ritocode/issues/42)).

The maintainer's own [#134](https://github.com/shoraLBRT/ritocode/issues/134) — domain, VPS, OAuth
apps, privacy text — runs in parallel and gates S7.

## Open questions

Decisions the specification left open are listed in [SPEC.md](SPEC.md) §13. Add here anything a
future session would otherwise have to rediscover.

- Tasks are ordered by difficulty, then title. SPEC §4.3 says "then publication", which needs a
  first-published timestamp the schema does not keep yet; add it if the order starts to matter.
- **An attempt's result holds the score and the key, not the task's notes and lesson.** The review
  (#29) needs both; it either reads them from content when it renders — they may then have changed
  since the attempt — or #29 adds them to the stored result on submit. Decide in #29.
- **Every `POST /attempts` starts a new attempt.** Opening a task twice leaves an open attempt
  behind; the task screen (#126) decides whether to resume an open one from `GET /attempts?task=`.
- **An unpublished task can still be submitted** by someone who started it before it left
  `content/`, and cannot be started. "Solved" on the catalogue means at least one submitted attempt.
- Ingest has no production entry point yet: a development host seeds `content/` on start, stamped
  `development`. The release command of [#136](https://github.com/shoraLBRT/ritocode/issues/136)
  runs ingest with the commit it deploys.
- **Smoke tests go stale as the catalogue grows.** A task is smoke-tested against the cards that
  exist when it is written; an easy task's shortlist and any other task's full list change as cards
  are added, and a new card may name a problem an old material already has. Re-run the smoke test
  over existing tasks once #124 has filled the catalogue. `flower-shop-daily-revenue` was tested
  against three cards, which is a weak test.
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
| `Ritocode.Architecture.Tests` | 13 |
| `Ritocode.Shared.Tests` | 51 |
| `Ritocode.Api.Tests` | 57 |
| `Ritocode.Modules.Content.Tests` | 68 |
| `Ritocode.Modules.Attempts.Tests` | 15 |
| Frontend (vitest) | 58 |

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
| `GET /api/v1/me` | `200`, `{"id":"0199aa00-…","username":"developer"}` under the development identity; `401 unauthenticated` without it |
| `GET /api/v1/problems` | `200`, `{ classes, cards }` — the six classes once content is seeded |
| `GET /api/v1/treatments` | `200`, five branches, leaves as `branch.leaf` |
| `GET /api/v1/tasks?pageSize=1000` | `400`, `code: "validation_failed"`, `errors.pageSize` present |
| `GET /api/v1/tasks/no-such-task` | `404`, `code: "task_not_found"` |
| `POST /api/v1/attempts` with `{"task":"<a seeded task>"}` | `201`, `Location` set, `step: "diagnosis"`, `result: null` |
| `POST /api/v1/attempts/{id}/submit` with `{"picks":[]}` | `200`, `result` with `total`, `maximum` and a line per card |
| `GET /api/v1/tasks` after a submit | `200`, that task has `solved: true` |
| any response | carries an `X-Request-Id` header |
