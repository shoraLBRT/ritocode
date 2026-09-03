# 0005 — Vertical slice before breadth

- Status: Accepted
- Date: 2026-09-03
- Relates to: [`docs/MVP_SCOPE.md`](../MVP_SCOPE.md), [`docs/SLICE_PLAN.md`](../SLICE_PLAN.md)
- Builds on: [0002](0002-modular-monolith-layout.md), [0003](0003-api-conventions.md), [0004](0004-persistence-and-migrations.md)

## Context

Phase 1 as scoped is 38 open issues, roughly 50–65 sessions. Ordered by layer — catalog, then
storage, auth, workspaces, submissions, evaluation, runner, and the frontend last — it puts the two
riskiest pieces at the end: the sandbox runner and the workspace editor. The first time anyone can
actually use the product is three to four months in.

The product rests on one untested claim: *a developer will keep solving "improve this existing
code" tasks if the verdict is deterministic and explains what was wrong.* Nothing in the layered
order tests that claim until nearly all of it is built. Two things in it are also unproven and
cheap to check early — whether a task on code quality can be authored without a workaround and with
a reproducible verdict, and whether the sandbox host works the way the orchestrator will assume.

The maintainer decided to cut one complete user journey first and put it in front of real people.

The risk this ADR exists to contain is not delivery risk but the usual cost of a slice: it is built
for speed, and the shortcuts outlive the experiment. The decision here is therefore **less of
Phase 1, not a worse Phase 1**. Every reduction has to be reversible by substitution behind a seam
that already exists, and the reductions that are not reversible are named and forbidden.

## Decision

Phase 1 ships in two stages.

**Stage one — the slice.** One journey, end to end, at full architectural quality: browse the
catalog, open a workspace on a problem version, edit files, submit, evaluate inside a real sandbox,
read the verdict and the per-validator report. Roughly 28–32 sessions. Tracked, item by item, in
[`docs/SLICE_PLAN.md`](../SLICE_PLAN.md).

**Stage two — the rest of Phase 1**, in the order at the end of that file. `docs/MVP_SCOPE.md` is
unchanged: it still defines what Phase 1 must deliver. This ADR changes the order and the first
milestone, not the destination.

### Reductions that are allowed

Each is reversible because the seam named beside it is built during the slice and stays.

| Reduction | Seam that makes it reversible |
| --- | --- |
| One task language, one runner image | `ISandboxRunner` is language-agnostic; another image is a configuration entry |
| A seeded development identity instead of a login | `ICurrentUser` and the authentication middleware exist; no endpoint changes when a real provider is wired in |
| Queue as a PostgreSQL table with `SKIP LOCKED` | One dispatch interface; a broker is a different implementation, not a different domain |
| Evaluation worker hosted inside the API process | The domain does not know where it runs; extracting it to its own project moves no domain code |
| No progress, XP or leaderboard | The Progress module consumes submissions after the fact; it supplies nothing and blocks nothing |
| Catalog without search, facets or tag filters | `Page<T>` and `PageRequest` already fix the shape of a list response |
| Two validators — compile and tests — instead of four | The plugin interface from [#18](https://github.com/shoraLBRT/ritocode/issues/18) makes the other two an addition, not a rewrite |
| Editor without tabs, diff view or file search | User interface volume; it touches no architecture |

The runner for the slice is `docker run` from the worker host with `--network none`, cpu, memory
and pid limits, a read-only root filesystem, a non-root user and a hard timeout. A warm pool, a
dedicated runner VM and Docker-in-Docker are all still open; that decision belongs to its own ADR
and is deferred until the queue makes it matter.

### Reductions that are forbidden

These break the domain, the schema or the security boundary in ways a later session cannot undo
cheaply. A session that finds one of them in the code fixes it rather than building on it.

| Forbidden | What it costs later |
| --- | --- |
| Executing user code outside a sandbox runner — including "just for our own tasks" | The whole evaluation path is rewritten, and until then anyone testing the product owns the host. Restates the rule in `docs/AGENT_GUIDELINES.md` |
| Taking `user_id` from a request body or query string | Every workspace and submission endpoint is rewritten, and each one carries an IDOR until that happens |
| Serving a workspace or submission without checking it belongs to the caller | The identity seam becomes decorative — the user comes from `ICurrentUser` exactly as required, and every resource is still readable and writable by id. Authorisation is not part of the hardening that gets deferred; it is the other half of having authentication |
| Reaching into another module's `DbContext` | `ModuleBoundaryTests` fails, correctly. Cross-module needs go through a contract in `Ritocode.Shared` — [ADR 0002](0002-modular-monolith-layout.md) |
| Evaluating synchronously in the HTTP request, skipping `Queued` and `Running` | `ck_submissions_completed_at_matches_status` becomes a lie and the status machine a fiction, repaired later with production rows already in the table |
| Writing repository tests on ad-hoc fixtures instead of [#37](https://github.com/shoraLBRT/ritocode/issues/37) | Every module's tests are rewritten the first time one of them needs database isolation |
| Changing the schema without a migration | CI already fails on model drift; working around the check removes the only guard the schema has |
| Writing workspace files at paths taken from the request without normalisation | Path traversal on day one of the test — an incident, not technical debt |
| Authoring problems as "just a folder" before the manifest in [#8](https://github.com/shoraLBRT/ritocode/issues/8) | Every task written so far is redone against the format that arrived late |

## Alternatives considered

**Finish Phase 1 first.** The destination anyway, and no partial issues to reconcile. Rejected: it
answers the product question last, after the most expensive work is already paid for. If the
hypothesis is wrong, that is the worst possible moment to learn it.

**A headless slice — API only, driven over HTTP.** Six sessions cheaper, and it still proves the
pipeline end to end. Rejected as the milestone, because a person who has to be talked through
`curl` is not testing the product, they are testing the explanation. Kept as the state the slice
passes through in week five.

**A throwaway prototype outside the solution.** Fastest to a demo. Rejected on the maintainer's
explicit constraint: the experiment must not become the reason the real path was built wrong. A
prototype outside the boundary teaches nothing about whether the boundary works.

**Content dry run only — the manifest and a handful of tasks, validated locally.** Not rejected;
absorbed. It is the first week of the slice, and it is where the authoring risk actually gets
tested, before any of the API exists.

## Consequences

- **A ticked box in `SLICE_PLAN.md` is not a closed issue.** Several Phase 1 issues are entered
  partially. Those stay open with a comment saying what landed and what was deliberately left, per
  `AGENTS.md`. GitHub issues remain the source of truth for issue status; the plan tracks slice
  progress, which an issue list cannot express.
- The Progress module stays empty for the whole slice. That is expected, not an oversight.
- Authentication ships as a seam before it ships as a feature. Every endpoint written during the
  slice takes its user from `ICurrentUser`, so the endpoints do not change when [#6](https://github.com/shoraLBRT/ritocode/issues/6)
  and [#7](https://github.com/shoraLBRT/ritocode/issues/7) are completed.
- The evaluation worker runs in the API process during the slice. Domain code must stay unaware of
  that, or the extraction in stage two stops being a move and becomes a rewrite.
- The frontend starts in week two rather than at the end, which finally closes the frontend half of
  [#31](https://github.com/shoraLBRT/ritocode/issues/31).
- Two decisions are pulled forward into week one because everything downstream assumes an answer:
  the sandbox host shape, and the form of the cross-module contract needed by
  [#10](https://github.com/shoraLBRT/ritocode/issues/10). Each gets its own ADR.
- This ADR is superseded, not edited, if the slice's result changes the plan.
