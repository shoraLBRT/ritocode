# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-01
- **Current phase:** Phase 1 (MVP) — see `docs/MVP_SCOPE.md`
- **Backlog board:** <https://github.com/users/shoraLBRT/projects/3>
- **Source of truth for task status:** GitHub issues. This file summarises; the board decides.

---

## Session workflow

Follow this loop. It is what makes the project continue without the maintainer re-explaining it.

1. **Orient.** Read `AGENTS.md`, this file, and `docs/adr/` (at least the index).
2. **Pick work.** Take the top unblocked item from [Next up](#next-up). Confirm it is still open:

   ```bash
   gh issue list --repo shoraLBRT/ritocode --state open --label phase:1 --limit 60
   ```

3. **Branch.** One issue per branch: `git checkout -b feat/<short-slug>` off `main`.
4. **Build it.** Follow `docs/AGENT_GUIDELINES.md` and the ADRs. Tests come with the code, not after.
5. **Verify.** Everything under [Verification](#verification) must pass. No exceptions, no
   "will fix in CI".
6. **Ship.** Commit, push, open a PR that names the issue (`Closes #N`). Comment on the issue with
   what landed and what was deliberately left out.
7. **Record.** Move the issue out of [Next up](#next-up) into [What exists](#what-exists), refresh
   *Last updated*, and add anything a future session would otherwise have to rediscover to
   [Open questions](#open-questions).

Rules that are easy to get wrong:

- **Never commit to `main`.** The maintainer merges PRs.
- **Never mark an issue done with failing or skipped tests.** Partial work ships as a PR that says
  plainly what is missing, with the issue left open.
- **A decision that outlives the session goes in an ADR**, not in a commit message.
- **Stay in phase.** Phase 2 and 3 issues exist but are not open work until Phase 1 closes.

---

## Stack

Decided in [ADR 0001](adr/0001-technology-stack.md).

| Layer | Choice |
| --- | --- |
| Backend | ASP.NET Core, .NET 10, minimal APIs, C# |
| Modules | One project per module, boundaries enforced by tests ([ADR 0002](adr/0002-modular-monolith-layout.md)) |
| API contract | RFC 9457 errors, offset pagination, FluentValidation ([ADR 0003](adr/0003-api-conventions.md)) |
| Tests | xUnit v3, `Microsoft.AspNetCore.TestHost` |
| Database | PostgreSQL 17, EF Core per module ([ADR 0004](adr/0004-persistence-and-migrations.md)) |
| Frontend | React + Vite + TypeScript (**not started**) |

Package versions live in `Directory.Packages.props`. The SDK is pinned in `global.json`.
Warnings are errors, vulnerability warnings included — a red build on a newly disclosed CVE is
expected behaviour, fixed by pinning the package forward.

---

## Repository map

```
Ritocode.slnx
Directory.Build.props         shared MSBuild settings (net10.0, nullable, warnings-as-errors)
Directory.Packages.props      central package versions
.editorconfig                 style, plus analyzer rules deliberately disabled, each with a reason
compose.yaml                  PostgreSQL and MinIO for local development
scripts/                      dev-up, migration helpers, drift check
src/
  Ritocode.Api/               composition root: pipeline, config, health, meta, module wiring
  Ritocode.DbMigrator/        applies each module's migrations; the host never migrates itself
  Ritocode.Shared/            errors, Result<T>, paging, IModule, correlation, persistence base
  Modules/Ritocode.Modules.*  one project per module: domain, DbContext, migrations
tests/
  Ritocode.Shared.Tests/        unit tests for the shared primitives
  Ritocode.Api.Tests/           in-memory host tests over the real composition root
  Ritocode.Architecture.Tests/  module boundary rules, executable
docs/
  adr/                        architecture decision records
  DATABASE_SCHEMA.md          ERD, conventions, and what the schema enforces
  PROJECT_STATE.md            this file
```

---

## What exists

| Issue | State | What landed | Where |
| --- | --- | --- | --- |
| [#1](https://github.com/shoraLBRT/ritocode/issues/1) Backend skeleton | Done | Solution, 7 module projects with enforced boundaries, options-based config validated at startup, request-id middleware, unified error handler, `/health/live` + `/health/ready`, `/api/v1/meta/modules` | `src/`, `tests/Ritocode.Architecture.Tests` |
| [#2](https://github.com/shoraLBRT/ritocode/issues/2) API conventions | Done | [ADR 0003](adr/0003-api-conventions.md), `ErrorStatusCodeMap`, `ApiProblem`, `PageRequest`/`Page<T>`, `ValidationEndpointFilter<T>` | `src/Ritocode.Shared`, `docs/adr/0003-api-conventions.md` |
| [#3](https://github.com/shoraLBRT/ritocode/issues/3) Core schema | Done | Seven tables across five module schemas, [ADR 0004](adr/0004-persistence-and-migrations.md), ERD in [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md), initial migrations | `src/Modules/*/Domain`, `src/Modules/*/Persistence` |
| [#4](https://github.com/shoraLBRT/ritocode/issues/4) Migration workflow | Done | `Ritocode.DbMigrator` (`apply` / `status`), `dotnet-ef` pinned as a local tool, CI applies from an empty database and fails on model drift | `src/Ritocode.DbMigrator`, `.github/workflows/backend-ci.yml` |
| [#32](https://github.com/shoraLBRT/ritocode/issues/32) Local environment | Done | `compose.yaml` (PostgreSQL + MinIO with buckets), `scripts/dev-up.sh` / `.ps1` doing setup and migrations in one command | `compose.yaml`, `scripts/` |

Nothing else from the backlog is implemented. Every module owns a schema and a `DbContext`, but
none exposes an endpoint or a service yet — the boundary and the storage are in place, the behaviour
is not.

### Deliberately deferred

- **CI ([#31](https://github.com/shoraLBRT/ritocode/issues/31))** covers the backend only; the
  frontend job lands with the frontend, so the issue stays open.
- **No repositories or services over the schema.** The tables exist and are migrated; nothing
  reads or writes them yet. The first module to do so is Problems, in
  [#9](https://github.com/shoraLBRT/ritocode/issues/9).
- **No authentication.** Endpoints are anonymous. `AllowAnonymous()` on health and meta is
  deliberate so they keep working once authentication is switched on in
  [#6](https://github.com/shoraLBRT/ritocode/issues/6).
- **No object storage client.** MinIO runs and its buckets exist, but no code talks to it. That
  arrives with [#5](https://github.com/shoraLBRT/ritocode/issues/5).
- **Cross-module references carry no foreign key**, by design — see
  [ADR 0004](adr/0004-persistence-and-migrations.md). Whichever module creates such a row is
  responsible for validating the reference first.

---

## Next up

Ordered. Items lower in the list depend on items above them.

1. **[#8](https://github.com/shoraLBRT/ritocode/issues/8) — Problem package manifest format.**
   Specification work: the `problem.yaml` schema, validator config, allowed paths, hints,
   constraints. `problem_versions.validator_config` is already a `jsonb` column waiting for this
   shape. No database dependency, so it can proceed in parallel with #37.
2. **[#37](https://github.com/shoraLBRT/ritocode/issues/37) — Backend integration test harness.**
   The API tests now need a running PostgreSQL, supplied by compose locally and a service container
   in CI. Testcontainers would make that self-contained and give each test class an isolated
   database. Do this before #9, or the first repository tests will invent their own fixture.
3. **[#9](https://github.com/shoraLBRT/ritocode/issues/9) — Problem catalog service.** The first
   module with real endpoints, the first repository over the schema, and the first consumer of the
   pagination contract. Blocked on #8 and, in practice, #37.
4. **[#42](https://github.com/shoraLBRT/ritocode/issues/42) — Seed the Phase 1 problem set.**
   Needs #8 to have settled the manifest. The catalog is untestable end to end without real content.
5. **[#5](https://github.com/shoraLBRT/ritocode/issues/5) — Object storage layout.** MinIO and its
   three buckets exist; nothing writes to them. Needed before workspaces can materialise a snapshot.
6. **[#6](https://github.com/shoraLBRT/ritocode/issues/6) — Authentication and session issuance.**
   The `users` and `auth` schemas are ready. Settle the token question below before starting.

After that the MVP path runs #10 → #14 → #17: workspaces, then submissions, then evaluation.

---

## Open questions

Decisions a future session will hit. None block the items in [Next up](#next-up) except where noted.

- **Session tokens: JWT or opaque plus a server-side store?** Needed for #6. Opaque tokens make
  revocation trivial, which matters once submissions can open real pull requests in Phase 3.
- **Sandbox runner host.** Docker-in-Docker, a dedicated runner VM, or a warm pool — undecided, and
  needed by #21. Not urgent, but it constrains how #17 is designed, so decide before building the
  orchestrator.
- **Test database isolation.** Every API test class currently shares one database, which is fine
  while nothing writes. The moment repositories land, tests need either a database per class or
  a reliable reset between them. That is the substance of #37.
- **Who validates cross-module references?** [ADR 0004](adr/0004-persistence-and-migrations.md) says
  the module creating the row does, but there is no mechanism yet — no module can call another. The
  first case is #10 (a workspace needs the user and the problem version to exist), and it will force
  a decision about in-process cross-module calls.
- **`net10.0` package pinning.** Framework-tied packages sit at `10.0.9` to match the development
  machine's runtime. When CI or a deployment target moves ahead, raise them together.

---

## Verification

Run from the repository root. All of these must be clean before opening a PR.

**Start the dependencies first.** The API tests exercise the readiness probe, which queries every
module schema, so they need a running PostgreSQL:

```bash
./scripts/dev-up.sh
```

```bash
dotnet build Ritocode.slnx --warnaserror
```

```bash
dotnet test Ritocode.slnx
```

```bash
./scripts/db-verify-no-drift.sh
```

```bash
dotnet run --project src/Ritocode.Api --no-launch-profile --urls http://127.0.0.1:5199
```

With the host running, these are the current smoke checks:

| Request | Expected |
| --- | --- |
| `GET /health/live` | `200`, `{"status":"Healthy","checks":[]}` |
| `GET /health/ready` | `200`, `"status":"Healthy"`, one check per module schema |
| `GET /api/v1/meta/modules` | `200`, all seven modules listed |
| any response | carries an `X-Request-Id` header |

The host reads `Database:ConnectionString`; locally it comes from `Database__ConnectionString`,
which `scripts/dev-up` prints the value for. The tests read `RITOCODE_TEST_DATABASE` and fall back
to the compose defaults, so they need no configuration on a machine that ran `dev-up`.

Current baseline: **54 tests, all passing** — 33 shared, 16 API, 5 architecture.
A session that leaves this number lower than it found it has broken something.
