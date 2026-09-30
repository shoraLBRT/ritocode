# Project State

**The living state of the project: what exists, what is next, and how to verify a change.** Update it
in the same pull request as the work it describes — a session that skips this makes the next one
start from nothing.

- **Last updated:** 2026-09-30
- **Current stage:** S1 · Content foundation — see [ROADMAP.md](ROADMAP.md)
- **Board:** <https://github.com/users/shoraLBRT/projects/3> — issues are the source of truth for
  what is done
- **What the product is:** [CONCEPT.md](CONCEPT.md); what gets built: [SPEC.md](SPEC.md)

---

## Session workflow

The `session` skill in `.claude/skills/session/` carries the full loop. In short:

1. `git fetch origin main`, then read `CLAUDE.md`, `AGENTS.md`, this file and `ROADMAP.md` from
   `origin/main`.
2. Take the first open issue of the lowest open stage in `ROADMAP.md` whose dependencies are met.
3. Branch off `origin/main`, one issue per branch.
4. Build it with its tests; run everything under [Verification](#verification).
5. Open a PR naming the issue, comment on the issue, and update this file in the same PR.

---

## What exists

After [#119](https://github.com/shoraLBRT/ritocode/issues/119), the repository holds the platform
the new product keeps, plus the Problems module that S1 replaces.

| Part | State | Where |
| --- | --- | --- |
| Modular monolith | Solution, module boundaries enforced by tests, options validated at startup, request id, unified error body, health endpoints, `/api/v1/meta/modules` | `src/`, `tests/Ritocode.Architecture.Tests` |
| API conventions | [ADR 0003](adr/0003-api-conventions.md): RFC 9457 errors, pagination envelope, validation filter | `src/Ritocode.Shared` |
| Persistence | PostgreSQL, EF Core per module, one schema each, migrations applied by `Ritocode.DbMigrator`, drift check in CI ([ADR 0004](adr/0004-persistence-and-migrations.md)) | `src/Ritocode.DbMigrator`, `scripts/` |
| Test harness | One PostgreSQL container per test assembly, one migrated database per test class | `tests/Ritocode.TestSupport` |
| Identity seam | `ICurrentUser`, a real authentication scheme with a seeded development identity, authenticated by default ([ADR 0008](adr/0008-authentication-seam.md)). No real sign-in yet | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth` |
| Users | The `users` table and `IUserLookup`. `xp` and `trust_level` removed in #119 | `src/Modules/Ritocode.Modules.Users` |
| Ownership rule | An architecture test reading compiled IL: a user's rows are reached only where the owner is in the query. No module owns such rows until Attempts ([#125](https://github.com/shoraLBRT/ritocode/issues/125)); its reader is proved against a test-only context | `tests/Ritocode.Architecture.Tests/OwnershipRuleTests.cs` |
| Content format ([#120](https://github.com/shoraLBRT/ritocode/issues/120)) | Parsers and validation for [CONTENT_FORMAT.md](CONTENT_FORMAT.md): taxonomy, cards, materials, tasks, every rule of §7 tested. `content validate` runs in CI (job *Validate content*). The taxonomy of SPEC §3.3 is committed with Russian labels; a reference card set, material and task live in the test fixtures. Not yet loaded into the database — that is #121 | `src/Modules/Ritocode.Modules.Problems/ContentFormat`, `src/Ritocode.ContentTool`, `content/taxonomy` |
| Problems — previous product | The old package format, ingest into object storage, and `GET /api/v1/problems`. **Replaced in S1** by [#121](https://github.com/shoraLBRT/ritocode/issues/121) and [#9](https://github.com/shoraLBRT/ritocode/issues/9) | `src/Modules/Ritocode.Modules.Problems`, `content/legacy-problems` |
| Frontend shell | React, Vite and TypeScript; the API client that owns the error envelope; layout, routes, loading, error and empty states | `frontend/` |
| CI | Backend build, test, formatting, migrations and drift; frontend lint, build and test. Nothing is shipped yet | `.github/workflows/` |

Removed in #119: the Workspaces, Evaluations, Submissions and Progress modules, the sandbox runner,
`spikes/`. They remain readable at the tag `pre-diagnosis`.

## Next up

From [ROADMAP.md](ROADMAP.md), in order:

1. [#121](https://github.com/shoraLBRT/ritocode/issues/121) — ingest content into PostgreSQL, and
   remove object storage, the old package format and `content/legacy-problems`.
2. [#9](https://github.com/shoraLBRT/ritocode/issues/9) — the content read APIs.
3. S2 can start once #121 lands: the `author-card` and `author-task` skills
   ([#122](https://github.com/shoraLBRT/ritocode/issues/122),
   [#123](https://github.com/shoraLBRT/ritocode/issues/123)).

The maintainer's own [#134](https://github.com/shoraLBRT/ritocode/issues/134) — domain, VPS, OAuth
apps, privacy text — runs in parallel and gates S7.

## Open questions

Decisions the specification left open are listed in [SPEC.md](SPEC.md) §13. Add here anything a
future session would otherwise have to rediscover.

- `docs/PROBLEM_PACKAGE_SPEC.md` and `docs/STORAGE_LAYOUT.md` describe code that still exists and are
  removed together with it in #121, not in #40.
- The old C# packages moved to `content/legacy-problems/` in #120, so `content/problems/` holds
  cards. The development seeder and the old tests read them from there until #121 deletes them.
- **CI's *Build and test* job is red on `main`** since 2026-09-30: every test that starts MinIO fails
  pulling `quay.io/minio/minio` (unauthorized). Nothing else fails. #121 removes MinIO and with it
  the failure; until then, check that a red run has only those failures before merging.

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

**After #119, recreate the development database** (`docker compose down -v`, then `dev-up`): the
compose volume still holds the schemas of the removed modules, and no migration drops them.

### Baseline

| Suite | Tests |
| --- | --- |
| `Ritocode.Architecture.Tests` | 13 |
| `Ritocode.Shared.Tests` | 129 |
| `Ritocode.Api.Tests` | 40 |
| `Ritocode.Modules.Problems.Tests` | 151 |
| Frontend (vitest) | 55 |

The count is a ratchet: if it drops, the PR says which tests went and why.

### Smoke checks on a running host

```bash
dotnet run --project src/Ritocode.Api --no-launch-profile --urls http://127.0.0.1:5199
```

| Request | Expected |
| --- | --- |
| `GET /health/live` | `200`, `{"status":"Healthy","checks":[]}` |
| `GET /health/ready` | `200`, one check per module schema |
| `GET /api/v1/meta/modules` | `200`, three modules: Auth, Users, Problems |
| `GET /api/v1/problems?pageSize=1000` | `400`, `code: "validation_failed"`, `errors.pageSize` present |
| any response | carries an `X-Request-Id` header |
