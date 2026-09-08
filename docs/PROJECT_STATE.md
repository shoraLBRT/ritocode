# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-08
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

1. **Orient.** `git fetch origin main` **before reading anything**, then read from `origin/main`:
   `AGENTS.md`, this file, `docs/SLICE_PLAN.md`, and `docs/adr/` (at least the index and
   [ADR 0005](adr/0005-vertical-slice-before-breadth.md), which says what may and may not be cut).
   These files are only as true as the checkout they are read from; reading a branch a previous
   session left behind is how a session takes a box that is already ticked on `main`.
2. **Pick work.** Take the first unticked box in `docs/SLICE_PLAN.md`. Confirm the issue behind it
   is still open:

   ```bash
   gh issue list --repo shoraLBRT/ritocode --state open --label phase:1 --limit 60
   ```

3. **Branch.** One issue per branch: `git checkout -b feat/<short-slug> origin/main`.
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
                              Storage/ holds the object storage client and the key layout as code
  Modules/Ritocode.Modules.*  one project per module: domain, DbContext, migrations
                              Problems also owns Packaging/ (the problem package format),
                              Ingest/ (package -> published version + bundle) and Catalog/
tests/
  Ritocode.TestSupport/         integration test harnesses: a PostgreSQL container per test
                                assembly with a migrated database per test class, and a MinIO
                                container with a bucket set per test class
  Ritocode.Shared.Tests/        the shared primitives, and the storage client against a real MinIO
  Ritocode.Api.Tests/           in-memory host tests over the real composition root
  Ritocode.Architecture.Tests/  module boundary rules, executable
  Ritocode.Modules.Problems.Tests/  the problem package format, the reference package, and
                                    ingest against a real PostgreSQL and MinIO
spikes/
  sandbox-execution/          time-boxed experiment behind ADR 0006, with the script that repeats it
