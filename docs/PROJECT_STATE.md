# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-05
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
content/
  problems/                   problem packages; the reference one is validated by tests
src/
  Ritocode.Api/               composition root: pipeline, config, health, meta, module wiring
  Ritocode.DbMigrator/        applies each module's migrations; the host never migrates itself
  Ritocode.Shared/            errors, Result<T>, paging, IModule, correlation, persistence base
  Modules/Ritocode.Modules.*  one project per module: domain, DbContext, migrations
                              Problems also owns Packaging/: the problem package format
tests/
  Ritocode.TestSupport/         integration test harness: a PostgreSQL container per test
                                assembly, a migrated database per test class
  Ritocode.Shared.Tests/        unit tests for the shared primitives
  Ritocode.Api.Tests/           in-memory host tests over the real composition root
  Ritocode.Architecture.Tests/  module boundary rules, executable
  Ritocode.Modules.Problems.Tests/  the problem package format, and the reference package
spikes/
  sandbox-execution/          time-boxed experiment behind ADR 0006, with the script that repeats it
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
| [#37](https://github.com/shoraLBRT/ritocode/issues/37) Integration test harness | Partial | `PostgresTestServer`: one Testcontainers PostgreSQL per test assembly, one migrated database per test class, copied from a template migrated once by `MigrationRunner`. API tests moved onto it; CI's test job dropped its service container | `tests/Ritocode.TestSupport` |
| [#8](https://github.com/shoraLBRT/ritocode/issues/8) Problem package manifest | Done | The format in [PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md) — `problem.yaml`, allowed paths, hints, limits, the validator pipeline and its canonical `validator_config` JSON — with a loader that reports every fault at once, and a reference package validated from the committed tree | `src/Modules/Ritocode.Modules.Problems/Packaging`, `content/problems/example-order-total`, `tests/Ritocode.Modules.Problems.Tests` |

Nothing else from the backlog is implemented. Every module owns a schema and a `DbContext`, but
none exposes an endpoint or a service yet — the boundary and the storage are in place, the behaviour
is not. The Problems module is the first with domain code of its own: the package format reads and
validates content, and still writes nothing to its schema.

### Deliberately deferred

- **CI ([#31](https://github.com/shoraLBRT/ritocode/issues/31))** covers the backend only; the
  frontend job lands with the frontend, so the issue stays open.
- **The flow tests in [#37](https://github.com/shoraLBRT/ritocode/issues/37)** — auth, problems,
  workspace, submission — need endpoints that do not exist yet. The harness they will be written
  on does exist, which was the point of doing #37 first; the tests themselves arrive with the
  features, in slice stages 2 to 4, and the issue stays open until then.
- **Nothing ingests a problem package yet.** The format is defined and packages load and validate,
  but no code turns one into a `Problem`, a `ProblemVersion` and a bundle in object storage. That
  is [#9](https://github.com/shoraLBRT/ritocode/issues/9) and
  [#42](https://github.com/shoraLBRT/ritocode/issues/42), in slice stage 2. The reference package
  is content for the tests, not catalog content.
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

Immediately: the rest of stage 1, which blocks everything after it.

1. **ADR 0007 — cross-module contract form.** The last box in stage 1, and the only one left. ADR
   0002 already settled that the contract lives in `Ritocode.Shared`; this settles its shape — thin
   read-interfaces, one per need, rather than an in-process mediator. Unblocked, untouched by the
   sandbox spike, and assumed by #10 in stage 3. It is an ADR, so it is the maintainer's decision to
   agree before it is written.

[ADR 0006](adr/0006-sandbox-execution-model.md) is off this list: the sandbox execution model is
decided. What it obliges later stages to do is in [Open questions](#open-questions) below —
briefly, ingest gains a dependency check, submission reports gain somewhere to carry a timeout or a
resource exhaustion, and #22 gains a runner registry.

[#37](https://github.com/shoraLBRT/ritocode/issues/37) is off this list: the harness landed, and
the flow tests the issue also asks for arrive with the endpoints they exercise.
[#8](https://github.com/shoraLBRT/ritocode/issues/8) is off it because it is done.

What used to be items 3 to 6 here — #9, #42, #5, #6 — are now stages 2 and 3 of the slice, entered
partially. The rest of Phase 1 is [after the slice](SLICE_PLAN.md#after-the-slice).

---

## Open questions

Decisions a future session will hit, and where in the slice each one comes due.

- **Language of the first problems.** *Due in slice stage 2, and nothing else is blocked by it.*
  Still undecided, and it is the maintainer's call. The reference package that ships with the
  manifest format is C#, which decides nothing: `language` is a manifest field, and the runner
  image registry it selects from belongs to [#22](https://github.com/shoraLBRT/ritocode/issues/22). C# means your own stack and the fastest validators;
  JavaScript or TypeScript means a wider pool of testers. The tiebreaker is neither: pick the
  language in which you can author three honest tasks in two days, because a weak task proves
  nothing on a popular language and a strong one proves plenty on an unpopular one.
- **Session tokens: JWT or opaque plus a server-side store?** *Due in stage two, invisible during
  the slice* — the identity seam hides it. Opaque tokens make revocation trivial, which matters
  once submissions can open real pull requests in Phase 3. Worth an ADR before #6 is completed, or
  the choice gets made by whoever writes the endpoint.
- **Sandbox runner host.** *Slice answer settled and now measured, production answer deferred.*
  [ADR 0005](adr/0005-vertical-slice-before-breadth.md) fixes `docker run` with limits for the
  slice, the spike confirmed every flag in that list holds while both of the reference package's
  real validators run underneath them, and [ADR 0006](adr/0006-sandbox-execution-model.md) now
  fixes the contract around it. Docker-in-Docker, a dedicated runner VM and a warm pool stay open
  until queue depth makes one of them necessary. Measured on one Windows/WSL2 machine on cgroups
  v1; a Linux host on cgroups v2 is worth re-measuring, which is one run of
  `spikes/sandbox-execution/run-spike.sh`.
- **How a verdict is derived from a runner artifact.** *Settled by
  [ADR 0006](adr/0006-sandbox-execution-model.md) §6, due in stage 5 with
  [#20](https://github.com/shoraLBRT/ritocode/issues/20).* Scores come from a normalised projection
  — for the test validator, the sorted `testName` → `outcome` pairs, which are byte-identical
  across runs whose raw TRX differs every time. The raw artifact is what a person reads, under
  `submission_reports.logs_reference`, and is never what a score is derived from.
  [#38](https://github.com/shoraLBRT/ritocode/issues/38) asserts on the projection. The resource
  limits are part of the same contract: `--cpus` and `--memory` are visible to the runtime, so
  changing them can legitimately change a test's answer, and results are comparable only within one
  image-and-limits version.
- **Where a runner's guarantees may live.** *Settled by
  [ADR 0006](adr/0006-sandbox-execution-model.md) §1–2, due in stage 5 with
  [#21](https://github.com/shoraLBRT/ritocode/issues/21).* Containment lives in the container flags
  and nowhere else. Where a toolchain lets a submitted `NuGet.Config` or `Directory.Build.props`
  outrank the runner's intent, the runner appends arguments that win by precedence — and those
  arguments belong to the **image**, not the runner, so `ISandboxRunner` stays language-agnostic and
  a second language adds a registry row rather than a branch. The ingest-denylist alternative was
  rejected: a user can write the same files into a workspace, where ingest never sees them.
- **Ingest has to check a package's dependencies against the image's offline cache.** *Created by
  [ADR 0006](adr/0006-sandbox-execution-model.md) §3, due in stage 2 with
  [#9](https://github.com/shoraLBRT/ritocode/issues/9) and
  [#42](https://github.com/shoraLBRT/ritocode/issues/42).* `--network none` means the runner image's
  warmed package cache is the entire set of dependencies a problem may have. A problem outside it
  can never be evaluated by anyone, so the rejection belongs at ingest. Until that check exists the
  failure still happens — as a failed compile validator at submission time, blamed on the submitter
  rather than on the content.
- **Where a submission report carries a timeout or a resource exhaustion.** *Created by
  [ADR 0006](adr/0006-sandbox-execution-model.md) §5, due in stage 4 with
  [#14](https://github.com/shoraLBRT/ritocode/issues/14) and
  [#17](https://github.com/shoraLBRT/ritocode/issues/17).* The runner distinguishes `Completed`,
  `TimedOut`, `ResourceExhausted` and `Crashed`, and is explicitly allowed not to know which of the
  last two applies — `OOMKilled` is a reliable positive and an unreliable negative, since a managed
  `OutOfMemoryException` aborts at 134 before the kernel is involved. If the schema above the runner
  has nowhere to put that distinction, the honesty is discarded on the way up and a person is told
  their tests failed when the container was killed.
- **Test database isolation.** *Settled.* One PostgreSQL container per test assembly, one database
  per test class, each copied from a template that `MigrationRunner` migrated once. Isolation is
  per database rather than per transaction because a test that wants to see what a migration, a
  trigger or a check constraint actually did cannot see it inside a transaction the harness rolls
  back. Consequence: `dotnet test` now needs a Docker daemon, and no longer needs `dev-up`.
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

**A Docker daemon has to be running.** Tests that need PostgreSQL start their own container
through Testcontainers, so `dotnet test` no longer needs `dev-up` — but it does need Docker.

```bash
dotnet build Ritocode.slnx --warnaserror
```

```bash
dotnet test Ritocode.slnx
```

The drift check and the host below run against the compose stack, which needs starting:

```bash
./scripts/dev-up.sh
```

`db-verify-no-drift.sh` and `dotnet ef` read `Database__ConnectionString` from the environment and
fail with a validation error if it is unset. `dev-up` prints the value.

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
which `scripts/dev-up` prints the value for. The tests configure themselves from the container the
harness starts, so they need no environment variable at all.

Current baseline: **121 tests, all passing** — 33 shared, 65 problems, 18 API, 5 architecture.
A session that leaves this number lower than it found it has broken something.
