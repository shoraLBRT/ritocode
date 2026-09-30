# Project State

**The living state of the project: what exists, what is next, and how to verify a change.** Update it
in the same pull request as the work it describes — a session that skips this makes the next one
start from nothing.

- **Last updated:** 2026-09-30
- **Current stage:** S2 · Authoring — S1 closed with #9: content is validated in CI, ingested, and
  served without its answer key — see [ROADMAP.md](ROADMAP.md)
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
| Identity seam | `ICurrentUser`, a real authentication scheme with a seeded development identity, authenticated by default ([ADR 0008](adr/0008-authentication-seam.md)). No real sign-in yet | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth` |
| Users | The `users` table and `IUserLookup`. `xp` and `trust_level` removed in #119 | `src/Modules/Ritocode.Modules.Users` |
| Ownership rule | An architecture test reading compiled IL: a user's rows are reached only where the owner is in the query. No module owns such rows until Attempts ([#125](https://github.com/shoraLBRT/ritocode/issues/125)); its reader is proved against a test-only context | `tests/Ritocode.Architecture.Tests/OwnershipRuleTests.cs` |
| Content ([#120](https://github.com/shoraLBRT/ritocode/issues/120), [#121](https://github.com/shoraLBRT/ritocode/issues/121)) | The format of [CONTENT_FORMAT.md](CONTENT_FORMAT.md) parsed and validated — every rule of §7 tested — and `content validate` in CI (job *Validate content*). The `content` schema — taxonomy, cards, materials, tasks — and an ingest that validates first, writes in one transaction stamped with the commit, upserts by slug, retires cards and unpublishes tasks that left `content/`, and derives the material overview and the easy-task shortlist. A development host seeds `content/` on start. The public reads of SPEC §9.3 ([#9](https://github.com/shoraLBRT/ritocode/issues/9)): `GET /problems` (every live card in full, with the classes), `GET /treatments`, `GET /tasks` (a page, easy first) and `GET /tasks/{slug}` (context, brief, material with its overview, the cards to pick from — name, summary and keywords only, the shortlist for an easy task — and the other tasks over the same material). No answer key and no card weight leave the server; a test serialises a task and looks for them | `src/Modules/Ritocode.Modules.Content`, `src/Ritocode.ContentTool`, `content/` |
| Authoring ([#122](https://github.com/shoraLBRT/ritocode/issues/122)) | The `author-card` skill: drafts a card from a name, reading the live catalogue so the summary is delimited from its neighbours; checks it with `content validate`; never overwrites a card. Three cards drafted with it — `secrets-in-repo`, `money-in-float`, `god-class` — open the catalogue of [#124](https://github.com/shoraLBRT/ritocode/issues/124) | `.claude/skills/author-card`, `content/problems` |
| Frontend shell | React, Vite and TypeScript; the API client that owns the error envelope; layout, routes, loading, error and empty states | `frontend/` |
| CI | Backend build, test, formatting, migrations and drift; frontend lint, build and test. Nothing is shipped yet | `.github/workflows/` |

Removed in #119: the Workspaces, Evaluations, Submissions and Progress modules, the sandbox runner,
`spikes/`. Removed in #121: the Problems module, the old package format and its C# packages, object
storage with MinIO, and the frontend's old problem pages. All of it remains readable at the tag
`pre-diagnosis`.

## Next up

From [ROADMAP.md](ROADMAP.md), in order:

1. S2: the `author-task` skill with the blind smoke test
   ([#123](https://github.com/shoraLBRT/ritocode/issues/123)). After it the content track — 55–60
   cards ([#124](https://github.com/shoraLBRT/ritocode/issues/124)) and the 20 tasks
   ([#42](https://github.com/shoraLBRT/ritocode/issues/42)) — is the maintainer's.
2. S3 can run beside the content track: scoring ([#20](https://github.com/shoraLBRT/ritocode/issues/20)),
   the Attempts module ([#125](https://github.com/shoraLBRT/ritocode/issues/125)) and the frontend
   shell ([#26](https://github.com/shoraLBRT/ritocode/issues/26)).

The maintainer's own [#134](https://github.com/shoraLBRT/ritocode/issues/134) — domain, VPS, OAuth
apps, privacy text — runs in parallel and gates S7.

## Open questions

Decisions the specification left open are listed in [SPEC.md](SPEC.md) §13. Add here anything a
future session would otherwise have to rediscover.

- Tasks are ordered by difficulty, then title. SPEC §4.3 says "then publication", which needs a
  first-published timestamp the schema does not keep yet; add it if the order starts to matter.
- Ingest has no production entry point yet: a development host seeds `content/` on start, stamped
  `development`. The release command of [#136](https://github.com/shoraLBRT/ritocode/issues/136)
  runs ingest with the commit it deploys.
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

**Recreate an old development database** (`docker compose down -v`, then `dev-up`): a volume from
before #119 or #121 still holds the schemas of the removed modules, and no migration drops them. The
compose stack is PostgreSQL only.

### Baseline

| Suite | Tests |
| --- | --- |
| `Ritocode.Architecture.Tests` | 13 |
| `Ritocode.Shared.Tests` | 48 |
| `Ritocode.Api.Tests` | 39 |
| `Ritocode.Modules.Content.Tests` | 57 |
| Frontend (vitest) | 44 |

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
| `GET /api/v1/meta/modules` | `200`, three modules: Auth, Users, Content |
| `GET /api/v1/problems` | `200`, `{ classes, cards }` — the six classes once content is seeded |
| `GET /api/v1/treatments` | `200`, five branches, leaves as `branch.leaf` |
| `GET /api/v1/tasks?pageSize=1000` | `400`, `code: "validation_failed"`, `errors.pageSize` present |
| `GET /api/v1/tasks/no-such-task` | `404`, `code: "task_not_found"` |
| any response | carries an `X-Request-Id` header |
