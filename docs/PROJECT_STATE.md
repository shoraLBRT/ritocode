# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-13
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
                              Contracts/ holds the cross-module contracts of ADR 0007, namespaced
                              by the module that answers them: Users/IUserLookup,
                              Problems/IProblemVersionLookup,
                              Problems/IWorkspaceAllowanceLookup and
                              Workspaces/IOwnedWorkspaceLookup, with their records
  Modules/Ritocode.Modules.*  one project per module: domain, DbContext, migrations
                              Problems also owns Packaging/ (the problem package format),
                              Ingest/ (package -> published version + bundle) and Catalog/
                              Auth owns the authentication scheme; Users owns the row behind the
                              development identity that scheme asserts
                              Workspaces owns Lifecycle/: opening a workspace on a published
                              version, reading it back, and StarterTree — what a bundle becomes —
                              and Files/: the file tree, file read and file save, WorkspacePath
                              (the one path rule), SnapshotArchive (a snapshot read back as
                              untrusted, and rewritten one file at a time) and FileRevision
                              Submissions owns Lifecycle/: submitting a workspace, which freezes
                              its tree by server-side copy, reading an attempt and the history —
                              and Queue/: claiming attempts with SKIP LOCKED and recording a
                              result only on the claim that still holds the attempt
                              Evaluations owns Validators/: the plugin interface, the result
                              schema and the registry — and Sandbox/: the runner's result shape
                              from ADR 0006, ahead of the runner
                              Contracts/ in a module is its implementation of a Shared contract
