# Slice Plan — Phase 1, stage one

The current milestone: one complete user journey, end to end, at full architectural quality.
Browse the catalog → open a workspace on a problem version → edit files → submit → evaluate inside
a real sandbox → read the verdict and the per-validator report.

Decided in [ADR 0005](adr/0005-vertical-slice-before-breadth.md), which also carries the list of
reductions that are allowed and the list that are forbidden. **Read that ADR before ticking
anything here.** This file tracks progress; it holds no decisions.

- **Last updated:** 2026-09-05
- **Progress:** 5 / 37
- **Estimate:** 30–34 sessions, six to seven weeks at five sessions a week
- **Then:** [stage two](#after-the-slice) — the rest of Phase 1

---

## How to use this file

- Tick a box only when the work is merged and everything under
  [Verification](PROJECT_STATE.md#verification) passes. Not when the PR opens.
- **A ticked box is not a closed issue.** Items marked `(partial)` enter an issue on purpose and
  leave the rest. Comment on the issue with what landed and what was left, and leave it open —
  `AGENTS.md` is explicit about this.
- Update **Progress** above in the same commit that ticks a box.
- If an item turns out to be wrong, change it here and say why in the PR. The plan is allowed to
  move; the ADR's forbidden list is not.

| Stage | Sessions | Done |
| --- | --- | --- |
| [1 — Foundation](#stage-1--foundation) | 5 | 5 / 5 |
| [2 — Content and catalog](#stage-2--content-and-catalog) | 5 | 0 / 6 |
| [3 — Identity and workspace](#stage-3--identity-and-workspace) | 6 | 0 / 7 |
| [4 — Submission and queue](#stage-4--submission-and-queue) | 5 | 0 / 5 |
| [5 — Execution](#stage-5--execution) | 7 | 0 / 8 |
| [6 — Product face](#stage-6--product-face) | 6 | 0 / 6 |

---

## Stage 1 — Foundation

Everything here gets more expensive the longer it waits. Nothing downstream is safe to start until
the two ADRs are written.

- [x] **[#37](https://github.com/shoraLBRT/ritocode/issues/37) (partial) — Integration test harness.**
  Testcontainers, one database per test class, each copied from a template migrated once by the
  same runner the deploy step uses. The API tests moved onto it and the baseline rose from 54 to
  56. Marked partial because the issue also asks for integration tests over the auth, problems,
  workspace and submission flows — none of those endpoints exist yet, so those tests land with the
  features that make them possible, in stages 2 to 4. The harness itself is complete.
- [x] **[#8](https://github.com/shoraLBRT/ritocode/issues/8) — Problem package manifest.**
  `problem.yaml` schema, the shape of `validator_config`, allowed paths, hints, constraints — all
  in [PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md), implemented by
  `Ritocode.Modules.Problems.Packaging` and enforced by 65 tests. The reference package in
  `content/problems/example-order-total` is loaded from the committed tree, so the format cannot
  drift from its example. `validator_config` is a canonical JSON projection of the pipeline: the
  same manifest serialises to the same bytes whatever order it was written in.
- [x] **Spike — sandbox execution.** Written up in
  [`spikes/sandbox-execution/`](../spikes/sandbox-execution/README.md), reproducible with the
  script beside it. The reference package's two real validators run under the whole flag set from
  ADR 0005 and separate the passing fixture from the failing one, at ~500 ms of container overhead
  and ~8.5 s per submission. Four things the ADR has to answer came out of it: `--network none`
  makes the offline package cache part of the runner image contract; `docker run` has no timeout
  and its exit code cannot say why a container died, so a managed out-of-memory looks like an
  ordinary crash; determinism holds for the normalised name-to-outcome projection and never for the
  raw artifact, which decides how [#20](https://github.com/shoraLBRT/ritocode/issues/20) scores and
  what [#38](https://github.com/shoraLBRT/ritocode/issues/38) may assert; and the submitted tree
  outranks anything the runner sets through configuration, so guarantees have to live in the flags.
  Closes no issue, as planned.
- [x] **[ADR 0006](adr/0006-sandbox-execution-model.md) — sandbox execution model.** Written from
  the spike, and it answers the four things the spike refused to settle. Containment lives in the
  container flags and nowhere else. The runner appends precedence-winning arguments to the
  manifest's command, and those arguments belong to the **image**, not the runner — so
  `ISandboxRunner` stays language-agnostic and a second language adds a registry row. One container
  per validator over a read-only workspace mount and a shared writable output mount. The runner
  reports `Completed` / `TimedOut` / `ResourceExhausted` / `Crashed` and is allowed not to know why
  a container died — `OOMKilled` is a reliable positive and an unreliable negative. Determinism is
  a property of the normalised projection, and the resource limits are part of that contract rather
  than an operational knob. The production host — warm pool, dedicated VM, Docker-in-Docker —
  stays deferred, unchanged from ADR 0005. Closes no issue, as planned.
- [x] **[ADR 0007](adr/0007-cross-module-contract-form.md) — cross-module contract form.**
  [ADR 0002](adr/0002-modular-monolith-layout.md) settled that the contract lives in
  `Ritocode.Shared`; this settles the shape. Thin read-interfaces, one per consumer need, taken as
  constructor parameters — chosen over an in-process mediator because a mediator satisfies ADR
  0002's letter while removing the property it exists for: with it, coupling stops appearing in any
  signature, and neither a reviewer nor `ModuleBoundaryTests` can see a new cross-module edge.
  Contracts answer facts and never policy, so they return the row's data or `null` rather than a
  `Result<T>` whose error code would be chosen by the wrong module. Contract types are their own
  flat records in `Shared`, never a module's domain entity. Read-only for the slice; a write need
  supersedes the ADR rather than editing it. Three architecture-test assertions are specified and
  land with the first contract, in stage 3. Closes no issue, as planned.

## Stage 2 — Content and catalog

The first module that reads and writes its schema, the first real content, and the frontend gets
started early so its CI job stops waiting.

- [ ] **[#5](https://github.com/shoraLBRT/ritocode/issues/5) (partial) — storage key layout.**
  Bucket naming and object key conventions for problem bundles, workspace snapshots and evaluation
  artifacts, documented. Retention rules are deferred with
  [#43](https://github.com/shoraLBRT/ritocode/issues/43).
- [ ] **[#5](https://github.com/shoraLBRT/ritocode/issues/5) (partial) — object storage client.**
  Put and get against the MinIO already running in `compose.yaml`. No fake implementation: a stub
  costs more to replace than the client costs to write.
- [ ] **[#9](https://github.com/shoraLBRT/ritocode/issues/9) (partial) — catalog.** List published
  problem versions and fetch one by slug, over `Page<T>` and `PageRequest`. Search, facets, tag and
  difficulty filters, explicit version resolution: all deferred.
- [ ] **[#42](https://github.com/shoraLBRT/ritocode/issues/42) (partial) — three problems.** Three,
  not ten, all in the language chosen in `PROJECT_STATE.md`. Three is the minimum that shows the
  verdict distinguishes a good solution from a bad one rather than being tuned to a single task.
  Each has a known-good and a known-bad solution committed as fixtures.
- [ ] **[#26](https://github.com/shoraLBRT/ritocode/issues/26) — frontend shell and API client.**
  React + Vite + TypeScript, the error envelope from [ADR 0003](adr/0003-api-conventions.md) handled
  in one place, routing, layout.
- [ ] **[#31](https://github.com/shoraLBRT/ritocode/issues/31) (partial) — frontend CI job.**
  Build, typecheck and lint the frontend. Closes the half of the issue that has been waiting for a
  frontend to exist; the issue stays open until the full pipeline is settled.

## Stage 3 — Identity and workspace

Editing existing code is the product. This is also where the module boundary gets exercised for the
first time.

- [ ] **[#6](https://github.com/shoraLBRT/ritocode/issues/6) (partial) — identity seam.**
  `ICurrentUser`, authentication middleware, and a seeded development identity behind it.
  `workspaces.user_id` and `submissions.user_id` are `IsRequired()`, so a user is not optional.
  Endpoints take the user from the seam and never from the request — see the forbidden list in
  ADR 0005. Login, session issuance and `/me` land in stage two.
- [ ] **Cross-module contract in `Ritocode.Shared`.** Per
  [ADR 0007](adr/0007-cross-module-contract-form.md): the Workspaces module asks whether a user and
  a problem version exist before creating a row.
  [ADR 0004](adr/0004-persistence-and-migrations.md) requires this — there is no foreign key across
  schemas to do it for us. Two interfaces, `IUserLookup` and `IProblemVersionLookup`, returning the
  row's summary or `null`. The three architecture-test assertions in ADR 0007 §7 ship **in this
  PR**, not after: assertion 3 is what turns a missing DI registration back into a test failure
  instead of a startup failure.
- [ ] **[#10](https://github.com/shoraLBRT/ritocode/issues/10) — create workspace from a problem
  version.** Materialises the bundle into a workspace snapshot. Never from a problem, always from a
  version.
- [ ] **[#11](https://github.com/shoraLBRT/ritocode/issues/11) — file tree and file read.**
- [ ] **[#12](https://github.com/shoraLBRT/ritocode/issues/12) — file write and draft persistence.**
- [ ] **[#36](https://github.com/shoraLBRT/ritocode/issues/36) (partial) — path and size limits.**
  Path normalisation, no escaping the workspace root, no symlink traversal, limits on file size and
  file count. Ships **in the same PR as #12**, not after it. Deferred as a whole this is not
  technical debt, it is a hole.
- [ ] **[#35](https://github.com/shoraLBRT/ritocode/issues/35) (partial) — ownership guards.**
  Every workspace and submission endpoint checks that the resource belongs to the caller, returning
  404 rather than 403 per [ADR 0003](adr/0003-api-conventions.md). Without this the identity seam
  is decorative: `user_id` comes from `ICurrentUser` exactly as the ADR requires, and anyone can
  still read and write anyone else's workspace by id. Not hardening — the authorisation half of
  having authentication at all.

## Stage 4 — Submission and queue

The button exists and the state machine is real, but nothing runs yet.

- [ ] **[#14](https://github.com/shoraLBRT/ritocode/issues/14) — submission lifecycle and attempt
  history.** `Queued` → `Running` → `Completed` / `Failed`, with
  `ck_submissions_completed_at_matches_status` holding.
- [ ] **[#15](https://github.com/shoraLBRT/ritocode/issues/15) (partial) — queue and worker.**
  A PostgreSQL table drained with `SKIP LOCKED`, and a hosted service in the API process. The
  partial index `(status, created_at) WHERE status IN ('Queued','Running')` is already in the
  schema — it was designed for exactly this query. No Redis. Extracting the worker into its own
  process is stage two and must not require domain changes.
- [ ] **[#18](https://github.com/shoraLBRT/ritocode/issues/18) — validator plugin interface.**
  This is what makes "two validators instead of four" an addition later rather than a rewrite, so
  it comes before any validator is written.
- [ ] **[#17](https://github.com/shoraLBRT/ritocode/issues/17) (partial) — orchestrator.**
  Sequential validator execution and status transitions. No retries, priorities, cancellation or
  parallelism.
- [ ] **[#35](https://github.com/shoraLBRT/ritocode/issues/35) (partial) — submission rate limit.**
  A cap on submissions per user per window, plus a cap on how many evaluations run at once. A
  submission starts a container: without a limit, one impatient tester — or one loop in a browser
  tab — is a denial of service against your own test, and an open invitation to use the runner as
  free compute. `RateLimited` is already in `ErrorType` and maps to 429.

## Stage 5 — Execution

The verdict becomes real. This is the stage that cannot be faked — see the first row of the
forbidden list.

- [ ] **[#21](https://github.com/shoraLBRT/ritocode/issues/21) (partial) — sandbox runner.**
  Per ADR 0006. Network disabled, cpu / memory / pid limits, read-only root filesystem, non-root
  user, hard timeout, artifacts captured. No warm pool, no cluster.
- [ ] **[#22](https://github.com/shoraLBRT/ritocode/issues/22) (partial) — one runner image**, for
  the chosen language. The image matrix is stage two.
- [ ] **[#19](https://github.com/shoraLBRT/ritocode/issues/19) (partial) — compile validator.**
- [ ] **[#19](https://github.com/shoraLBRT/ritocode/issues/19) (partial) — test validator.** Lint
  and patch-scope are stage two, added as plugins.
- [ ] **[#20](https://github.com/shoraLBRT/ritocode/issues/20) — scoring and verdict aggregation.**
  The rules are part of what a person is shown; an opaque number devalues the verdict.
- [ ] **[#23](https://github.com/shoraLBRT/ritocode/issues/23) — runner logs and artifacts.**
  Captured to object storage under the keys from stage 2, referenced by
  `submission_reports.logs_reference`.
- [ ] **[#38](https://github.com/shoraLBRT/ritocode/issues/38) (partial) — determinism tests.**
  Unit tests for both validators, and a test that evaluates the same submission twice and asserts
  an identical score and report. Determinism is the claim the whole product rests on — asserting it
  in a definition of done and never testing it is how it quietly stops being true. The full suite,
  including runner integration across images, is stage two.
- [ ] **[#33](https://github.com/shoraLBRT/ritocode/issues/33) (partial) — structured logging.**
  `RequestIdMiddleware` and `X-Request-Id` already work; the correlation id has to reach the worker
  and the runner, or a failed evaluation cannot be traced. Dashboards are
  [#34](https://github.com/shoraLBRT/ritocode/issues/34), deferred.

## Stage 6 — Product face

After this stage the slice can be put in front of someone without an explanation.

- [ ] **[#27](https://github.com/shoraLBRT/ritocode/issues/27) — catalog and problem screens.**
- [ ] **[#28](https://github.com/shoraLBRT/ritocode/issues/28) (partial) — workspace editor.**
  Monaco with a flat file list. No tabs, no diff view, no file search, no settings.
- [ ] **[#16](https://github.com/shoraLBRT/ritocode/issues/16) — submission result and report API.**
- [ ] **[#29](https://github.com/shoraLBRT/ritocode/issues/29) — submission status and result UI.**
  The per-validator breakdown, not just a score.
- [ ] **[#39](https://github.com/shoraLBRT/ritocode/issues/39) (partial) — one end-to-end smoke
  test** over the whole journey, against a real sandbox run.
- [ ] **Slice review.** Run the full verification list, walk the journey by hand, and write down in
  `PROJECT_STATE.md` what the test needs: how someone gets an identity, what to watch, what counts
  as a positive result.

---

## Definition of done for the slice

All of these, together:

1. A person with no explanation can open the catalog, pick a problem, edit it, submit, and get a
   verdict with a per-validator breakdown.
2. The same submission evaluated twice produces the same result.
3. User code has executed only inside a sandbox runner, never in the API or worker process.
4. `dotnet build --warnaserror`, `dotnet test` and `db-verify-no-drift.sh` are clean, and the test
   count is higher than the 54 this started from.
5. No item from ADR 0005's forbidden list is present in the codebase.

## Not in the slice

Listed so nobody builds them by accident, and so nobody mistakes their absence for an oversight:
profile, XP, levels and leaderboard; GitHub login; workspace reset and snapshot history; catalog
search and filters; lint and patch-scope validators; a second language; metrics and dashboards;
cleanup jobs; the rest of the security baseline — input validation hardening, security headers,
CORS policy; and the rest of the validator and runner test suites, including runner integration
across images.

## After the slice

Stage two, ordered by what starts hurting first. None of it requires touching the domain — each
step either substitutes an implementation behind an existing seam or adds a screen.

1. Real sign-in: [#6](https://github.com/shoraLBRT/ritocode/issues/6) completed and
   [#7](https://github.com/shoraLBRT/ritocode/issues/7), behind the seam built in stage 3.
2. The remaining validators — lint and patch-scope in
   [#19](https://github.com/shoraLBRT/ritocode/issues/19) — then the rest of
   [#38](https://github.com/shoraLBRT/ritocode/issues/38).
3. Progress, XP and leaderboard: [#24](https://github.com/shoraLBRT/ritocode/issues/24),
   [#25](https://github.com/shoraLBRT/ritocode/issues/25),
   [#30](https://github.com/shoraLBRT/ritocode/issues/30) — **only if the test showed people come
   back.**
4. A second language: the image matrix in [#22](https://github.com/shoraLBRT/ritocode/issues/22),
   more problems in [#42](https://github.com/shoraLBRT/ritocode/issues/42).
5. Hardening before anything is public: the rest of
   [#35](https://github.com/shoraLBRT/ritocode/issues/35),
   [#36](https://github.com/shoraLBRT/ritocode/issues/36) in full,
   [#34](https://github.com/shoraLBRT/ritocode/issues/34),
   [#43](https://github.com/shoraLBRT/ritocode/issues/43).
6. Extract the evaluation worker into its own process; a broker instead of the table only if the
   table stops keeping up.
7. Close the phase: [#40](https://github.com/shoraLBRT/ritocode/issues/40),
   [#41](https://github.com/shoraLBRT/ritocode/issues/41), the rest of
   [#39](https://github.com/shoraLBRT/ritocode/issues/39) and
   [#31](https://github.com/shoraLBRT/ritocode/issues/31). Only then Phase 2.
