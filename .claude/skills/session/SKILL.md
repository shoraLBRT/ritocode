---
name: session
description: Run one Ritocode work session end to end — orient in the docs, take the next issue from the roadmap, branch, build, verify, open a PR, merge it when the maintainer has allowed that, and leave the docs true for the next session. Use this whenever work is about to start or resume in this repository: "what's next", "take the next task", "let's continue", "pick something from the roadmap", "open a PR for this", "wrap up the session". Also use it when entering mid-way — someone has already built something and needs the verify / ship / record half done properly. Prefer it over improvising the workflow, because the ordering and the partial-work rules here are the part an issue list cannot express.
---

# Ritocode session

This repository is built by sessions that do not share memory. What makes it continue is the handoff
at the end of one: the docs describe reality, the issue says what is really left, and the next
session can start from `docs/PROJECT_STATE.md` alone.

Most of the cost of getting this wrong is invisible at the time. An issue closed over unfinished
work, a caveat buried in a commit message, a decision made in code instead of an ADR — none of them
break the build. They break the session three weeks from now that trusts the docs.

## Phases

| # | Phase | Ends when |
| --- | --- | --- |
| 1 | Orient | The checkout is current, and you can name the current stage and the next issue |
| 2 | Pick | One issue is chosen, open, with its dependencies met |
| 3 | Branch | You are off a freshly fetched `origin/main` |
| 4 | Build | Code and its tests exist |
| 5 | Verify | Everything in the verification section passes |
| 6 | Ship | PR open, issue commented; merged if the maintainer allows it |
| 7 | Record | The docs describe what the PR does |

Entering mid-way is fine — if the code exists and only the shipping half is left, start at
**Verify**, but still skim Orient.

---

### 1. Orient

**Make the checkout current before reading anything.**

```bash
git fetch origin main
```

In a git worktree `main` is usually checked out elsewhere, so read files from the fetched ref
(`git show origin/main:docs/ROADMAP.md`) and branch off `origin/main` by name.

Read, in this order, and actually read them — they move:

- `docs/PROJECT_STATE.md` — what exists, what is next, open questions, verification.
- `docs/ROADMAP.md` — the stages, their exit criteria, and the issues in order.
- `docs/SPEC.md` — the sections the next issue points at.
- Any ADR the issue depends on. `AGENTS.md` lists the rest of the map.

Then say, in two or three lines: the current stage, the next issue, and anything that blocks it.

### 2. Pick

Take the **first open issue of the lowest open stage** in `docs/ROADMAP.md` whose *Depends on* is
met — not the first interesting one. Confirm it is open:

```bash
gh issue list --repo shoraLBRT/ritocode --state open --label phase:mvp --limit 60
```

Issues of type `content` (the catalogue, the 20 tasks) and the maintainer's own launch
prerequisites are **not** taken by a coding session unless the maintainer asks.

**Stop and ask the maintainer before building when:**

- the issue needs an architectural or business decision the spec does not make — check
  `docs/SPEC.md` §13 and **Open questions** in `PROJECT_STATE.md` first;
- the issue turns out to be wrong. The plan may move; say why in the PR;
- a problem needs the maintainer's hands — credentials, an account, a paid service.

### 3. Branch

```bash
git checkout -b feat/<issue>-<short-slug> origin/main
```

Prefix by what the change is: `feat/`, `fix/`, `test/`, `docs/`, `chore/`. **Never commit to
`main`.**

### 4. Build

Follow `docs/AGENT_GUIDELINES.md`, `AGENTS.md` and the ADRs. The rules broken most often:

- **A module never references another module.** Design around it rather than discovering it at
  verification.
- **Tests ship with the code, not after.**
- **A test that needs PostgreSQL takes it from `tests/Ritocode.TestSupport`.** No fixture of your own.
- **A decision that outlives the session goes in an ADR**, not in a commit message.
- **Keep authoring cheap.** Content formats and authoring tools must not make writing a task harder
  than it has to be.

### 5. Verify

Run **everything** under Verification in `docs/PROJECT_STATE.md` — read it there, not from memory.

- **Docker has to be running** for `dotnet test`.
- **Warnings are errors**, vulnerability warnings included; a new CVE is fixed by pinning forward.
- The test baseline in `PROJECT_STATE.md` is a ratchet. If it drops because the thing tested is gone,
  say so in the PR.

### 6. Ship

Commit, push, open a PR that names the issue.

- The PR body says what landed **and what was deliberately left out**.
- `Closes #N` **only when the issue is genuinely finished.** Partial work references the issue
  without a closing keyword and leaves it open.
- Comment on the issue with the same summary.

**Merging.** The maintainer merges by default. On 2026-09-30 they gave a standing mandate for
roadmap work: merge your own PR once CI is green, then take the next issue — stopping only for an
architectural or business decision, or a problem that needs them. Before starting each new issue,
check the plan usage (`get_usage`) and do not start one when the 5-hour window is more than 80%
used. If the maintainer withdraws the mandate, it is withdrawn.

### 7. Record

Update the docs **in the same PR as the code**:

- `PROJECT_STATE.md`: move the work into **What exists** (or say what is left of a partial issue),
  refresh **Next up**, **Last updated**, the test baseline, and the smoke checks if endpoints
  changed.
- `DOMAIN_MODEL.md` and `DATABASE_SCHEMA.md` when entities or tables change.
- **Open questions**: anything a future session would otherwise have to rediscover.

When a stage's exit criterion has been shown to work, say so in `PROJECT_STATE.md` and move
**Current stage** on.

---

## Partial work is normal here

Some issues span several pull requests — the content issues always do. Say so plainly, every time,
in the PR body, the issue comment and `PROJECT_STATE.md`. Partial is fine. Ambiguous is not.