docs/
  adr/                        architecture decision records
  DATABASE_SCHEMA.md          ERD, conventions, and what the schema enforces
  STORAGE_LAYOUT.md           buckets, object keys, and what a *_reference column holds
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
| [#37](https://github.com/shoraLBRT/ritocode/issues/37) Integration test harness | Partial | `PostgresTestServer`: one Testcontainers PostgreSQL per test assembly, one migrated database per test class, copied from a template migrated once by `MigrationRunner`. API tests moved onto it; CI's test job dropped its service container. `MinioTestServer` beside it does the same for object storage: one container per test assembly, a bucket per role per test class | `tests/Ritocode.TestSupport` |
| [#8](https://github.com/shoraLBRT/ritocode/issues/8) Problem package manifest | Done | The format in [PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md) — `problem.yaml`, allowed paths, hints, limits, the validator pipeline and its canonical `validator_config` JSON — with a loader that reports every fault at once, and a reference package validated from the committed tree | `src/Modules/Ritocode.Modules.Problems/Packaging`, `content/problems/example-order-total`, `tests/Ritocode.Modules.Problems.Tests` |
| [#5](https://github.com/shoraLBRT/ritocode/issues/5) Object storage layout and client | Partial | [STORAGE_LAYOUT.md](STORAGE_LAYOUT.md): three buckets as roles with configurable physical names, the `role/key` reference form stored in the three `*_reference` columns, object versus prefix references, and the keys for bundles, workspace snapshots and evaluation artifacts — and now the client that reads and writes them. `StorageRole`, `StorageReference` and `StorageKeys` make the layout executable; `IObjectStore` / `S3ObjectStore` put and get over the S3 API, registered from the composition root and tested against a real MinIO. Deletion, prefix listing and server-side copy stay out | `src/Ritocode.Shared/Storage`, `tests/Ritocode.TestSupport/MinioTestServer.cs`, `docs/STORAGE_LAYOUT.md` |
| [#9](https://github.com/shoraLBRT/ritocode/issues/9) Problem catalog | Partial | `GET /api/v1/problems` and `GET /api/v1/problems/{slug}` over `Page<T>`, and the ingest behind them: a validated package becomes a `Problem`, a published `ProblemVersion` and a bundle in object storage. The catalog resolves a problem's highest **published** version and never a draft. `snapshot_reference` is now a typed `StorageReference` column. A development-only content seeder is the first caller of ingest | `src/Modules/Ritocode.Modules.Problems/Catalog`, `.../Ingest`, `src/Ritocode.Shared/Persistence/StorageReferenceConverter.cs` |

Nothing else from the backlog is implemented. Six of the seven modules own a schema and a
`DbContext` and expose neither an endpoint nor a service — the boundary and the storage are in
place, the behaviour is not. **Problems is the exception and the first module that is actually
alive**: it reads and writes its own schema, serves two endpoints, and is the first caller of
`IObjectStore`. Everything downstream of it is still empty — nothing creates a workspace from a
published version yet, which is [#10](https://github.com/shoraLBRT/ritocode/issues/10) in stage 3,
and it is the first code that will read a bundle back.

### Deliberately deferred

- **CI ([#31](https://github.com/shoraLBRT/ritocode/issues/31))** covers the backend only; the
  frontend job lands with the frontend, so the issue stays open.
- **The flow tests in [#37](https://github.com/shoraLBRT/ritocode/issues/37)** — auth, problems,
  workspace, submission — need endpoints that do not exist yet. The harness they will be written
  on does exist, which was the point of doing #37 first; the tests themselves arrive with the
  features, in slice stages 2 to 4, and the issue stays open until then.
- **The catalog reads; nothing else about a problem is exposed.**
  [#9](https://github.com/shoraLBRT/ritocode/issues/9) stays open for search, facets, tag and
  difficulty filters, and explicit version resolution — a client can list published problems and
  fetch one by slug, and cannot ask for a particular version of it. Each of those is an addition to
  `IProblemCatalog` rather than a change to it, which is the reduction ADR 0005 allows.
- **Ingest does not check a package's dependencies against the runner image's offline cache**,
  which ADR 0006 §3 says it must. Neither half of that check exists to build on: a manifest does
  not declare dependencies, and there is no runner image or registry to name a cache — both arrive
  with [#22](https://github.com/shoraLBRT/ritocode/issues/22) in stage 5. It stays under
  [Open questions](#open-questions) with what unblocks it.
- **Ingest has no caller in a deployment.** `ProblemContentSeeder` publishes the packages in a
  content directory once, after startup, and is off unless `Problems:Content:SeedOnStartup` says
  otherwise — on in `appsettings.Development.json`, so a local run has a catalog to browse. It
  publishes a slug's *first* version only and skips a slug that already has one, so a restart is
  not a revision. Editing a package and wanting the new revision published is a real need with no
  answer yet, and the real content pipeline is
  [#42](https://github.com/shoraLBRT/ritocode/issues/42).
- **Only the Problems module writes rows.** The other six own migrated tables that nothing reads or
  writes. The next to change is Workspaces, in
  [#10](https://github.com/shoraLBRT/ritocode/issues/10).
- **No authentication.** Endpoints are anonymous. `AllowAnonymous()` on health and meta is
  deliberate so they keep working once authentication is switched on in
  [#6](https://github.com/shoraLBRT/ritocode/issues/6).
- **The object storage client puts and gets, and does nothing else.**
  [#5](https://github.com/shoraLBRT/ritocode/issues/5) stays open for the three operations left out,
  each because its first real caller decides its shape:
  **deletion and prefix listing** go together — deleting a prefix reference is a list-then-delete —
  and belong to retention, deferred with
  [#43](https://github.com/shoraLBRT/ritocode/issues/43);
  **server-side copy**, which [STORAGE_LAYOUT.md](STORAGE_LAYOUT.md) requires at enqueue to freeze
  the workspace tree, arrives with [#14](https://github.com/shoraLBRT/ritocode/issues/14). Put and
  get now have a real caller: ingest writes problem bundles, and nothing reads one back until
  [#10](https://github.com/shoraLBRT/ritocode/issues/10) materialises a workspace from one.
- **Object storage has no readiness check.** `AddObjectStorage` registers a client that contacts
  nothing at startup, so `/health/ready` still reports one check per module schema and no more.
  Adding a storage check would make `dotnet test` and a bare `dotnet run` require MinIO — the
  property [#37](https://github.com/shoraLBRT/ritocode/issues/37) spent a session buying back — so
  it waits for the first endpoint that cannot serve a request without an object.
- **Cross-module references carry no foreign key**, by design — see
  [ADR 0004](adr/0004-persistence-and-migrations.md). Whichever module creates such a row is
  responsible for validating the reference first.

---

## Next up

The slice plan is the ordered list now: **[`docs/SLICE_PLAN.md`](SLICE_PLAN.md)**. Take the first
unticked box. The stages there are ordered so that each depends only on stages above it.

**Stage 1 is complete**, and stage 2 is three boxes in: the storage key layout, the client that
reads and writes those keys, and now the catalog and the ingest that fills it. The next boxes:

1. **[#42](https://github.com/shoraLBRT/ritocode/issues/42) (partial) — three problems**, which
   cannot start until the language below is chosen. The machinery is now waiting for it: ingest
   publishes whatever packages are in the content directory, so the remaining work in #42 is
   authoring, not plumbing.
2. **[#26](https://github.com/shoraLBRT/ritocode/issues/26) — frontend shell and API client**, and
   then **[#31](https://github.com/shoraLBRT/ritocode/issues/31) (partial) — frontend CI job**.
   Neither is blocked by the language decision, and the catalog they will call now exists.

One decision falls due now and is the maintainer's, not a session's: **the language of the first
problems**, which [#42](https://github.com/shoraLBRT/ritocode/issues/42) cannot start without. It is
the first entry under [Open questions](#open-questions). Nothing else in stage 2 is blocked by it —
#26 and #31 can be taken first.

The three ADRs written so far are off this list and their obligations are in
[Open questions](#open-questions) instead. Briefly: submission reports gain somewhere to carry a
timeout or a resource exhaustion, #22 gains a runner registry, and #10 gains two lookup interfaces
plus the three architecture-test assertions that keep them honest.

[#37](https://github.com/shoraLBRT/ritocode/issues/37) is off this list: the harness landed, and
the flow tests the issue also asks for arrive with the endpoints they exercise.
[#8](https://github.com/shoraLBRT/ritocode/issues/8) is off it because it is done.

[#9](https://github.com/shoraLBRT/ritocode/issues/9) is off this list and stays open: the catalog
reads, and search, facets, filters and explicit version resolution are stage two. The rest of
Phase 1 is [after the slice](SLICE_PLAN.md#after-the-slice).

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
  [ADR 0006](adr/0006-sandbox-execution-model.md) §3, was due in stage 2 with
  [#9](https://github.com/shoraLBRT/ritocode/issues/9); moved to stage 5 with
  [#22](https://github.com/shoraLBRT/ritocode/issues/22), which is the first point it can exist.*
  `--network none` means the runner image's warmed package cache is the entire set of dependencies
  a problem may have. A problem outside it can never be evaluated by anyone, so the rejection
  belongs at ingest. Ingest now exists and does **not** do this, because neither side of the
  comparison does: a `problem.yaml` declares no dependencies —
  [PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md) has no field for them, and reading them out of
  a `.csproj` would be the language-specific branch ADR 0006 §2 exists to keep out of shared code —
  and there is no runner image or registry to name a cache. Implementing it before #22 would mean
  inventing both. Two things a session taking #22 inherits: the manifest needs a dependency
  declaration or the runner registry needs to expose its cache in a form ingest can read, and
  whichever it is, `IProblemIngest.IngestAsync` gains its first expected failure and returns a
  `Result<T>` — it returns the ingested version directly today because there is nothing yet that a
  valid package can be rejected for. Until then the failure still happens, as a failed compile
  validator at submission time, blamed on the submitter rather than on the content.
- **How a revised problem gets republished.** *Created by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9), due in stage 2 with
  [#42](https://github.com/shoraLBRT/ritocode/issues/42).* Ingest adds a version every time it is
  called and never replaces one, which is right — a published version is what a workspace was
  created from. The seeder therefore has to decide when *not* to call it, and its rule is the
  crudest one that is safe: skip a slug that already has a published version. The consequence is
  that editing a package and restarting changes nothing, silently, and there is no other way to
  publish revision 2. #42 is where that starts to hurt, and the answer is probably a content
  digest on `problem_versions` so the seeder can tell "already published" from "published, but not
  this content" — which is a column, and therefore a migration, and therefore a decision rather
  than a detail.
- **What the API says beyond the error body and the page envelope.** *Settled by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9), the first module endpoints.* Enums are
  serialised as camelCase names host-wide, not ordinals — a number would make every client depend
  on a C# enum's member order, and inserting a member in the middle would change what existing
  clients read without changing a single response shape. The catalog's list order is newest first,
  with the id breaking ties: the id is a UUIDv7, so it agrees with `created_at` and makes the sort
  total, without which two rows created in the same instant could swap places between two requests
  for the same page. Neither is in [ADR 0003](adr/0003-api-conventions.md); both belong there if a
  second endpoint has to restate them.
- **The migrator composes host infrastructure it does not use.** *Created by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9), settled for now.* `Ritocode.DbMigrator`
  calls `AddObjectStorage` before `AddModules`. It needs no object storage to apply a migration,
  but it composes the same module set the API does, and a module offering a service that depends on
  `IObjectStore` cannot be composed into a host that has none — the container validates that on
  build, and design-time `dotnet ef` fails first. Registration contacts nothing and every setting
  has a default, so this adds no requirement to migrating. If a third host ever appears, the
  alternative worth weighing is splitting `IModule.RegisterServices` into persistence and the rest,
  so a host can compose only what it needs.
- **A submission's evaluated tree has nowhere to be recorded.** *Created by
  [STORAGE_LAYOUT.md](STORAGE_LAYOUT.md), due in stage 4 with
  [#14](https://github.com/shoraLBRT/ritocode/issues/14).* An evaluation reads a frozen copy at
  `evaluation-artifacts/submissions/{id}/input/tree.tar.gz`, never the live workspace key — that key
  is overwritten on every save, so evaluating from it means re-evaluating one submission reads
  different bytes, and the determinism claim fails underneath anything ADR 0006 guarantees. But
  `submissions` has no reference column, so this is the single key derived from an id instead of
  read back from a row, against the layout's own rule. #14 should add the column while it builds the
  lifecycle; until it does, moving that part of the layout strands existing rows.
- **Where a submission report carries a timeout or a resource exhaustion.** *Created by
  [ADR 0006](adr/0006-sandbox-execution-model.md) §5, due in stage 4 with
  [#14](https://github.com/shoraLBRT/ritocode/issues/14) and
  [#17](https://github.com/shoraLBRT/ritocode/issues/17).* The runner distinguishes `Completed`,
  `TimedOut`, `ResourceExhausted` and `Crashed`, and is explicitly allowed not to know which of the
  last two applies — `OOMKilled` is a reliable positive and an unreliable negative, since a managed
  `OutOfMemoryException` aborts at 134 before the kernel is involved. If the schema above the runner
  has nowhere to put that distinction, the honesty is discarded on the way up and a person is told
  their tests failed when the container was killed.
- **How the reference form is enforced at the database.** *Settled by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9), which wrote the first reference; adopted
  for one column of three.* An EF value converter, `StorageReferenceConverter` in
  `Ritocode.Shared.Persistence`. The property is typed as `StorageReference`, so no code path can
  put an arbitrary string in the column, and a value this build cannot resolve throws where it is
  read rather than reaching a caller that assumed it parsed. A check constraint on the role prefix
  was the alternative and buys little the converter does not: the writes it would catch are the
  ones the converter makes unexpressible. **`workspaces.snapshot_reference` and
  `submission_reports.logs_reference` are still `string`.** Converting them was left to their first
  writers — [#12](https://github.com/shoraLBRT/ritocode/issues/12) and
  [#23](https://github.com/shoraLBRT/ritocode/issues/23) — rather than done speculatively here, and
  both tables are empty, so it stays a two-line change until they are not. Adopting it needs no
  migration: the store type and width are unchanged, and `has-pending-model-changes` reported no
  drift.
- **Who creates the buckets in a deployment?** *Created by the storage client, due before anything
  is deployed.* `compose.yaml` creates the three local buckets with `mc mb` and `MinioTestServer`
  creates a set per test class, so both environments that exist today are covered by accident of
  their own setup. A real deployment has neither, and a missing bucket surfaces as a failed put at
  the first ingest rather than at startup — the client validates bucket *names* at startup and
  cannot check existence without a network call. Options are a migrator-style one-shot step
  alongside `Ritocode.DbMigrator`, infrastructure-as-code outside the application, or a create-if-
  absent on first use, which is the tempting one and the wrong one: it needs bucket-creation rights
  in the running service's credentials forever.
- **The shape of the object storage client.** *Settled.* Put and get, addressed by
  `StorageReference`; a read copies into a destination the caller supplies rather than returning a
  stream, so nothing has to decide who disposes the HTTP response and no version of the interface
  buffers a whole archive to avoid that question. A missing object is `false`, not an exception and
  not an `AppError`: the module that owns the row pointing at it is the one entitled to choose the
  error code a client branches on, which is [ADR 0007](adr/0007-cross-module-contract-form.md)'s
  reasoning applied to infrastructure. Transport, permission and bucket faults throw
  `ObjectStoreException`, so no AWS SDK type reaches a caller. The client lives in
  `Ritocode.Shared` because [ADR 0002](adr/0002-modular-monolith-layout.md) rule 1 leaves a module
  nowhere else to reference; the cost is that the AWS SDK is now on every module's transitive
  reference list, the same way EF Core already is.
- **Test database isolation.** *Settled.* One PostgreSQL container per test assembly, one database
  per test class, each copied from a template that `MigrationRunner` migrated once. Isolation is
  per database rather than per transaction because a test that wants to see what a migration, a
  trigger or a check constraint actually did cannot see it inside a transaction the harness rolls
  back. Consequence: `dotnet test` now needs a Docker daemon, and no longer needs `dev-up`.
- **Who validates cross-module references, and how?** *Settled by
  [ADR 0007](adr/0007-cross-module-contract-form.md), due in stage 3 with
  [#10](https://github.com/shoraLBRT/ritocode/issues/10).*
  [ADR 0004](adr/0004-persistence-and-migrations.md) says the module creating the row does; ADR 0002
  said the contract lives in `Ritocode.Shared`; ADR 0007 fixes its shape. Thin read-interfaces, one
  per consumer need, taken as constructor parameters — so a cross-module dependency is visible in a
  signature, which a mediator would have hidden while still passing `ModuleBoundaryTests`. Contracts
  answer facts, never policy: they return the row's summary or `null`, never a `Result<T>`, because
  an `AppError` supplied by the owning module means the wrong module chose the error code a client
  branches on. Read-only for the slice. Three consequences a later session inherits: a contract call
  is outside the caller's transaction, so check-then-write races and is meant to — ADR 0004 gave up
  referential integrity on purpose; a per-row call in a list endpoint is an N+1, and the fix is a
  batch method on the contract, never a cross-schema join; and routing through an interface turns a
  missing DI registration into a startup failure, which is what ADR 0007 §7's third assertion exists
  to move back into `dotnet test`.
- **How does one module *change* another's state?** *Open, and outside the slice.* ADR 0007 is
  read-only by decision, so there is no mechanism and no need for one yet. The first real case is
  user deletion in [#43](https://github.com/shoraLBRT/ritocode/issues/43), which
  [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) already says must notify each module rather than run a
  single `DELETE`. Whether the answer is a command interface or a domain event is worth deciding
  against that case rather than in advance; it supersedes ADR 0007 rather than editing it.
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
| `GET /api/v1/problems` | `200`, the page envelope — `items`, `pageNumber`, `pageSize`, `totalItems`, `totalPages`, `hasNextPage`, `hasPreviousPage` |
| `GET /api/v1/problems?pageSize=1000` | `400`, `application/problem+json`, `code: "validation_failed"`, `errors.pageSize` present |
| `GET /api/v1/problems/no-such-problem` | `404`, `code: "problem_not_found"` |
| any response | carries an `X-Request-Id` header |

The command above runs without a launch profile, so the host starts in **Production** — content
seeding is off there and `items` is empty. To see the catalog with content in it, start the host in
Development instead, which turns seeding on and publishes the packages under `content/problems`:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Ritocode.Api --no-launch-profile --urls http://127.0.0.1:5199
```

| Request | Expected |
| --- | --- |
| `GET /api/v1/problems` | `200`, `totalItems: 1`, one item with `slug: "example-order-total"`, `difficulty: "medium"`, `version: 1` |
| `GET /api/v1/problems/example-order-total` | `200`, the same fields plus `description` and a `problemVersionId` |

Seeding needs the MinIO from `dev-up`, and the reference package is the only content there is until
[#42](https://github.com/shoraLBRT/ritocode/issues/42) — it is a fixture that a development host
also publishes so there is something to browse, not the Phase 1 problem set.

The host reads `Database:ConnectionString`; locally it comes from `Database__ConnectionString`,
which `scripts/dev-up` prints the value for. The tests configure themselves from the container the
harness starts, so they need no environment variable at all.

Current baseline: **230 tests** — 110 shared, 90 problems, 25 API, 5 architecture.
A session that leaves this number lower than it found it has broken something.

Three of the four test assemblies now need a Docker daemon: the shared assembly starts MinIO, the
API assembly starts PostgreSQL, and the Problems assembly starts both — its ingest and catalog
tests write rows and objects for real. Only `Ritocode.Architecture.Tests` runs without one.