tests/
  Ritocode.TestSupport/         integration test harnesses: a PostgreSQL container per test
                                assembly with a migrated database per test class, and a MinIO
                                container with a bucket set per test class
  Ritocode.Shared.Tests/        the shared primitives, and the storage client against a real MinIO
  Ritocode.Api.Tests/           in-memory host tests over the real composition root
  Ritocode.Architecture.Tests/  module boundary rules, the ADR 0007 contract rules, and the ownership
                                rule — a user's rows reached only where the owner is in the query,
                                read from the modules' IL — executable
  Ritocode.Modules.Problems.Tests/  the problem package format, the reference package, and
                                    ingest against a real PostgreSQL and MinIO
  Ritocode.Modules.Workspaces.Tests/  the starter tree a bundle becomes, the path rule and the
                                      snapshot archive, and the workspace lifecycle, file reads
                                      and file saves — the row lock included — against a real
                                      PostgreSQL and MinIO
  Ritocode.Modules.Submissions.Tests/ the submission transitions, the schema's half of them, and
                                      submitting, reading and listing against a real PostgreSQL
                                      and MinIO
  Ritocode.Modules.Evaluations.Tests/ the validator plugin interface, the result schema as literal
                                      JSON and the registry — no Docker, no database
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
| [#31](https://github.com/shoraLBRT/ritocode/issues/31) CI pipeline | Partial | `backend-ci.yml` (build and test, formatting, migrations and drift) and now `frontend-ci.yml`: `npm ci`, then lint, build — the typecheck rides on it — and the frontend tests, on the Node line `frontend/.nvmrc` pins. The frontend job needs no backend, no database and no Docker | `.github/workflows/` |
| [#42](https://github.com/shoraLBRT/ritocode/issues/42) Initial problem set | Partial | Three authored C# problems — `split-the-invoice` (easy), `no-double-booking` (medium), `respect-the-precedence` (hard) — at three difficulties over three unrelated trees, each with a known-good and a known-bad fixture that disagree on behaviour the package's own tests pin. The catalog has content that is not the format's reference fixture for the first time | `content/problems/`, `tests/Ritocode.Modules.Problems.Tests/CatalogPackageTests.cs` |
| [#6](https://github.com/shoraLBRT/ritocode/issues/6) Authentication | Partial | The identity seam from [ADR 0008](adr/0008-authentication-seam.md), which is **Proposed** and needs the maintainer. `ICurrentUser` is one value wide and lives with the host infrastructure in `Shared/Identity`; the Auth module owns a real authentication scheme, so stage two swaps a handler rather than unpicking a mechanism; the Users module keeps the row the seeded identity names, which is the first row that module has written. The host is authenticated by default and anonymous by exception — a fallback policy protects any endpoint that states nothing — and a rejected request answers in the ADR 0003 error body rather than an empty 401. Login, session issuance and `/me` stay out | `src/Ritocode.Shared/Identity`, `src/Modules/Ritocode.Modules.Auth/Identity`, `src/Modules/Ritocode.Modules.Users/Identity`, `docs/adr/0008-authentication-seam.md` |
| [#10](https://github.com/shoraLBRT/ritocode/issues/10) Create workspace from problem version | Done | `POST /api/v1/workspaces` opens the caller's workspace on a **published** version — 201 and a `Location` when it is new, 200 with the same workspace when the caller already has one on that version — and `GET /api/v1/workspaces/{id}` reads it back, answering another user's workspace as `workspace_not_found`. The first caller of both ADR 0007 contracts and the first code to read a bundle back: the starter tree under the version's `workspace_root`, re-rooted and regular files only, becomes the workspace snapshot. The owner comes from `ICurrentUser` and never the body. `problem_versions.workspace_root` is new, and `workspaces.snapshot_reference` is now a typed `StorageReference` | `src/Modules/Ritocode.Modules.Workspaces/Lifecycle`, `tests/Ritocode.Modules.Workspaces.Tests`, `tests/Ritocode.Api.Tests/Endpoints/WorkspaceEndpointsTests.cs` |
| [#11](https://github.com/shoraLBRT/ritocode/issues/11) Workspace file tree and file read | Done | `GET /api/v1/workspaces/{id}/files` answers every file with its size in bytes, ordered by path, as one object rather than a page; `GET /api/v1/workspaces/{id}/files/content?path=` answers one file as UTF-8 text, byte-order mark and line endings intact. The path is a query value so it reaches the API verbatim, and a path that could leave the tree is refused as `400` on `errors.path` before anything is looked up — never normalised. A path the tree does not hold is `workspace_file_not_found`, a file that is not UTF-8 is `409 workspace_file_not_text`, and another user's workspace is `workspace_not_found` on both. `WorkspacePath` is the one path rule, shared with the starter tree; `SnapshotArchive` reads a snapshot back as untrusted; `OwnedWorkspaces.FindOwnedAsync` is the owner-in-the-query lookup all three workspace reads now share. The frontend API client can open a workspace, list its files and read one | `src/Modules/Ritocode.Modules.Workspaces/Files`, `tests/Ritocode.Modules.Workspaces.Tests/Files`, `tests/Ritocode.Api.Tests/Endpoints/WorkspaceFileEndpointsTests.cs`, `frontend/src/api/endpoints.ts` |
| [#12](https://github.com/shoraLBRT/ritocode/issues/12) Workspace file write and draft persistence | Done | `PUT /api/v1/workspaces/{id}/files/content?path=` replaces an editable file's text, addressed exactly as a read is, and answers its new `sizeBytes` and `revision`; the next read — and the next open of the same version — returns the change. **Revision protection** is a per-file content hash: a read reports `revision`, the SHA-256 of the file's bytes, a save must send it back as `baseRevision`, and a file that moved on since answers `412 workspace_file_changed` rather than being overwritten. Saves to one workspace are serialised by a `FOR UPDATE` lock on its row, held from before the snapshot is read until `updated_at` commits, so two saves — even of different files — cannot drop each other's change. What a version allows reaches Workspaces through a third contract, `IWorkspaceAllowanceLookup`: ingest now stores `problem_versions.editable_files`, the manifest globs resolved against the starter tree, and the three limits. A file the version does not list is `403 workspace_file_read_only`, the tree marks each file `editable`, and saving exactly what is stored writes nothing. The frontend API client gained `saveWorkspaceFile` | `src/Modules/Ritocode.Modules.Workspaces/Files`, `src/Ritocode.Shared/Contracts/Problems/IWorkspaceAllowanceLookup.cs`, `src/Modules/Ritocode.Modules.Problems/Contracts/WorkspaceAllowanceLookup.cs`, `tests/Ritocode.Modules.Workspaces.Tests/Files/WorkspaceFileWriteTests.cs`, `tests/Ritocode.Api.Tests/Endpoints/WorkspaceFileWriteEndpointsTests.cs` |
| [#36](https://github.com/shoraLBRT/ritocode/issues/36) Workspace file handling and sandbox boundaries | Partial | The half a save needs, shipped in the same PR as #12. A path from a request is refused, never normalised, before anything is looked up — the #11 rule, now in front of a write. A save can only replace a file the snapshot already holds and the version lists as editable, so it can neither leave the tree nor add a link: the snapshot is rewritten as regular files only, and a snapshot holding anything else fails the save instead of being saved back clean. `max_file_bytes` is checked on the UTF-8 bytes (`400`, `errors.content`), `max_total_bytes` and `max_files` on the tree as it would be written (`409 workspace_limit_exceeded`), and text with no UTF-8 form is refused rather than stored as a replacement character | `src/Modules/Ritocode.Modules.Workspaces/Files/WorkspaceFiles.cs`, `src/Modules/Ritocode.Modules.Workspaces/Files/SnapshotArchive.cs` |
| [#14](https://github.com/shoraLBRT/ritocode/issues/14) Submission lifecycle and attempt history | Done | `POST /api/v1/submissions` queues an attempt at a workspace the caller owns — 201 and a `Location` — `GET /api/v1/submissions/{id}` reads it back, and `GET /api/v1/submissions` is the caller's history, newest first, in the page envelope, optionally at one `workspaceId`; another user's workspace or attempt answers exactly like a missing one. Submitting freezes the workspace tree by a **server-side copy** into `evaluation-artifacts`, written before the row commits and referenced by the new `submissions.input_reference`, so a save afterwards never changes what is graded. The transitions are `Submission.Start`, `Complete(score, at)` and `Fail(at)`, and every transition they allow is one `ck_submissions_completed_at_matches_status` accepts. `IObjectStore` gained `CopyAsync`, and Workspaces answers a fourth contract, `IOwnedWorkspaceLookup`, which takes the owner. Nothing runs an attempt yet — that is #15. The frontend API client gained `submitWorkspace`, `getSubmission` and `listSubmissions` | `src/Modules/Ritocode.Modules.Submissions`, `src/Ritocode.Shared/Contracts/Workspaces`, `src/Modules/Ritocode.Modules.Workspaces/Contracts/OwnedWorkspaceLookup.cs`, `tests/Ritocode.Modules.Submissions.Tests`, `tests/Ritocode.Api.Tests/Endpoints/SubmissionEndpointsTests.cs` |
| [#15](https://github.com/shoraLBRT/ritocode/issues/15) Queue and worker | Partial | The queue half, placed by [ADR 0009](adr/0009-evaluation-is-a-command-submissions-issues.md): the `submissions` table drained by the module that owns it. `ISubmissionDispatcher.ClaimNextAsync` takes the oldest `Queued` attempt — or a `Running` one whose claim is older than `Submissions:Queue:ClaimTimeout` — with `FOR UPDATE SKIP LOCKED`, and starts or reclaims it in one short transaction; `CompleteAsync` and `FailAsync` record only on the claim that still holds the attempt. The claim's identity is the new `submissions.started_at`, set by `Submission.Start(at)`, moved by `Reclaim(at)`, and held to its status by `ck_submissions_started_at_matches_status`. Concurrent claims never hand out an attempt twice, tested with twelve claimers against a real PostgreSQL. No loop drains the queue yet — that lands with the evaluator in #17 | `src/Modules/Ritocode.Modules.Submissions/Queue`, `tests/Ritocode.Modules.Submissions.Tests/Queue/SubmissionDispatcherTests.cs` |
| [#18](https://github.com/shoraLBRT/ritocode/issues/18) Validator plugin interface | Partial | `IValidatorPlugin` in the Evaluations module: `Plan` reads what to run from a step's `with`, `InterpretAsync` turns the runner's observation into a verdict, and a plugin never starts a process. `SandboxRunResult` is ADR 0006 §5's shape, declared ahead of #21. `ValidatorResult` is built only by `Judged`, `NotCompleted` and `Skipped`, so a run that did not complete is never a pass or a fail, and its checks are a sorted, duplicate-free projection. `ValidatorResults.ToJson` is the canonical `validator_results` JSON — the result schema — with nothing in it that differs between two runs. The registry maps a type to a plugin ordinally and refuses a duplicate or unnameable type. No plugin is registered yet, and the issue's three validators are two in the slice | `src/Modules/Ritocode.Modules.Evaluations/Validators`, `src/Modules/Ritocode.Modules.Evaluations/Sandbox`, `tests/Ritocode.Modules.Evaluations.Tests` |
| [#35](https://github.com/shoraLBRT/ritocode/issues/35) Backend security baseline | Partial | The ownership guard, as a rule rather than a habit. Every workspace endpoint already found its row with the owner inside the query; `OwnershipRuleTests` now fails when code in the Workspaces or Submissions module reaches an entity either context maps anywhere but an allowance that says where and why — `OwnedWorkspaces`, and the creation in `WorkspaceLifecycle.OpenAsync`. Submissions has no allowance, so its first endpoint meets the rule before it exists. The rule reads compiled IL, with the async state machines and lambda closures attributed to the method that was written, and is proved against six shapes of unguarded read. Rate limiting and input hardening stay out | `tests/Ritocode.Architecture.Tests/OwnershipRuleTests.cs`, `tests/Ritocode.Architecture.Tests/MethodBodyReferences.cs`, `src/Modules/Ritocode.Modules.Workspaces/Persistence/OwnedWorkspaces.cs` |

The frontend now exists as a shell: it renders the layout, resolves its routes, and reads the
catalog from a running host. It has no identity, no editor and no designed screens — those are
stages 3 and 6. It now lists four problems against a development host, and the descriptions arrive
as text rather than rendered Markdown, which is [#27](https://github.com/shoraLBRT/ritocode/issues/27).

Nothing else from the backlog is implemented. Progress still exposes neither an endpoint nor a
service. **Evaluations** now registers one — the validator plugin registry, empty until #19 — and owns
no schema, per ADR 0009. **Submissions is the third module that is alive**: it
writes its own schema and the frozen input trees in `evaluation-artifacts`, serves three protected
endpoints, and consumes `IUserLookup` and `IOwnedWorkspaceLookup` — but every attempt it creates stays
`Queued`, because nothing drains the queue until #15. **Auth** owns the authentication
scheme and no endpoints, and **Users** writes exactly one row — the development identity's — and
answers `IUserLookup`. **Problems** reads and writes its own schema, serves the catalog, and answers
`IProblemVersionLookup` and `IWorkspaceAllowanceLookup`. **Workspaces is the second module that is
fully alive**: it writes its own schema and the `workspace-snapshots` bucket, serves five protected
endpoints, consumes three cross-module contracts and answers a fourth. A workspace can be opened and
read back, its files listed, read and — the editable ones — saved, and submitted.

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
  workspace, submission — arrive with the endpoints they exercise. Problems, the opening of a
  workspace, and its file reads and saves now have theirs, over real PostgreSQL and MinIO; submission
  does not exist yet, and the issue stays open until it does.
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
- **Problems, Users, Workspaces and Submissions write rows**, and Users writes exactly one: the
  development identity's, on startup, and never again. Workspaces writes a row per opened workspace
  and moves its `updated_at` on every save that changes a file; Submissions writes a `Queued` row per
  attempt and nothing else — the transitions exist on the entity and have no caller until #15 and #17.
  Auth owns a migrated table that nothing touches, and so does Submissions' `submission_reports`.
- **The validator interface exists, and no validator does.**
  [#18](https://github.com/shoraLBRT/ritocode/issues/18) stays open for three things. **Its acceptance
  criterion is three validators on the interface**; the slice builds compile and test in
  [#19](https://github.com/shoraLBRT/ritocode/issues/19) and lint and patch scope are stage two. **An
  unknown `type` or a malformed `with` is not refused at ingest**: the manifest carries `with`
  uninterpreted and the registry lives in Evaluations, so today both surface when an attempt is
  evaluated — `Plan` returns a validation error — and are blamed on the attempt rather than the content.
  Refusing them at ingest means Problems asking Evaluations whether a step could run, a read contract
  ADR 0007 allows, and it belongs with the first real validator rather than ahead of it. And **no plugin
  reads a real artifact yet**: `InterpretAsync` is asynchronous because the test validator will parse a
  TRX from the output directory, and nothing does until #19.
- **The queue can be claimed and recorded on, and nothing drains it.**
  [#15](https://github.com/shoraLBRT/ritocode/issues/15) stays open for three things, each owned
  elsewhere on purpose. **The hosted loop** — claim, evaluate, record — lands with
  [#17](https://github.com/shoraLBRT/ritocode/issues/17), because ADR 0009 puts the evaluator behind a
  contract that cannot be registered before its implementation exists, and a loop that claimed without
  one would strand every attempt in `Running`. **The report** is written in the same transaction as the
  result, and its shape is [#18](https://github.com/shoraLBRT/ritocode/issues/18)'s, so `CompleteAsync`
  records a score and no report yet. **The cap on concurrent evaluations** is
  [#35](https://github.com/shoraLBRT/ritocode/issues/35)'s rate-limit box, and the drain is where it
  goes. One edge to carry: a claim times out after `Submissions:Queue:ClaimTimeout`, 15 minutes by
  default, and has to outlast the longest evaluation #17's deadline allows — a shorter one costs a
  second evaluation of the same input, never a wrong verdict, because the first worker's result is
  refused.
- **A submitted tree is frozen and not checked again.** The package spec says the limits of
  [#36](https://github.com/shoraLBRT/ritocode/issues/36) apply to a submitted tree, and
  [#14](https://github.com/shoraLBRT/ritocode/issues/14) does not apply them. The reason is where the
  code can live: Submissions may not read the snapshot archive — its format belongs to Workspaces —
  and copying the tree server-side is what keeps it out of the API process. The orchestrator of
  [#17](https://github.com/shoraLBRT/ritocode/issues/17) has to unpack the frozen tree to mount it
  into the runner anyway, and that is where the check costs nothing extra: a tree over its version's
  limits fails the attempt rather than being evaluated. Until then the only guard is the first one,
  and it is a real one — a tree can only reach a workspace through saves, and every save is checked.
  #36 stays open for it.
- **A workspace's files can be listed, read and saved — and a save only ever replaces.**
  [#12](https://github.com/shoraLBRT/ritocode/issues/12) is done against its own scope; what it left
  out on purpose is three things. **No file can be created, deleted or renamed**: a version stores
  its editable files resolved, so a path the starter tree did not hold was never matched by the
  globs and cannot be saved, and a refactoring that extracts a type into a new file has to put it in
  an existing one. What would lift it is under [Open questions](#open-questions). **Every list, read
  and save downloads the whole snapshot**, and a save uploads it again — the right cost for the
  kilobyte trees the slice's packages make, and not for the 100 MiB a package's limits permit. And
  **there is no endpoint listing a user's workspaces** — "continue where you left off" has its index
  and no reader, and the client that needs one is the stage 6 editor. The frontend API client has a
  function for each of the five workspace endpoints and no screen uses them yet; that screen is
  [#28](https://github.com/shoraLBRT/ritocode/issues/28).
- **The limits of [#36](https://github.com/shoraLBRT/ritocode/issues/36) hold for a save, and
  nowhere else yet.** The slice plan marks the box partial and the issue stays open, for three
  things. **A submitted tree is not checked again**, as the package spec says it must be: the tree a
  submission freezes does not exist until [#14](https://github.com/shoraLBRT/ritocode/issues/14), and
  since a tree can only reach a submission through saves, the check there is the second guard rather
  than the only one. **A request body is not capped below the server's 30 MB default** before it is
  parsed — a save over `max_file_bytes` is refused, but only after its JSON has been read. A
  per-endpoint request size limit derived from the format's 4 MiB file ceiling is the obvious shape,
  and belongs with the input-hardening half of [#35](https://github.com/shoraLBRT/ritocode/issues/35)
  after the slice. And **nothing guarantees a starter file is UTF-8** — the open question below; the
  loader is where that rule belongs. The issue's "no symlink traversal" needs no code of its own: a
  save touches no file system — the snapshot is rewritten in memory as regular tar entries — and a
  snapshot that holds a link already fails.
- **The contracts have single-id methods only.** ADR 0007 §6 names `FindManyAsync` as the answer to
  an N+1 and keeps the single-id method beside it. No consumer lists anything yet, so the batch form
  waits for the first one that does — most likely a submission history screen in stage 6 — rather
  than being guessed at here.
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
- **Ownership is a rule for reads of a user's rows, and the rest of
  [#35](https://github.com/shoraLBRT/ritocode/issues/35) is not started.** The three reads find a
  workspace through `OwnedWorkspaces.FindOwnedAsync` and a save through `FindOwnedForUpdateAsync`, both
  with the owner inside the query, and `OwnershipRuleTests` fails on any other way into the
  Workspaces or Submissions sets. What the issue still holds: the **submission rate limit**, which is
  its own box in stage 4, and the **input hardening** — a request body cap, security headers, a CORS
  policy for anything but development — which is after the slice. Two edges the rule does not see, on
  purpose: the non-generic `DbContext.Find(Type, …)` and SQL passed as a string to `ExecuteSql`;
  neither is a way anyone reads a row by accident. And it guards **modules**, not endpoints: a
  Submissions endpoint that asked Workspaces for another user's workspace through a contract would
  pass it, which is why a contract answering a workspace would have to take the owner too.
- **The object storage client puts, gets and copies, and does nothing else.**
  [#5](https://github.com/shoraLBRT/ritocode/issues/5) stays open for the operations left out, each
  because its first real caller decides its shape:
  **deletion and prefix listing** go together — deleting a prefix reference is a list-then-delete —
  and belong to retention, deferred with
  [#43](https://github.com/shoraLBRT/ritocode/issues/43);
  **server-side copy** arrived with its caller,
  [#14](https://github.com/shoraLBRT/ritocode/issues/14), which freezes a workspace tree at submit —
  so the issue is now open for deletion and listing alone. All three operations have real callers:
  ingest writes problem bundles, opening a workspace reads one back and writes the first snapshot,
  and submitting copies a snapshot into `evaluation-artifacts`.
- **Object storage has no readiness check.** `AddObjectStorage` registers a client that contacts
  nothing at startup, so `/health/ready` still reports one check per module schema and no more.
  Adding a storage check would make `dotnet test` and a bare `dotnet run` require MinIO — the
  property [#37](https://github.com/shoraLBRT/ritocode/issues/37) spent a session buying back — so
  it waits for the first endpoint that cannot serve a request without an object.
- **Cross-module references carry no foreign key**, by design — see
  [ADR 0004](adr/0004-persistence-and-migrations.md). Whichever module creates such a row is
  responsible for validating the reference first, and the two Workspaces references are now
  validated on create through the contracts. The other three in [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) —
  `linked_accounts.user_id`, and `submissions.workspace_id` and `.user_id` — get theirs when their
  writers arrive; `submissions.user_id` can reuse `IUserLookup` only if it asks the identical question.

---

## Next up

The slice plan is the ordered list now: **[`docs/SLICE_PLAN.md`](SLICE_PLAN.md)**. Take the first
unticked box. The stages there are ordered so that each depends only on stages above it.

**Stages 1, 2 and 3 are complete**: the identity seam, the cross-module contracts, opening a
workspace, reading its files, saving them within the version's limits, and the ownership rule. What
those left in place for everything after them: every endpoint takes its user from `ICurrentUser`, one
that says nothing about authorisation is protected rather than open, a module that needs another
module's facts asks through a contract in `Shared/Contracts`, a workspace exists as a row and a
snapshot, every path that reaches a workspace passes one rule, every save of a workspace holds its
row's lock, and a user's rows in Workspaces or Submissions are reached only where the owner is in the
query — `OwnershipRuleTests` fails otherwise. **Stage 4 is three boxes of five in**: a workspace can
be submitted, which freezes its tree and queues an attempt the caller can read back and list; the
queue can be claimed without handing an attempt out twice, with a result recorded only on the claim
that still holds it; and a validator has an interface, a result schema and a registry to be found in.
**The next box is:**

1. **[#17](https://github.com/shoraLBRT/ritocode/issues/17) (partial) — orchestrator.** Sequential
   validator execution and status transitions; no retries, priorities, cancellation or parallelism.
   Shaped by [ADR 0009](adr/0009-evaluation-is-a-command-submissions-issues.md): `ISubmissionEvaluator`
   lands here with its implementation in Evaluations, and so does the hosted loop in Submissions that
   claims, evaluates and records. **It has an ordering problem to settle before it starts, and it is
   likely the maintainer's.** The plan puts #17 in stage 4, but everything an evaluation needs to
   *run* — the sandbox runner (#21), the runner image (#22) and the compile and test validators (#19) —
   is stage 5. So the orchestrator can be written and tested against `ISandboxRunner` and a test plugin,
   but in the host there is nothing to run a step with. A loop enabled in that state claims attempts
   and must fail every one of them, and a loop left disabled is a switch — the thing the ownership entry
   below warns gets flipped. The candidates are: ship the orchestrator in stage 4 with no loop, and move
   the loop into stage 5 with the runner; move #17 after #21 in the plan; or ship the loop and fail
   attempts honestly as `Failed` with a `notCompleted` report until stage 5. The first changes what
   ADR 0009's consequences say lands with #17; the second reorders the slice; the third puts attempts
   that could never have passed in front of a tester. Whichever it is, the rule that holds is ADR 0005's
   first forbidden row: no stand-in that grades.

The ADRs written so far are off this list and their obligations are in
[Open questions](#open-questions) instead. The newest,
[ADR 0009](adr/0009-evaluation-is-a-command-submissions-issues.md), places the worker in Submissions
and makes evaluation a command Evaluations answers; #15 and #17 are built to it. Briefly: submission reports gain somewhere to carry a
timeout or a resource exhaustion, and #22 gains a runner registry. ADR 0007's obligation — two lookup
interfaces and the assertions that keep them honest — is now met.

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
  the token decision — `/me` does not: it reads `ICurrentUser` and returns a user row. One
  correction to what this entry used to say: it does **not** simply reuse `IUserLookup`. That
  contract answers "does this user exist" with an id and a username; `/me` wants an email and more,
  and ADR 0007 §1 gives a different question its own interface rather than a wider one. `/me` is
  also inside the Users module's own boundary if Users serves it, in which case it needs no contract
  at all. It stayed out because the plan said so and nothing in the slice
  needs it, not because it is hard. Its absence no longer leaves the fallback policy exercised only
  by a probe: the workspace endpoints of [#10](https://github.com/shoraLBRT/ritocode/issues/10) are
  protected product endpoints, and their refusal of an anonymous caller is tested.
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
- **Where a submission's evaluated tree is recorded.** *Created by
  [STORAGE_LAYOUT.md](STORAGE_LAYOUT.md); settled by
  [#14](https://github.com/shoraLBRT/ritocode/issues/14).* In `submissions.input_reference`. An
  evaluation reads a frozen copy at `evaluation-artifacts/submissions/{id}/input/tree.tar.gz`, never
  the live workspace key, which every save overwrites. #14 copies it server-side at submit, before the
  row commits, and stores the reference — required and with **no default**, deliberately unlike what
  `migrations add` generated, because an empty string is not a reference and a default would let an
  insert that forgot the tree succeed and fail later in a worker. The table was empty when the column
  arrived, so no row needed one. Two consequences for #15 and #17: **the orchestrator reads the
  input from the row**, never from `StorageKeys.SubmissionInputTree`, which is now for the write only;
  and **the copy is not under the workspace's row lock**, which is correct — a put is atomic, so the
  copy sees the tree before a concurrent save or after it — and would stop being correct the day a
  snapshot is written in more than one object.
- **Submissions asks Workspaces for a workspace with its owner.** *Settled by
  [#14](https://github.com/shoraLBRT/ritocode/issues/14).* `IOwnedWorkspaceLookup.FindAsync(userId,
  workspaceId)` rather than a lookup by id whose summary carries a `UserId` for the consumer to compare.
  ADR 0007 would allow either — whose workspace it is, is a fact — but the second moves the ownership
  check out of the module that owns the row and into every consumer that remembers it, which is the
  habit [#35](https://github.com/shoraLBRT/ritocode/issues/35)'s rule replaced, and which that rule
  cannot see across a contract. The consumer still chooses its own code: Submissions answers
  `workspace_not_found`, the same string Workspaces uses, so a client branches on one meaning. A
  worker that needs a workspace **without** a user — none does yet, since the frozen tree is all an
  evaluation reads — would get its own interface rather than an optional owner on this one.
- **Where a submission report carries a timeout or a resource exhaustion.** *Created by
  [ADR 0006](adr/0006-sandbox-execution-model.md) §5; settled by
  [ADR 0009](adr/0009-evaluation-is-a-command-submissions-issues.md) §4, and built by
  [#17](https://github.com/shoraLBRT/ritocode/issues/17).* In the report, per validator. A run that
  ended `TimedOut`, `ResourceExhausted` or `Crashed` makes the submission `Failed` — ADR 0006 §5 already
  said so — and the report still carries each validator's runner outcome, so the distinction reaches
  the person reading it rather than being flattened into the status. A pipeline that ran to the end is
  `Completed` with its score, passing or not. `Submission.Fail(at)` therefore stays reason-less on
  purpose. The JSON shape of `validator_results` is [#18](https://github.com/shoraLBRT/ritocode/issues/18)'s. The runner distinguishes `Completed`,
  `TimedOut`, `ResourceExhausted` and `Crashed`, and is explicitly allowed not to know which of the
  last two applies — `OOMKilled` is a reliable positive and an unreliable negative, since a managed
  `OutOfMemoryException` aborts at 134 before the kernel is involved. If the schema above the runner
  has nowhere to put that distinction, the honesty is discarded on the way up and a person is told
  their tests failed when the container was killed.
- **How the reference form is enforced at the database.** *Settled by
  [#9](https://github.com/shoraLBRT/ritocode/issues/9), which wrote the first reference; adopted
  for two columns of three.* An EF value converter, `StorageReferenceConverter` in
  `Ritocode.Shared.Persistence`. The property is typed as `StorageReference`, so no code path can
  put an arbitrary string in the column, and a value this build cannot resolve throws where it is
  read rather than reaching a caller that assumed it parsed. A check constraint on the role prefix
  was the alternative and buys little the converter does not: the writes it would catch are the
  ones the converter makes unexpressible. **`workspaces.snapshot_reference` converted with its first
  writer**, [#10](https://github.com/shoraLBRT/ritocode/issues/10) — earlier than the #12 this entry
  used to name, because #10 turned out to write it first — with no migration and no drift, since the
  store type and width are unchanged. **`submission_reports.logs_reference` is still `string`** and
  converts with its first writer, [#23](https://github.com/shoraLBRT/ritocode/issues/23), while its
  table is empty and the change is two lines.
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
  [ADR 0007](adr/0007-cross-module-contract-form.md); the first two contracts exist, and
  [#10](https://github.com/shoraLBRT/ritocode/issues/10) is their first caller.*
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
  to move back into `dotnet test`. **Two things building the first contracts settled that the ADR
  left open.** A summary record may carry more than the ADR's illustrative fields when the consumer
  needs them — `ProblemVersionSummary.SnapshotReference` exists because a consumer outside Problems
  could otherwise only reach the bundle by rebuilding its key, which
  [STORAGE_LAYOUT.md](STORAGE_LAYOUT.md) forbids; the discipline that stops this becoming a shared
  entity is that each added field names the consumer need it serves. And the contract's owner is
  enforced, not just its existence: `CrossModuleContractTests` requires `Contracts.Users.*` to be
  implemented in `Ritocode.Modules.Users`, so a contract declared directly under
  `Ritocode.Shared.Contracts` with no owner segment fails. Implementations are `internal` and
  registered by implementation type, never by factory — a factory hides the type the test reads.
- **Where Workspaces learns what a version allows.** *Created by
  [#10](https://github.com/shoraLBRT/ritocode/issues/10); settled by
  [#12](https://github.com/shoraLBRT/ritocode/issues/12).* The manifest's workspace section is a
  format that belongs to Problems and that Workspaces may not parse. #10 carried `workspace_root`
  across as a column; #12 carries the rest the same way. Ingest stores `editable_files text[]` — the
  loader's **resolved** list, not the globs, so glob matching stays in Problems and a consumer
  compares paths — and `max_files`, `max_file_bytes` and `max_total_bytes`, under a check
  constraint restating the format's rule. A **third contract**, `IWorkspaceAllowanceLookup`, hands
  them to Workspaces, rather than two more fields on `ProblemVersionSummary`: ADR 0007 §1 gives a
  different subset its own interface, and opening needs the bundle and publication where saving
  needs neither. It is named for an allowance rather than a policy because §2 says contracts answer
  facts — the lists and numbers are facts, and `403`, `409` and `400` are Workspaces' answers to them.
  Columns rather than one `workspace_config jsonb`, because each is read whole by one consumer and a
  constraint can check them. `readonly` is not stored: by the format's totality rule a starter file
  that is not editable is read-only, and the orchestrator in
  [#17](https://github.com/shoraLBRT/ritocode/issues/17) restores read-only files from the bundle,
  which holds them. **One consequence to carry**: the migration gives every existing version **no**
  editable files — a version that never said what may change refuses every save rather than
  guessing — so a development database seeded before #12 shows every file read-only, and the seeder
  skips a slug that already has a version. That is the republishing question below arriving for
  real; clearing the problem rows, or `docker compose down -v`, and restarting is the way out today.
- **A save replaces a file and never creates one.** *Created by
  [#12](https://github.com/shoraLBRT/ritocode/issues/12); revisit when a problem needs it.* Because
  `editable_files` is resolved, a path the starter tree did not hold was never matched, and nothing
  Workspaces can reach knows whether `src/Extracted.cs` would have been. That rules out the most
  natural refactoring move — extracting a type into its own file — and deleting and renaming with it.
  Two ways to lift it, both a change behind the contract rather than to the endpoints: store the
  globs beside the resolved list and let Problems answer whether a path matches — a question ADR 0007
  §2 allows, as long as it reports the match and not the permission — or move a glob matcher into
  `Shared`, which moves format knowledge out of Problems, the thing resolving was chosen to avoid.
  Creating a file is also the first time `max_files` can be exceeded by a save, since a replace
  cannot change the count, and it needs its own answer to a revision for a file that does not exist
  yet. Every slice problem is solvable inside its existing files, so this waits for content that is
  not.
- **Revision protection is a hash of the file, sent in the body.** *Settled by
  [#12](https://github.com/shoraLBRT/ritocode/issues/12).* A read reports `revision`, the lower-case
  hex SHA-256 of the file's bytes; a save must send it as `baseRevision`, and a mismatch answers
  `412 workspace_file_changed` — the first producer of `ErrorType.PreconditionFailed`. A content hash
  rather than a counter because it needs no column, it is per file — saving one file never makes an
  editor's copy of another stale — and it cannot disagree with the tree: if a save's put lands and its
  commit fails, the next read still reports the stored content's revision. In the body rather than
  as `If-Match` and `ETag`, because a file is addressed by a query value and read as JSON, and a
  header pair would be the one part of this contract outside the JSON the frontend client models;
  ADR 0003 names neither, and a second endpoint needing a precondition is the moment the choice
  belongs there. **Required**, not optional with "last write wins" as the default: a client that
  omits it is exactly the client that would overwrite a change nobody saw. Saving exactly the stored
  bytes writes nothing and does not move `updated_at`.
- **A save holds a row lock across two object store calls.** *Created by
  [#12](https://github.com/shoraLBRT/ritocode/issues/12); revisit with
  [#13](https://github.com/shoraLBRT/ritocode/issues/13), and when saves get large or frequent.* The
  revision alone cannot stop two saves of **different** files from losing one: both read the same
  tree, each puts back the whole tree with only its own change, and the later put wins.
  `OwnedWorkspaces.FindOwnedForUpdateAsync` takes `SELECT … FOR UPDATE` on the workspace row inside
  a transaction, and the download, the rewrite, the put and the `updated_at` commit all happen under
  it; `ConcurrentWritesToDifferentFiles_BothSurvive` is the test that fails without it. Three
  consequences. **A database connection is held for a download and an upload** — milliseconds for
  the slice's trees, and not for the 100 MiB the limits permit. **Every other writer of the snapshot
  has to take the same lock**, and reset in #13 is the next one; a server-side copy that only reads
  the object, as the submission freeze in [#14](https://github.com/shoraLBRT/ritocode/issues/14)
  will, does not need it, because a put is atomic. And **the whole save is the unit the EF execution
  strategy retries**: `Database:MaxRetryCount` defaults to 3 outside tests, and a retrying strategy
  refuses a user-initiated transaction that is not wrapped in one. A retry after a put that landed
  and a commit that failed answers `412` for a save that is in fact stored — rare, and seen by a
  person as a reload, not as a lost change.
- **What a validator reports, and what it does not.** *Settled by
  [#18](https://github.com/shoraLBRT/ritocode/issues/18).* A validator reports what happened in its own
  step — `passed`, `failed`, `notCompleted` or `skipped`, the runner's outcome, a one-line summary and
  its named checks — and never what that is worth. Weights, and whether a failed required step stops the
  pipeline, belong to the orchestrator and [#20](https://github.com/shoraLBRT/ritocode/issues/20), so two
  plugins cannot score one outcome two ways. **One product question is left open on purpose, for #20**:
  whether a test validator earns its weight all or nothing, or in proportion to the tests that passed.
  The per-test checks are in the result so either rule can be written without changing a plugin, and
  the rule is part of what a person is shown, so it should be decided with the verdict screen in mind
  rather than inside a parser. The schema's JSON is canonical and holds nothing that varies between two
  runs — no duration, no timestamp, no raw output — which is what lets
  [#38](https://github.com/shoraLBRT/ritocode/issues/38) compare two evaluations byte for byte. A
  digest of it, if one is ever wanted, is computed from `ValidatorResults.ToJson` before the write,
  never from the `jsonb` column, for the reason the `validator_config` entry below gives.
- **How ownership is enforced.** *Settled by
  [#35](https://github.com/shoraLBRT/ritocode/issues/35).* By an architecture test over compiled IL,
  not by an EF global query filter. `OwnershipRuleTests` guards every entity `WorkspacesDbContext` and
  `SubmissionsDbContext` map — read from the model EF builds, so an entity with no set property is
  guarded too — and fails on any member that reaches one outside an allowance carrying its reason. A
  query filter keyed on `ICurrentUser` would be automatic, and would put the current user inside a
  `DbContext`: the queue worker of [#15](https://github.com/shoraLBRT/ritocode/issues/15) serves no user,
  and every test that writes another user's row directly would need `IgnoreQueryFilters`, so the
  switch-off would exist in exactly the places a mistake is most expensive. The test says what it
  checks, and an allowance is a line a reviewer reads. Three things to carry. **A new owned lookup
  lives beside its module's `Owned*` class**, which is allowed whole; anything else gets its own
  allowance naming the method — `EveryAllowance_StillReachesAUsersRows` fails on one left behind by a
  rename. **The reader has to keep seeing**: `TheReader_SeesEveryShapeOfReachingAUsersRows` runs it
  over `UnguardedReads`, six never-called methods covering a set property, `Set<T>`, `Find<T>`, an async
  state machine, a lambda closure and `SqlQuery<T>`, so a compiler change that moves bodies somewhere
  new fails there rather than silently passing the module code. And **it guards modules, not
  endpoints**: nothing stops a contract from answering another user's row to a caller in another
  module — a contract that answers a workspace has to take the owner, which is ADR 0007's facts rule
  applied to ownership.
- **How a workspace file is addressed.** *Settled by
  [#11](https://github.com/shoraLBRT/ritocode/issues/11); #12 inherits it.* As a query value —
  `GET /api/v1/workspaces/{id}/files/content?path=src/App.cs` — and not as the rest of the URL path.
  Kestrel decodes a request path and removes its `.` and `..` segments **before routing**, and
  `HttpClient` normalises them before sending, so `files/../problem.yaml` never reaches a handler as
  written: it becomes some other route. That is safe, and it means the refusal ADR 0005 demands can
  be trusted but never observed — no test can send the path to the rule. A query value arrives
  verbatim, is refused by name as `errors.path`, and needs no per-segment escaping in a client. The
  cost is a URL that reads less like a file system; `/files` for the tree and `/files/content` for a
  file keeps one shape per route rather than letting `?path=` switch `/files` between two bodies.
- **Workspace files are text, and nothing guarantees it.** *Created by
  [#11](https://github.com/shoraLBRT/ritocode/issues/11); worth settling with
  [#36](https://github.com/shoraLBRT/ritocode/issues/36) or the next package authored.* A file is
  served as a JSON string, so its bytes must be UTF-8, and one that is not answers
  `409 workspace_file_not_text` rather than being decoded leniently into something that saves back
  as different bytes. Every committed starter file is ASCII today. But
  [PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md) requires UTF-8 of the manifest and the
  description and says nothing about the workspace tree, so a package with a binary or Latin-1 file
  loads, ingests and publishes, and then shows its user a file they cannot open. The failure belongs
  at authoring: a validation rule "every file under the workspace root is UTF-8" in the spec and the
  loader, which is Problems' change, not Workspaces'. The alternative — serving bytes and letting the
  editor cope — gives up the JSON payload rule of ADR 0003 for files no problem in the slice has.
- **Every file read downloads the whole snapshot.** *Created by
  [#11](https://github.com/shoraLBRT/ritocode/issues/11); revisit when a tree gets large or reads
  get frequent.* Listing the tree and reading one file each fetch the gzipped tar and scan it, and a
  save fetches it, rewrites it and puts it back, under the row lock above. For the slice's packages
  that is a few kilobytes and cheaper than any cache would be to keep correct now that saves replace
  it. The package limits, though, allow `max_total_bytes` up to 100 MiB, and an editor
  opening ten files would fetch that ten times. The two options worth weighing when it matters: a
  per-workspace cache keyed by the snapshot's version, invalidated by the save that replaces it; or
  a manifest object beside the tar holding paths and sizes, which makes listing cheap and leaves
  reads as they are. Neither changes the endpoints.
- **The file tree is one object, not a `Page<T>`.** *Settled by
  [#11](https://github.com/shoraLBRT/ritocode/issues/11).* ADR 0003 says a collection endpoint answers
  in the page envelope and never as a bare array. The tree is neither: it is `{ "files": [...] }`,
  whole. An editor cannot use half a file list, a page boundary in a tree has no meaning to a person,
  and a package's limits bound the tree at 2000 entries. The reading of the ADR that makes this
  consistent is that the tree is one resource with a list inside it, not a collection of resources.
  If a second endpoint needs the same reading, it belongs in ADR 0003 alongside the enum and ordering
  notes below.
- **One workspace per user per version, and nothing enforces it.** *Created by
  [#10](https://github.com/shoraLBRT/ritocode/issues/10); revisit with
  [#13](https://github.com/shoraLBRT/ritocode/issues/13).* Opening a version the caller already has
  a workspace on returns that workspace — 200, not 201 — because the draft a person left is what
  "open" has to find, and there is no endpoint yet that lists workspaces to find it any other way.
  The index on `(user_id, problem_version_id)` is not unique, so two concurrent first opens can both
  create a row and a snapshot; a later open returns the most recently written, and the other is an
  orphan with an object behind it. That costs storage rather than work, and nothing in the slice
  races itself. A unique index would make it a rule, and is a migration plus a decision about reset:
  whether #13 replaces a tree in place — which a unique index suits — or starts a fresh workspace,
  which it forbids. Worth deciding there, against the real case.
- **A timestamp returned from memory has to match the one read back.** *Found by
  [#10](https://github.com/shoraLBRT/ritocode/issues/10), settled.* .NET keeps 100-nanosecond ticks
  and `timestamptz` keeps microseconds, so a create response built from the new entity reported a
  `createdAt` that no later read of the same workspace would ever return — caught by the test that
  follows the `Location`, where two values that print identically compared unequal.
  `Workspace.Create` now truncates to the microsecond. `ProblemVersion.Create` does not, and does not
  need to yet, because nothing answers with a version it just built; the next endpoint that answers a
  create with the entity in hand needs the same truncation, or the same test.
- **How does one module *change* another's state?** *Answered for evaluation by
  [ADR 0009](adr/0009-evaluation-is-a-command-submissions-issues.md); open for everything else, and
  outside the slice.* Evaluation does not change another module's state at all: Submissions issues a
  command whose callee writes only its own artifacts and returns the outcome, and Submissions records
  it. ADR 0009 admits that category — a *command contract* — under three conditions, and any need that
  fails one of them is still without a mechanism. ADR 0007 is otherwise read-only by decision. The first real case is
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

Every endpoint above says `AllowAnonymous()`, which is load-bearing: the host protects anything that
does not. The workspace endpoints say nothing, and in Production — development identity off — they
refuse:

| Request | Expected |
| --- | --- |
| `POST /api/v1/workspaces` with `{"problemVersionId":"<any id>"}` | `401`, `application/problem+json`, `code: "unauthenticated"` |
| `PUT /api/v1/workspaces/<any id>/files/content?path=src/App.cs` with a well-formed body | `401`, `code: "unauthenticated"` — the policy, not a validation error that happened to run first |

The seam itself is observed in the log and in the database:

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
| `POST /api/v1/workspaces` with `{"problemVersionId":"<that id>"}` | `201`, a `Location` ending `/api/v1/workspaces/{id}`, and `id`, `problemVersionId`, `createdAt`, `updatedAt` |
| the same `POST` again | `200`, the same body — one workspace per user per version |
| `GET` the `Location` | `200`, the same body |
| `GET /api/v1/workspaces/not-a-workspace` | `404`, `code: "workspace_not_found"` |
| `POST /api/v1/workspaces` with `{}` | `400`, `code: "validation_failed"`, `errors.problemVersionId` present |
| `GET /api/v1/workspaces/{id}/files` on that workspace | `200`, `files` holding four entries in this order — `Billing.csproj`, `README.md`, `src/InvoiceSplitter.cs`, `tests/InvoiceSplitterTests.cs` — each with a `sizeBytes` and `editable`, `true` on `src/InvoiceSplitter.cs` only, and no `problem.yaml` |
| `GET /api/v1/workspaces/{id}/files/content?path=src/InvoiceSplitter.cs` | `200`, `path`, `sizeBytes`, the file's text in `content`, and a 64-character hex `revision` |
| the same with `?path=problem.yaml` | `404`, `code: "workspace_file_not_found"` — the manifest is in the bundle, not the workspace |
| the same with `?path=../problem.yaml` | `400`, `code: "validation_failed"`, `errors.path` present |
| `PUT` the same `?path=src/InvoiceSplitter.cs` with `{"content":"// edited\n","baseRevision":"<that revision>"}` | `200`, `path`, `sizeBytes: 10` and a new `revision`; the `GET` above now answers `// edited`, and `GET` on the workspace shows `updatedAt` later than `createdAt` |
| the same `PUT` again, with the same `baseRevision` | `412`, `code: "workspace_file_changed"` |
| `PUT` to `?path=tests/InvoiceSplitterTests.cs` with that file's `revision` | `403`, `code: "workspace_file_read_only"` |
| `PUT` to `?path=../problem.yaml` with any well-formed body | `400`, `code: "validation_failed"`, `errors.path` present |
| `PUT` with `{}` | `400`, `code: "validation_failed"`, `errors.content` and `errors.baseRevision` present |
| `POST /api/v1/submissions` with `{"workspaceId":"<that workspace>"}` | `201`, a `Location` ending `/api/v1/submissions/{id}`, and `id`, `workspaceId`, `status: "queued"`, `score: null`, `createdAt`, `completedAt: null` |
| a save to the workspace, then the same `POST` again | `201` with a different `id` — every submit is a new attempt, and each freezes the tree as it was |
| `GET` the `Location` | `200`, the same body |
| `GET /api/v1/submissions?workspaceId=<that workspace>` | `200`, the page envelope, `totalItems: 2`, the second attempt first |
| `GET /api/v1/submissions?workspaceId=not-a-workspace` | `200`, an empty page — an id that names nothing filters to nothing |
| `GET /api/v1/submissions?pageSize=1000` | `400`, `code: "validation_failed"`, `errors.pageSize` present |
| `GET /api/v1/submissions/not-a-submission` | `404`, `code: "submission_not_found"` |
| `POST /api/v1/submissions` with an id no workspace has | `404`, `code: "workspace_not_found"` |
| `POST /api/v1/submissions` with `{}` | `400`, `code: "validation_failed"`, `errors.workspaceId` present |
| `select status, input_reference from submissions.submissions` | every row `Queued` — nothing drains the queue before #15 — each with its own `evaluation-artifacts/submissions/{id}/input/tree.tar.gz` |

**If every file answers `editable: false`, the database predates the workspace allowance.** The
migration gives an existing version no editable files, and the seeder will not publish a slug again,
so clear the problem rows — or `docker compose down -v` — and restart; see
[Open questions](#open-questions).

Opening a workspace needs the MinIO from `dev-up` for the same reason seeding does: it reads the
version's bundle and writes `workspace-snapshots/workspaces/{id}/tree.tar.gz`. A second `POST` on the
same database answers `200` for as long as the database lives, so the `201` is seen once per version
per database.

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

Current baseline: **564 backend tests, all passing** — 129 shared, 120 problems, 97 API,
112 workspaces, 52 submissions, 41 evaluations, 13 architecture — and **68 frontend tests**, run
separately by `npm test`. A session that leaves either number lower than it found it has broken
something.

The new evaluations assembly arrived at 41 and the API assembly rose from 96 to 97 with the validator
plugin interface of [#18](https://github.com/shoraLBRT/ritocode/issues/18). The evaluations assembly
needs no Docker and references no test support at all: the interface is exercised through
`ExitCodeValidator`, a plugin that exists only in the tests, and the result schema is asserted as
literal JSON, because that string is what [#38](https://github.com/shoraLBRT/ritocode/issues/38) will
compare between two evaluations. The API assembly's one is `ValidatorRegistryCompositionTests`, which
asserts the host registers no plugin yet — the assertion #19 changes on purpose.

The submissions assembly rose from 32 to 52 and the API assembly from 94 to 96 with the queue of
[#15](https://github.com/shoraLBRT/ritocode/issues/15). `SubmissionDispatcherTests` runs against a real
PostgreSQL because both of the things worth testing there are properties of the database rather than
of the code: twelve concurrent claimers over six attempts must hand each out exactly once, and a row
another transaction holds locked must be passed over rather than waited on. The schema tests now cover
the claim-time constraint as well, each case breaking exactly one rule so the constraint named is the
cause. The API assembly's two are `SubmissionDispatcherCompositionTests`, which resolve the dispatcher
from the real host — nothing in the host calls it before #17, so a registration that cannot be
constructed would otherwise first fail inside the worker loop.

The new submissions assembly arrived at 32, the API assembly rose from 80 to 94, the shared assembly
from 125 to 129, the workspaces assembly from 110 to 112 and the frontend from 63 to 68 with
[#14](https://github.com/shoraLBRT/ritocode/issues/14). Its domain tests start no container;
`SubmissionSchemaTests` writes around the entity with raw SQL on purpose, because the constraint exists
for the writer the entity cannot stop; and `SubmissionLifecycleTests` saves over a snapshot after
submitting and asserts the frozen copy did not move, against a real MinIO. The shared assembly's four
new tests are the copy, including that a copy is not a pointer. The architecture count did not move,
and did not need to: `OwnershipRuleTests` picked up the two new allowances, and its stale-allowance
test proves both are used.

The architecture assembly rose from 9 to 13 with the ownership rule of
[#35](https://github.com/shoraLBRT/ritocode/issues/35), and still needs no Docker: it builds the two
contexts' models with no connection and reads IL. The count matters less than the shape. One test is
the rule, one guards it against being vacuous, one against a stale allowance, and one proves the reader
against `UnguardedReads` — without that last one, a reader that stopped seeing anything would pass the
rule exactly as clean code does. The rule was also run once against a real violation added to the
Workspaces module, where it failed naming the method and the member, before that file was removed.

The workspaces assembly rose from 71 to 110, the API assembly from 65 to 80, the problems assembly
from 113 to 120 and the frontend from 61 to 63 with [#12](https://github.com/shoraLBRT/ritocode/issues/12)
and [#36](https://github.com/shoraLBRT/ritocode/issues/36). `WorkspaceFileWriteTests` saves against a
real PostgreSQL and MinIO, because the two things most worth testing there are not fakeable: the row
lock, which `ConcurrentWritesToDifferentFiles_BothSurvive` would fail without, and a snapshot that is
really rewritten. `FileRevisionTests` and the new `SnapshotArchiveTests` start no container. The API
assembly's save tests take which files are editable and how large one may be from the committed
`split-the-invoice` manifest through real ingest, so a limit written in `problem.yaml` is the limit
refused over HTTP.

The workspaces assembly rose from 28 to 71, the API assembly from 50 to 65 and the frontend from 55
to 61 with [#11](https://github.com/shoraLBRT/ritocode/issues/11). Most of the workspaces rise is
theories: `WorkspacePathTests` and `SnapshotArchiveTests` start no container, and
`WorkspaceFilesTests` writes each workspace's row and snapshot directly, so a snapshot can hold bytes
no starter tree would — a byte-order mark, a PNG. The API assembly's file tests compare what a client
reads against the committed package on disk, and the refusal of a path such as `../problem.yaml` is
asserted over HTTP, which is only possible because the path is a query value.

The API assembly rose from 39 to 50 and the new workspaces assembly arrived at 28 with
[#10](https://github.com/shoraLBRT/ritocode/issues/10). The API assembly now starts MinIO as well as
PostgreSQL, but only for `WorkspaceApi`, which ingests a committed package for real — the content
tree is copied into its output for that. `WorkspaceApi` publishes a fresh version per test rather
than one per class: every request there is the same identity, and a version it already opened
answers 200 instead of 201.

The architecture assembly rose from 5 to 9, the problems assembly from 109 to 113 and the API
assembly from 36 to 39 with the cross-module contracts. The architecture tests compose the host's
real service collection to check registrations and never build a provider, so they still need no
Docker; resolving a contract for real — lifetimes, dependencies, the query — is the API assembly's
`ContractResolutionTests`, which is where a registration that compiles and cannot be constructed
shows up.

The shared assembly rose from 110 to 125 and the API assembly from 25 to 36 with the identity seam
of [#6](https://github.com/shoraLBRT/ritocode/issues/6). The API assembly now boots **two** hosts: the
usual one, and `AnonymousTestApi` with the development identity switched off. That second host is
where everything about the authorisation policy is actually proved — with an identity enabled every
request passes, and a lost `AllowAnonymous` looks exactly like a correct host.

The problems assembly rose from 91 to 109 with the content of
[#42](https://github.com/shoraLBRT/ritocode/issues/42): `CatalogPackageTests` checks each committed
package as it ships, and most of its cases are theories over the content directory, so a fourth
catalog problem adds tests without anyone writing one.

Four of the five test assemblies now need a Docker daemon: the shared assembly starts MinIO, and
the API, Problems and Workspaces assemblies start both — each writes rows and objects for real. Only
`Ritocode.Architecture.Tests` runs without one, and inside the others the format, starter tree and
domain tests start no container.
