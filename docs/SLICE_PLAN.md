# Slice Plan — Phase 1, stage one

The current milestone: one complete user journey, end to end, at full architectural quality.
Browse the catalog → open a workspace on a problem version → edit files → submit → evaluate inside
a real sandbox → read the verdict and the per-validator report.

Decided in [ADR 0005](adr/0005-vertical-slice-before-breadth.md), which also carries the list of
reductions that are allowed and the list that are forbidden. **Read that ADR before ticking
anything here.** This file tracks progress; it holds no decisions.

- **Last updated:** 2026-09-03
- **Progress:** 0 / 34
- **Estimate:** 28–32 sessions, six weeks at five sessions a week
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
| [1 — Foundation](#stage-1--foundation) | 5 | 0 / 5 |
| [2 — Content and catalog](#stage-2--content-and-catalog) | 5 | 0 / 6 |
| [3 — Identity and workspace](#stage-3--identity-and-workspace) | 5 | 0 / 6 |
| [4 — Submission and queue](#stage-4--submission-and-queue) | 5 | 0 / 4 |
| [5 — Execution](#stage-5--execution) | 6 | 0 / 7 |
| [6 — Product face](#stage-6--product-face) | 6 | 0 / 6 |

---

## Stage 1 — Foundation

Everything here gets more expensive the longer it waits. Nothing downstream is safe to start until
the two ADRs are written.

- [ ] **[#37](https://github.com/shoraLBRT/ritocode/issues/37) — Integration test harness.**
  Testcontainers, one database per test class. Existing API tests move onto it and the current
  baseline of 54 tests stays green.
- [ ] **[#8](https://github.com/shoraLBRT/ritocode/issues/8) — Problem package manifest.**
  `problem.yaml` schema, the shape of `validator_config`, allowed paths, hints, constraints. The
  `problem_versions.validator_config` column is already `jsonb` and waiting for this. An example
  package validates against the schema in a test.
- [ ] **Spike — sandbox execution.** Time-boxed, no issue closed. Run a real task under `docker run`
  with `--network none`, cpu / memory / pid limits, read-only root, non-root user and a timeout.
  Find out what actually works on the development machine and what the orchestrator can assume.
- [ ] **ADR 0006 — sandbox execution model.** Written from the spike. Fixes what the runner
  contract looks like for the slice and states plainly that the production host — warm pool,
  dedicated VM, Docker-in-Docker — is deferred.
- [ ] **ADR 0007 — cross-module contract form.** [ADR 0002](adr/0002-modular-monolith-layout.md)
  already settled that the contract lives in `Ritocode.Shared`; this settles the shape. Thin
  read-interfaces, one per need, over an in-process mediator — `docs/AGENT_GUIDELINES.md` warns
  against dynamic runtime magic, and a mediator hides the coupling the interface would show in a
  signature. First consumer is [#10](https://github.com/shoraLBRT/ritocode/issues/10).

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
- [ ] **Cross-module contract in `Ritocode.Shared`.** Per ADR 0007: the Workspaces module asks
  whether a user and a problem version exist before creating a row.
  [ADR 0004](adr/0004-persistence-and-migrations.md) requires this — there is no foreign key across
  schemas to do it for us.
- [ ] **[#10](https://github.com/shoraLBRT/ritocode/issues/10) — create workspace from a problem
  version.** Materialises the bundle into a workspace snapshot. Never from a problem, always from a
  version.
- [ ] **[#11](https://github.com/shoraLBRT/ritocode/issues/11) — file tree and file read.**
- [ ] **[#12](https://github.com/shoraLBRT/ritocode/issues/12) — file write and draft persistence.**
- [ ] **[#36](https://github.com/shoraLBRT/ritocode/issues/36) (partial) — path and size limits.**
  Path normalisation, no escaping the workspace root, no symlink traversal, limits on file size and
  file count. Ships **in the same PR as #12**, not after it. Deferred as a whole this is not
  technical debt, it is a hole.

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
rate limiting and the rest of the security baseline; cleanup jobs; the full validator and runner
test suites.

## After the slice

Stage two, ordered by what starts hurting first. None of it requires touching the domain — each
step either substitutes an implementation behind an existing seam or adds a screen.

1. Real sign-in: [#6](https://github.com/shoraLBRT/ritocode/issues/6) completed and
   [#7](https://github.com/shoraLBRT/ritocode/issues/7), behind the seam built in stage 3.
2. The remaining validators — lint and patch-scope in
   [#19](https://github.com/shoraLBRT/ritocode/issues/19) — then
   [#38](https://github.com/shoraLBRT/ritocode/issues/38).
3. Progress, XP and leaderboard: [#24](https://github.com/shoraLBRT/ritocode/issues/24),
   [#25](https://github.com/shoraLBRT/ritocode/issues/25),
   [#30](https://github.com/shoraLBRT/ritocode/issues/30) — **only if the test showed people come
   back.**
4. A second language: the image matrix in [#22](https://github.com/shoraLBRT/ritocode/issues/22),
   more problems in [#42](https://github.com/shoraLBRT/ritocode/issues/42).
5. Hardening before anything is public: [#35](https://github.com/shoraLBRT/ritocode/issues/35),
   [#36](https://github.com/shoraLBRT/ritocode/issues/36) in full,
   [#34](https://github.com/shoraLBRT/ritocode/issues/34),
   [#43](https://github.com/shoraLBRT/ritocode/issues/43).
6. Extract the evaluation worker into its own process; a broker instead of the table only if the
   table stops keeping up.
7. Close the phase: [#40](https://github.com/shoraLBRT/ritocode/issues/40),
   [#41](https://github.com/shoraLBRT/ritocode/issues/41), the rest of
   [#39](https://github.com/shoraLBRT/ritocode/issues/39) and
   [#31](https://github.com/shoraLBRT/ritocode/issues/31). Only then Phase 2.
