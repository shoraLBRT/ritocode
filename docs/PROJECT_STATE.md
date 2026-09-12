# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-12
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
| Frontend | React + Vite + TypeScript in `frontend/`, shell and API client only |

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
  problems/                   problem packages: three authored C# problems and the format's
                              reference package, all validated from the committed tree by tests
frontend/                     React + Vite + TypeScript. src/api is the only code that knows the
                              backend exists; src/hooks, src/components, src/pages and routes.tsx
                              are the shell around it. Its own README covers running it
src/
  Ritocode.Api/               composition root: pipeline, config, health, meta, module wiring
  Ritocode.DbMigrator/        applies each module's migrations; the host never migrates itself
  Ritocode.Shared/            errors, Result<T>, paging, IModule, correlation, persistence base
                              Storage/ holds the object storage client and the key layout as code
                              Identity/ holds the identity seam: ICurrentUser, the claim it reads
                              and the development identity settings two modules share
  Modules/Ritocode.Modules.*  one project per module: domain, DbContext, migrations
                              Problems also owns Packaging/ (the problem package format),
                              Ingest/ (package -> published version + bundle) and Catalog/
                              Auth owns the authentication scheme; Users owns the row behind the
                              development identity that scheme asserts
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
| [#26](https://github.com/shoraLBRT/ritocode/issues/26) Frontend shell | Partial | React + Vite + TypeScript in `frontend/`. `ApiClient` is the only code that calls `fetch`, and `api/errors.ts` is the only code that reads the ADR 0003 envelope: a failure reaches a screen as an `ApiError` carrying the stable `code`, kept apart from a status with no envelope behind it and from a server that never answered. `useApiResource` reports one request as a discriminated union. Layout, routes, and the loading / error / empty panels, on 55 tests over a stubbed `fetch` | `frontend/` |
| [#31](https://github.com/shoraLBRT/ritocode/issues/31) CI pipeline | Partial | `backend-ci.yml` (build and test, formatting, migrations and drift) and now `frontend-ci.yml`: `npm ci`, then lint, build — the typecheck rides on it — and the 55 tests, on the Node line `frontend/.nvmrc` pins. The frontend job needs no backend, no database and no Docker | `.github/workflows/` |
| [#42](https://github.com/shoraLBRT/ritocode/issues/42) Initial problem set | Partial | Three authored C# problems — `split-the-invoice` (easy), `no-double-booking` (medium), `respect-the-precedence` (hard) — at three difficulties over three unrelated trees, each with a known-good and a known-bad fixture that disagree on behaviour the package's own tests pin. The catalog has content that is not the format's reference fixture for the first time | `content/problems/`, `tests/Ritocode.Modules.Problems.Tests/CatalogPackageTests.cs` |
| [#6](https://github.com/shoraLBRT/ritocode/issues/6) Authentication | Partial | The identity seam from [ADR 0008](adr/0008-authentication-seam.md), which is **Proposed** and needs the maintainer. `ICurrentUser` is one value wide and lives with the host infrastructure in `Shared/Identity`; the Auth module owns a real authentication scheme, so stage two swaps a handler rather than unpicking a mechanism; the Users module keeps the row the seeded identity names, which is the first row that module has written. The host is authenticated by default and anonymous by exception — a fallback policy protects any endpoint that states nothing — and a rejected request answers in the ADR 0003 error body rather than an empty 401. Login, session issuance and `/me` stay out | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth/Identity`, `src/Modules/Ritocode.Modules.Users/Identity`, `docs/adr/0008-authentication-seam.md` |

The frontend now exists as a shell: it renders the layout, resolves its routes, and reads the
catalog from a running host. It has no identity, no editor and no designed screens — those are
stages 3 and 6. It now lists four problems against a development host, and the descriptions arrive
as text rather than rendered Markdown, which is [#27](https://github.com/shoraLBRT/ritocode/issues/27).

Nothing else from the backlog is implemented. Five of the seven modules own a schema and a
`DbContext` and expose neither an endpoint nor a service — the boundary and the storage are in
place, the behaviour is not. Two are now awake in part: **Auth** owns the authentication scheme and
no endpoints, and **Users** writes exactly one row — the development identity's — and reads none.
**Problems is still the only module that is fully
alive**: it reads and writes its own schema, serves two endpoints, and is the first caller of
`IObjectStore`. Everything downstream of it is still empty — nothing creates a workspace from a
published version yet, which is [#10](https://github.com/shoraLBRT/ritocode/issues/10) in stage 3,
and it is the first code that will read a bundle back.

### Deliberately deferred

- **The problem set ([#42](https://github.com/shoraLBRT/ritocode/issues/42)) is three problems, and
  three is what the slice needs rather than what Phase 1 needs.** What the three buy is the thing
  the slice has to demonstrate: a verdict that separates a good answer from a bad one on unrelated
  tasks rather than on one. What is left is volume — and nobody has written down how much volume, see
  [Open questions](#open-questions) — plus the two things authoring walked into and could not finish
  here. **A revision to any of these three cannot reach the catalog**: the seeder publishes a slug's
  first version and skips a slug that already has one, so editing a package changes nothing,
  silently. And **nothing in this repository executes a fixture.** All nine combinations were run by
  hand while authoring — each starter fails, each known-good answer passes, each known-bad one fails,
  on `dotnet build --warnaserror` and `dotnet test` in a scratch directory — and the counts are in the
  pull request. That is evidence, not a guard. An author may run a package by hand; the **platform**
  may not run one outside a sandbox runner, which is the first row of ADR 0005's forbidden list — so
  the repeatable form of this check is stage 5's, not a test added here.
  `CatalogPackageTests` goes as far as reading files can — both fixtures exist, differ from each
  other, and each changes the starter tree — and
  [#38](https://github.com/shoraLBRT/ritocode/issues/38) is where they actually get run.
- **CI ([#31](https://github.com/shoraLBRT/ritocode/issues/31)) now checks both halves and ships
  nothing.** `frontend-ci.yml` closed the half that was waiting for a frontend to exist. What is
  left is the other axis entirely: no job publishes a build artifact, builds a container image,
  tags a release or deploys anything. None of that can be specified yet — the runner image is
  [#22](https://github.com/shoraLBRT/ritocode/issues/22) in stage 5 and there is nothing to
  release until the slice has a journey to release — so the issue stays open and unscheduled
  rather than becoming the next box. A CI job invented against an imagined deployment would be
  rewritten by the first real one.
- **The frontend still has no protected routes, and now for a settled reason rather than a waiting
  one.** [#26](https://github.com/shoraLBRT/ritocode/issues/26)'s acceptance criterion names them,
  and [ADR 0008](adr/0008-authentication-seam.md) §6 answers the question that blocked them: with a
  seeded development identity **every request is authenticated and there is no signed-out state in
  the browser**, so the guard **renders in place rather than redirecting** — there is no login to
  redirect to — and there is nothing yet for it to guard against. `ApiClient` still sends no
  credential, correctly: the handler asks for none. What changed is that
  `ApiError.isUnauthenticated` now has a real producer — a protected endpoint answers
  `code: "unauthenticated"` in the ADR 0003 body — so the branch is reachable the moment the
  development identity is switched off. The guard itself lands with the session that issues one.
- **`ProblemsPage` and `ProblemDetailPage` are wiring, not screens.** They exist so the page
  envelope, the query parameters and the error body are proved to survive the trip end to end, and
  [#27](https://github.com/shoraLBRT/ritocode/issues/27) in stage 6 replaces both. The description
  is rendered as text rather than Markdown for the same reason: choosing a renderer for content
  someone else authored is a decision that belongs with the designed screen.
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
- **Only Problems and Users write rows**, and Users writes exactly one: the development identity's,
  on startup, and never again — the seeder finds the row on a restart rather than adding a second.
  Nothing reads `users.users` yet; the first reader is `IUserLookup` in the next box. The other five
  own migrated tables that nothing touches, and the next to change is Workspaces, in
  [#10](https://github.com/shoraLBRT/ritocode/issues/10).
- **Authentication is a seam, not a feature.** The host authenticates — a real scheme, a fallback
  policy, a 401 in the unified error body — and the only identity it can assert is the seeded
  development one that ADR 0005 allows in place of a login. Enabled, it authenticates **every**
  request as one fixed user and checks no credential; it is off by default and logs a warning naming
  the environment when it is on outside Development. What is missing is the half
  [#6](https://github.com/shoraLBRT/ritocode/issues/6) names and stage two owns: a login endpoint,
  session issuance, and `/me`. `/me` is the cheapest of the three and, unlike the other two, does
  **not** depend on the token-format decision — see [Open questions](#open-questions).
  `AllowAnonymous()` on health, meta and the catalog is now load-bearing rather than anticipatory,
  and pinned by tests against a host with no identity.
- **Authorisation is only "is authenticated".** Nothing checks that a resource belongs to its
  caller, because nothing owns a resource yet. That is
  [#35](https://github.com/shoraLBRT/ritocode/issues/35) in stage 3, and ADR 0005 is explicit that
  it is not hardening to be deferred — without it the identity seam is decorative. It ships with the
  endpoints it guards, not after them.
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

**Stages 1 and 2 are complete, and stage 3 has started** with the identity seam. What that box left
in place for everything after it: every endpoint takes its user from `ICurrentUser`, and one that
says nothing about authorisation is protected rather than open. **The next box is:**

1. **Cross-module contract in `Ritocode.Shared`.** Per
   [ADR 0007](adr/0007-cross-module-contract-form.md): `IUserLookup` and `IProblemVersionLookup`,
   thin and read-only, each returning the row's summary or `null`. The three architecture-test
   assertions in ADR 0007 §7 ship **in that PR**, not after — assertion 3 is what turns a missing DI
   registration back into a test failure instead of a startup failure. Two things it now inherits:
   the development identity's row is the first user `IUserLookup` can be pointed at, and
   `Shared/Identity` is deliberately **not** under `Shared/Contracts` — `ICurrentUser` is ambient
   request state with no owning module, so assertion 3 must not sweep it up.

Then [#10](https://github.com/shoraLBRT/ritocode/issues/10) — the first code that reads a problem
bundle back out of object storage, and the first endpoint the fallback policy actually protects.

The three ADRs written so far are off this list and their obligations are in
[Open questions](#open-questions) instead. Briefly: submission reports gain somewhere to carry a
timeout or a resource exhaustion, #22 gains a runner registry, and #10 gains two lookup interfaces
plus the three architecture-test assertions that keep them honest.

[#26](https://github.com/shoraLBRT/ritocode/issues/26) is off this list and stays open: the shell
and the API client landed, and the protected routes its acceptance criterion asks for now have their
answer rather than their blocker — the guard renders in place and waits for a session to guard
against, per [ADR 0008](adr/0008-authentication-seam.md) §6.
[#6](https://github.com/shoraLBRT/ritocode/issues/6) is off it and stays open too: the seam landed,
and the login, session issuance and `/me` its acceptance criteria name did not.

[#37](https://github.com/shoraLBRT/ritocode/issues/37) is off this list: the harness landed, and
the flow tests the issue also asks for arrive with the endpoints they exercise.
[#8](https://github.com/shoraLBRT/ritocode/issues/8) is off it because it is done.

[#9](https://github.com/shoraLBRT/ritocode/issues/9) is off this list and stays open: the catalog
reads, and search, facets, filters and explicit version resolution are stage two.
[#42](https://github.com/shoraLBRT/ritocode/issues/42) is off it and stays open too: the three
problems the slice needs exist, and volume, republishing and the dependency check do not. The rest of
Phase 1 is [after the slice](SLICE_PLAN.md#after-the-slice).

---

## Open questions

Decisions a future session will hit, and where in the slice each one comes due.

- **What a verdict of `compile` plus `test` can actually grade.** *Found while authoring
  [#42](https://github.com/shoraLBRT/ritocode/issues/42); relaxes in stage two.* The slice grades a
  submission with two validators and nothing else, and both of them measure behaviour. A pure
  refactoring task — tangled code whose tests already pass, which is the shape of the reference
  package — therefore scores an **untouched workspace** 100, and scores a beautiful answer the same.
  That is not a content flaw; it is what this validator set can see. So the three problems of #42 each
  start from code that **fails at least one of its own tests**: the prose asks for the refactoring,
  the failing test is the part that can be checked, and every `description.md` says which is which
  under *What is graded* — the spec's "honest about scope" rule applied to the grader rather than to
  the task. The rule to carry: **until a validator grades quality, a problem whose starter passes is
  not a problem.** What changes it is the lint and patch-scope validators in
  [#19](https://github.com/shoraLBRT/ritocode/issues/19), after the slice; pure refactoring tasks
  become authorable in the same breath, and this constraint can be dropped from `content/README.md`.
  The slice review should also look at the other end of it: three tasks that all hide a defect is a
  product that reads as bug-hunting, and the product claim is about improving code.
- **How many problems Phase 1 needs.** *Created by
  [#42](https://github.com/shoraLBRT/ritocode/issues/42), due at the slice review.* Three is what the
  slice argues for and is written down in `SLICE_PLAN.md`; the size of the Phase 1 set is written
  nowhere — not in the issue, not in `MVP_SCOPE.md`. It is not a blocker, because volume is additive
  and each package is independent, but it is the sort of number that gets invented by whoever next
  opens #42 unless the maintainer says it. Worth answering with the test result in hand rather than
  before: how many tasks someone works through before they stop is a thing the slice is being put in
  front of people to find out.
- **Language of the first problems.** *Was due in slice stage 2 and blocked
  [#42](https://github.com/shoraLBRT/ritocode/issues/42); now spent.* **Decided by the maintainer on
  2026-09-11: C#**, and the three problems of #42 are written in it. The pool of testers is the thing being traded away, and the thing bought is
  that every part of the evaluation path is one you can debug — `dotnet build` and `dotnet test`
  are very nearly the compile and test validators themselves, the reference package already proved
  both under the full ADR 0006 flag set in the sandbox spike, and the first runner image in
  [#22](https://github.com/shoraLBRT/ritocode/issues/22) is a toolchain you already run. The
  decision is narrow and reversible by addition: `language` is a manifest field, and a second
  language is a row in the runner image registry rather than a change to anything above it — which
  is what [#22](https://github.com/shoraLBRT/ritocode/issues/22)'s image matrix is, after the
  slice. What it does **not** license is authoring three tasks that only differ in surface: the
  point of three rather than one is showing the verdict separates a good solution from a bad one
  rather than being tuned to a single task, so each needs a known-good and a known-bad fixture that
  genuinely disagree.
- **The frontend duplicates the API's types by hand.** *Created by
  [#26](https://github.com/shoraLBRT/ritocode/issues/26), settled for now.* `frontend/src/api/types.ts`
  transcribes the C# records rather than generating from the OpenAPI document the API already
  produces when `Api:EnableOpenApi` is on. The surface is two endpoints plus meta, and generating
  would put a running backend on the critical path of a frontend build — including CI's, where the
  frontend job in [#31](https://github.com/shoraLBRT/ritocode/issues/31) would then need a database
  to typecheck. The cost is that a contract change is caught by a test rather than by the compiler:
  `frontend/src/test/responses.ts` holds bodies copied from the verification table above, which is
  the thread that breaks first. Worth reversing when the surface is big enough that transcribing it
  is the slower half — the generator replaces that one file and nothing that imports it.
- **Where the frontend gets a signed-in user.** *Created by
  [#26](https://github.com/shoraLBRT/ritocode/issues/26), answered by
  [#6](https://github.com/shoraLBRT/ritocode/issues/6) and
  [ADR 0008](adr/0008-authentication-seam.md) §6; the code lands with a real session.* The guard
  **renders in place rather than redirecting**, because a redirect assumes a login route and the
  seeded identity has none. It is not written yet for the reason that settles the shape: with the
  development identity enabled every request is authenticated, so there is no signed-out state for a
  guard to detect. `ApiClient` still sends no credential, and that is correct rather than
  outstanding — the handler asks for none, and `ApiClient` stays the single place one is added.
  `ApiError.isUnauthenticated` now has a real producer and is reachable the moment the development
  identity is off, which is what a stage-two session will find when it writes the guard.
- **A React 19 lint rule forbids `setState` in an effect body, and the ordinary fetch-in-effect
  shape trips it.** *Created by [#26](https://github.com/shoraLBRT/ritocode/issues/26), settled.*
  `useApiResource` does not switch itself to `loading` from inside its effect. Instead a settled
  result records the deps it was issued for, and `loading` is derived during render by comparing
  them — so the very first render after a dep change already reports `loading`. This started as a
  way to satisfy `react-hooks/set-state-in-effect` and turned out to be the more correct shape: the
  effect version leaves one committed frame showing the previous problem's data under the new
  problem's url. `useApiResource.test.tsx` asserts the absence of that frame. Two nearby dead ends,
  so the next person does not walk into them: `useMemo` for the same purpose is rejected by
  `react-hooks/use-memo` when the dependency list is not a literal, and a ref compared during
  render is rejected by `react-hooks/refs`.
- **A hook wrapping `useEffect` cannot memoise its callback on the caller's deps.** *Created by
  [#26](https://github.com/shoraLBRT/ritocode/issues/26), settled, and it cost a failing test to
  find.* `useCallback(load, deps)` returns the *same* function when `load` is referentially stable,
  whatever `deps` did — so a caller passing a stable function would silently never reload. It works
  by accident for the usual inline arrow. `useApiResource` therefore holds the latest closure in a
  ref refreshed by an effect, and lets `deps` alone decide when to run.
- **CI runs the frontend on a newer Node than the development machine.** *Created by
  [#31](https://github.com/shoraLBRT/ritocode/issues/31), settled, and worth knowing before a
  confusing red build.* There are two Node numbers and they are different on purpose.
  `engines.node` in `frontend/package.json` is the **floor** — `>=22.22.0`, what the dependencies
  themselves demand — and `frontend/.nvmrc` is the **line CI and `nvm use` resolve to**, currently
  `22`, so both land on the newest 22 LTS. Pointing `setup-node` at `engines` instead was the
  obvious single-source-of-truth move and is wrong: it resolves the `>=` range to the newest Node
  in existence, so the next major release would redden a build nobody had touched. The live
  consequence is that the development machine is on **22.17**, below the floor: `npm ci` warns
  `EBADENGINE` and works, and "it passed locally" is therefore a slightly weaker statement than
  "it passed in CI". Raising the machine past 22.22 removes the gap; until then, a frontend
  failure that reproduces nowhere locally is worth checking the Node version for first.
- **No workflow has a `paths:` filter, and that is a decision rather than an omission.** *Created
  by [#31](https://github.com/shoraLBRT/ritocode/issues/31).* Filtering `frontend-ci.yml` to
  `frontend/**` is the obvious saving and it breaks the moment either workflow becomes a required
  check: a workflow skipped by a path filter reports **no status at all**, not a passing one, so a
  PR that misses the filtered paths can never satisfy the requirement and sits pending forever.
  The documented escape is a second, near-duplicate job that reports success for the skipped case,
  which costs more than the runner minute it saves. Worth reopening only if the frontend job grows
  slow enough to be felt.
- **Session tokens: JWT or opaque plus a server-side store?** *Due in stage two, and now measurably
  invisible during the slice* — the seam in [ADR 0008](adr/0008-authentication-seam.md) hides it, and
  nothing built on top of it names a token. Opaque tokens make revocation trivial, which matters once
  submissions can open real pull requests in Phase 3. Still worth an ADR before #6 is completed, or
  the choice gets made by whoever writes the endpoint. **The maintainer's call.**
- **`/me` is the cheapest half of [#6](https://github.com/shoraLBRT/ritocode/issues/6) left, and it
  is not blocked by the question above.** *Found while building the seam; deferred to stage two by
  the plan.* `SLICE_PLAN.md` groups `/me` with login and session issuance, which genuinely wait for
  the token decision — `/me` does not: it reads `ICurrentUser` and returns the row `IUserLookup` will
  already fetch from the next box. It stayed out because the plan said so and nothing in the slice
  needs it, not because it is hard. The cost of it being out is small but real: **no endpoint in the
  running host requires authentication yet**, so the fallback policy and the 401 body are exercised
  only by tests over a probe endpoint until
  [#10](https://github.com/shoraLBRT/ritocode/issues/10) lands the first protected product endpoint.
- **The development identity is not refused outside Development.** *Decided in
  [ADR 0008](adr/0008-authentication-seam.md); revisit when a real session provider lands.* Enabled,
  it authenticates every request as one fixed user with no credential — which is precisely what
  ADR 0005 wants when the slice goes in front of people on a deployed host that has no login, and an
  authentication bypass in every other reading. It is off by default and logs a warning naming the
  environment. Once a real provider exists the argument for tolerating it evaporates, and refusing to
  start becomes the right answer; nothing will prompt that change except this entry.
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
- **`validator_config` does not round-trip byte for byte.** *Found by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9), which wrote the first one.* The column is
  `jsonb`, and PostgreSQL normalises `jsonb` on write: it reorders an object's keys and rewrites the
  whitespace. So the canonical bytes `ValidatorPipeline.ToJson()` produces are **not** the bytes a
  read returns, and a test asserting string equality between them fails — correctly. The property
  the spec actually claims survives, because the normalisation is itself deterministic: the same
  manifest still stores the same value, and array order — which the pipeline's execution order
  rides on — is preserved, which `TheStoredValidatorConfig_KeepsThePipelineInOrder` asserts. The
  consequence to carry: **a content digest over the pipeline has to be computed before the write,
  from `ToJson()`, never from the column** — which is exactly what the republishing question below
  will want. Storing it as `json` instead of `jsonb` would preserve the bytes and give up the
  indexing [ADR 0004](adr/0004-persistence-and-migrations.md) chose `jsonb` for; it is not worth
  reopening until something needs the bytes.
- **How a revised problem gets republished.** *Created by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9); was due with
  [#42](https://github.com/shoraLBRT/ritocode/issues/42) and is now live rather than theoretical.*
  Ingest adds a version every time it is called and never replaces one, which is right — a published
  version is what a workspace was created from. The seeder therefore has to decide when *not* to call
  it, and its rule is the crudest one that is safe: skip a slug that already has a published version.
  #42 did not hit this, because three new slugs are three first versions. **The next edit to any of
  them does**: a development database that has published `split-the-invoice` at version 1 will ignore
  every later change to that package, with no error and no log line saying the content moved. Anyone
  authoring against a host they have already run has to clear the problem's rows first, and nothing in
  the product does that — `docker compose down -v` is the blunt version and deletes the bundles too.
  The answer is probably a content digest on `problem_versions` so the seeder can tell
  "already published" from "published, but not this content" — which is a column, therefore a
  migration, therefore a decision rather than a detail. Note the constraint from the `validator_config`
  entry above: such a digest is computed from `ToJson()` before the write, never read back from the
  `jsonb` column.
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

Every endpoint above says `AllowAnonymous()`, which is now load-bearing: the host protects anything
that does not. **No endpoint in the running host requires authentication yet** — the first is
[#10](https://github.com/shoraLBRT/ritocode/issues/10)'s — so the seam is observed in the log and in
the database rather than over HTTP:

| Where | Expected |
| --- | --- |
| the startup log, in Production | no development identity line at all — it is off outside `appsettings.Development.json` |
| the startup log, in Development | `Seeded the development identity as user 0199aa00-…-000000000001 (developer)`, then `already exists as user …` on every later start |
| `select count(*) from users.users` after two Development starts | `1` — the identifier is configuration, not generated, so a restart is not a second user |

The command above runs without a launch profile, so the host starts in **Production** — content
seeding is off there, `items` is empty, and so is the development identity. To see the catalog with content in it, start the host in
Development instead, which turns seeding on and publishes the packages under `content/problems`:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Ritocode.Api --no-launch-profile --urls http://127.0.0.1:5199
```

The content path is relative to the host's **content root**, which `dotnet run` sets to the project
directory rather than the repository root — which is why `appsettings.Development.json` says
`../../content/problems` and not `content/problems`. A path that resolves nowhere is a logged
warning naming the absolute path it tried, not a silent empty catalog.

| Request | Expected |
| --- | --- |
| `GET /api/v1/problems` | `200`, `totalItems: 4`, all at `version: 1` — `split-the-invoice` (`easy`), `respect-the-precedence` (`hard`), `no-double-booking` (`medium`) and `example-order-total` (`medium`), newest first |
| `GET /api/v1/problems/split-the-invoice` | `200`, the same fields plus `description` and a `problemVersionId` |

Seeding needs the MinIO from `dev-up`. Four packages rather than the three of
[#42](https://github.com/shoraLBRT/ritocode/issues/42): the fourth is `example-order-total`, the
format's reference fixture, which lives in the same directory and is therefore published too — see
[`content/README.md`](../content/README.md).

**If the catalog does not match this table, check whether the database already had these slugs.** The
seeder publishes a slug's first version and skips a slug that already has one, so an edited package is
ignored in silence — the republishing entry under [Open questions](#open-questions) has the detail and
the only way out is to clear the rows.

The host reads `Database:ConnectionString`; locally it comes from `Database__ConnectionString`,
which `scripts/dev-up` prints the value for. The tests configure themselves from the container the
harness starts, so they need no environment variable at all.

### The frontend

Run from `frontend/`. Node 22.22 or newer; `npm ci` once. None of these need a backend, a database
or Docker — the tests stub `fetch`.

```bash
npm run lint
```

```bash
npm run build
```

```bash
npm test
```

These three are exactly what `.github/workflows/frontend-ci.yml` runs, after `npm ci`, so the
frontend's CI result is reproducible from this directory with no extra setup. Note the Node
version gap under [Open questions](#open-questions): the job runs the newest 22 LTS, and the
development machine is below the floor `engines.node` declares.

To see it against a real host, start the API in Development as above and then:

```bash
npm run dev
```

The dev server binds port 5173 with `strictPort`, because that exact origin is the one
`appsettings.Development.json` allows through CORS. Changing the port on either side without the
other makes every request fail in the browser and succeed from `curl`.

| Page | Expected |
| --- | --- |
| <http://localhost:5173/> | The layout, and the seven modules listed under **Backend** |
| <http://localhost:5173/problems> | Four rows — `Split the invoice without losing a penny` (`Easy`), `Respect the precedence` (`Hard`), `Stop the double bookings` (`Medium`), `Untangle the order total calculator` (`Medium`) — each with its tags, and `Page 1 of 1` |
| <http://localhost:5173/problems/respect-the-precedence> | The title, the version and the description — as Markdown source, not rendered, which is [#27](https://github.com/shoraLBRT/ritocode/issues/27) |
| <http://localhost:5173/problems/no-such-problem> | "No such problem" — the `problem_not_found` branch, not the generic panel |
| <http://localhost:5173/nowhere> | "Page not found" |
| the same pages with the API stopped | The failure panel, saying the backend cannot be reached |

Current baseline: **275 backend tests, all passing** — 125 shared, 109 problems, 36 API,
5 architecture — and **55 frontend tests**, run separately by `npm test`. A session that leaves
either number lower than it found it has broken something.

The shared assembly rose from 110 to 125 and the API assembly from 25 to 36 with the identity seam
of [#6](https://github.com/shoraLBRT/ritocode/issues/6). The API assembly now boots **two** hosts: the
usual one, and `AnonymousTestApi` with the development identity switched off. That second host is
where everything about the authorisation policy is actually proved — with an identity enabled every
request passes, and a lost `AllowAnonymous` looks exactly like a correct host.

The problems assembly rose from 91 to 109 with the content of
[#42](https://github.com/shoraLBRT/ritocode/issues/42): `CatalogPackageTests` checks each committed
package as it ships, and most of its cases are theories over the content directory, so a fourth
catalog problem adds tests without anyone writing one.

Three of the four test assemblies now need a Docker daemon: the shared assembly starts MinIO, the
API assembly starts PostgreSQL, and the Problems assembly starts both — its ingest and catalog
tests write rows and objects for real. Only `Ritocode.Architecture.Tests` runs without one.
