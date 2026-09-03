# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-03
- **Current phase:** Phase 1 (MVP) — see `docs/MVP_SCOPE.md`
- **Current milestone:** the vertical slice — [`docs/SLICE_PLAN.md`](SLICE_PLAN.md), decided in
  [ADR 0005](adr/0005-vertical-slice-before-breadth.md). Phase 1 now ships in two stages; the slice
  is stage one. **Take work from the slice plan, not from the whole phase.**
- **Backlog board:** <https://github.com/users/shoraLBRT/projects/3>
- **Source of truth for task status:** GitHub issues. This file summarises; the board decides.
  `SLICE_PLAN.md` tracks slice progress, which an issue list cannot express — several issues are
  entered partially and stay open on purpose.

---

## Session workflow

Follow this loop. It is what makes the project continue without the maintainer re-explaining it.

1. **Orient.** Read `AGENTS.md`, this file, `docs/SLICE_PLAN.md`, and `docs/adr/` (at least the
   index and [ADR 0005](adr/0005-vertical-slice-before-breadth.md), which says what may and may not
   be cut).
2. **Pick work.** Take the first unticked box in `docs/SLICE_PLAN.md`. Confirm the issue behind it
   is still open:

   ```bash
   gh issue list --repo shoraLBRT/ritocode --state open --label phase:1 --limit 60
   ```

3. **Branch.** One issue per branch: `git checkout -b feat/<short-slug>` off `main`.
4. **Build it.** Follow `docs/AGENT_GUIDELINES.md` and the ADRs. Tests come with the code, not after.
5. **Verify.** Everything under [Verification](#verification) must pass. No exceptions, no
   "will fix in CI".
6. **Ship.** Commit, push, open a PR that names the issue (`Closes #N`). Comment on the issue with
   what landed and what was deliberately left out.
7. **Record.** Tick the box in `docs/SLICE_PLAN.md` and update its progress counters, move the
   issue into [What exists](#what-exists) if it is fully done, refresh *Last updated*, and add
   anything a future session would otherwise have to rediscover to
   [Open questions](#open-questions).

Rules that are easy to get wrong:

- **Never commit to `main`.** The maintainer merges PRs.
- **Never mark an issue done with failing or skipped tests.** Partial work ships as a PR that says
  plainly what is missing, with the issue left open.
- **A decision that outlives the session goes in an ADR**, not in a commit message.
- **Stay in phase.** Phase 2 and 3 issues exist but are not open work until Phase 1 closes.
- **Stay in the slice.** Phase 1 work outside `SLICE_PLAN.md` waits for stage two, and the
  reductions the slice makes are listed in ADR 0005 — the allowed ones and, more importantly, the
  forbidden ones. A shortcut from the forbidden list is not a trade-off, it is a defect.

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
  SLICE_PLAN.md               the current milestone, tracked box by box
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

The slice plan is the ordered list now: **[`docs/SLICE_PLAN.md`](SLICE_PLAN.md)**. Take the first
unticked box. The stages there are ordered so that each depends only on stages above it.

Immediately: stage 1, which is the whole of the first week and blocks everything after it.

1. **[#37](https://github.com/shoraLBRT/ritocode/issues/37) — Backend integration test harness.**
   Testcontainers, a database per test class. It is first because it is the one item that gets
   cheaper by being done earlier: #9 is the first module that writes to its schema, and without a
   harness its tests invent a fixture that has to be rewritten later.
2. **[#8](https://github.com/shoraLBRT/ritocode/issues/8) — Problem package manifest format.**
   `problem.yaml`, the shape of `validator_config`, allowed paths, hints, constraints. No database
   dependency, so it runs as a second branch in parallel with #37.
3. **Sandbox spike, then ADR 0006 and ADR 0007.** The spike is time-boxed and closes no issue; the
   two ADRs settle the sandbox execution model and the form of the cross-module contract. Both are
   assumed by work in stage 3 and stage 4, so they are written before that work starts.

What used to be items 3 to 6 here — #9, #42, #5, #6 — are now stages 2 and 3 of the slice, entered
partially. The rest of Phase 1 is [after the slice](SLICE_PLAN.md#after-the-slice).

---

## Open questions

Decisions a future session will hit, and where in the slice each one comes due.

- **Language of the first problems.** *Due in slice stage 2, and nothing else is blocked by it.*
  Undecided, and it is the maintainer's call. C# means your own stack and the fastest validators;
  JavaScript or TypeScript means a wider pool of testers. The tiebreaker is neither: pick the
  language in which you can author three honest tasks in two days, because a weak task proves
  nothing on a popular language and a strong one proves plenty on an unpopular one.
- **Session tokens: JWT or opaque plus a server-side store?** *Due in stage two, invisible during
  the slice* — the identity seam hides it. Opaque tokens make revocation trivial, which matters
  once submissions can open real pull requests in Phase 3. Worth an ADR before #6 is completed, or
  the choice gets made by whoever writes the endpoint.
- **Sandbox runner host.** *Slice answer settled, production answer deferred.*
  [ADR 0005](adr/0005-vertical-slice-before-breadth.md) fixes `docker run` with limits for the
  slice; ADR 0006 records what the spike found. Docker-in-Docker, a dedicated runner VM and a warm
  pool stay open until queue depth makes one of them necessary.
- **Test database isolation.** *Now the first item in the slice* — it is the substance of #37, and
  it is first precisely because every later test depends on the answer.
- **Who validates cross-module references?** [ADR 0004](adr/0004-persistence-and-migrations.md)
  says the module creating the row does, but there is still no mechanism. ADR 0002 already settled
  *where* the contract lives (`Ritocode.Shared`); ADR 0007 in stage 1 settles *what shape* it takes,
  and #10 in stage 3 is the first consumer.
- **Queue transport.** *Settled for the slice:* a PostgreSQL table drained with `SKIP LOCKED`. The
  partial index `(status, created_at) WHERE status IN ('Queued','Running')` has been in
  `SubmissionConfiguration` since #3 — the schema was designed for this query. Redis is not adopted
  unless the table stops keeping up.
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
