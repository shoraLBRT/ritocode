---
name: session
description: Run one Ritocode work session end to end — orient in the docs, take the next box from the slice plan, branch, build, verify, open a PR, and leave the docs true for the next session. Use this whenever work is about to start or resume in this repository: "what's next", "take the next task", "let's continue", "pick something from the slice plan", "open a PR for this", "wrap up the session". Also use it when entering mid-way — someone has already built something and needs the verify / ship / record half done properly. Prefer it over improvising the workflow, because the ordering and the partial-work rules here are the part an issue list cannot express.
---

# Ritocode session

This repository is built by sessions that do not share memory. What makes it continue is not
cleverness inside a session, it is the handoff at the end of one: the docs describe reality, the
issue says what is really left, and the next session can start from `docs/PROJECT_STATE.md` alone.

Most of the cost of getting this wrong is invisible at the time. A ticked box over unfinished work,
an issue closed with a caveat buried in a commit message, a decision made in code instead of an
ADR — none of them break the build. They break the session three weeks from now that trusts the
docs and finds them lying.

So the discipline below is not bureaucracy around the real work. It *is* part of the real work.

## Phases

Seven phases. A session normally runs all of them, but entering mid-way is common and fine — if the
code already exists and only the shipping half is left, start at **Verify**. If you enter mid-way,
still skim Orient: you cannot judge whether work is in scope without knowing which stage the project
is in.

| # | Phase | Ends when |
| --- | --- | --- |
| 1 | Orient | The checkout is current, and you can name the current stage and the next box out loud |
| 2 | Pick | One item is chosen and its issue confirmed open |
| 3 | Branch | You are off a freshly fetched `origin/main` |
| 4 | Build | Code and its tests exist |
| 5 | Verify | Everything in the verification section passes |
| 6 | Ship | PR open, issue commented |
| 7 | Record | The docs describe what the PR does |

---

### 1. Orient

**Make the checkout current before reading anything.** The files below are only as true as the tree
they are read from, and this repository is worked by sessions that do not share memory — the branch
you start on is as old as the last time *someone* here updated it, which may be several merged PRs
ago. Read a stale copy and the plan on `main` says a box is ticked while your copy still offers it
as the next task. The failure is silent, because a stale `SLICE_PLAN.md` is a perfectly well-formed
file with nothing wrong on its face.

```bash
git fetch origin main
git rev-list --left-right --count main...origin/main
```

The second command prints two numbers: how far the local `main` is **ahead** of the remote, then how
far **behind**. Behind is ordinary and is the whole reason for this step. Ahead is not — a non-zero
first number means a commit landed on `main` directly, against the rule in **Branch** below. Stop
and ask the maintainer rather than merging it away, because the merge hides how it got there.

Then read from `origin/main` rather than from whatever the working tree happens to hold. Moving the
local branch up is the direct way:

```bash
git checkout main && git pull --ff-only origin main
```

In a git worktree that command usually fails, because `main` is checked out in another worktree —
and a failure here is exactly when the step is most tempting to skip and most costly to skip. Read
the files straight from the fetched ref instead (`git show origin/main:docs/SLICE_PLAN.md`), and
branch off `origin/main` by name in **Branch**.

Read, in this order — actually read them, do not rely on what a previous session or a summary said
they contain, because these files move and being wrong about them is how work lands in the wrong
stage:

- `docs/PROJECT_STATE.md` — what exists, what is next, what is deliberately deferred, and the
  verification commands.
- `docs/SLICE_PLAN.md` — the ordered checklist work is taken from.
- `docs/adr/0005-vertical-slice-before-breadth.md` — the reductions the slice may make and, more
  importantly, the ones it may not.
- Any ADR the item you are about to take depends on. `AGENTS.md` lists the rest of the map.

Then say back to the maintainer, in two or three lines: the current stage, the first unticked box,
and anything that blocks it. This is cheap and it catches the expensive mistake — starting work the
project has already moved past — before any code exists.

### 2. Pick

Take the **first unticked box in `docs/SLICE_PLAN.md`**, not the first interesting one. The stages
are ordered so each depends only on stages above it; skipping ahead means building on a decision
that has not been made yet.

Confirm the issue behind it is still open:

```bash
gh issue list --repo shoraLBRT/ritocode --state open --label phase:1 --limit 60
```

Some boxes close no issue at all — spikes and ADRs. That is intended, not an oversight.

**Stop and ask the maintainer before building when:**

- The box is an ADR or a spike. Those record a decision, and a decision the maintainer has not
  agreed to is worth less than no decision.
- The box hits an entry in the **Open questions** section of `PROJECT_STATE.md` that is marked due
  in this stage and is explicitly the maintainer's call — the language of the first problems is the
  standing example. Guessing there produces work that gets thrown away.
- The box turns out to be wrong. The plan is allowed to move; change it and say why in the PR. The
  ADR's forbidden list is not allowed to move.

### 3. Branch

One issue per branch, off the `main` you made current in **Orient**:

```bash
git checkout -b feat/<short-slug> origin/main
```

Naming `origin/main` explicitly is what makes this work whether or not the local branch could be
moved up — in a worktree it usually could not. If you reached this phase without the fetch in Orient,
go back and do it before branching: branching off a stale `main` builds on code the project no longer
has, and the conflict surfaces at review time rather than now, when it is one command to avoid.

Prefix by what the change is: `feat/`, `fix/`, `test/`, `docs/`, `chore/`.

**Never commit to `main`.** The maintainer merges PRs — that is the review gate, and committing
directly removes it.

### 4. Build

Follow `docs/AGENT_GUIDELINES.md` and the ADRs. The rules that get broken most often here:

- **A module never references another module.** `tests/Ritocode.Architecture.Tests` enforces it, so
  breaking it fails loudly — but design around it rather than discovering it at verification.
- **Tests ship with the code, not after.** A PR whose tests are "next session" is a PR that changes
  the baseline test count downward and hides it.
- **A test that needs PostgreSQL takes it from `tests/Ritocode.TestSupport`.** Do not write a
  fixture of your own — an in-memory provider, a shared database, a hand-rolled container. ADR 0005
  lists ad-hoc fixtures among the forbidden shortcuts precisely because every test written on one
  gets rewritten by the first module that needs isolation.
- **User-submitted code executes only inside sandbox runners.** Never in an API or worker process.
  This one is not a style preference.
- **A decision that outlives the session goes in an ADR**, not in a commit message and not in a
  code comment. If you find yourself explaining *why* at length in a PR description, that is the
  signal.

If you find a shortcut from ADR 0005's forbidden list already in the code, fix it rather than
building on it. It is a defect, not an existing trade-off.

### 5. Verify

Run **everything** under the verification section of `docs/PROJECT_STATE.md` — read it there rather
than from memory, since the commands and the baseline change as the project grows. At the time of
writing it wants a clean `dotnet build --warnaserror`, a clean `dotnet test`, and, against the
compose stack, a clean drift check plus the smoke checks on a running host.

Two things that reliably go wrong:

- **Docker has to be running.** Tests start their own PostgreSQL through Testcontainers, so
  `dotnet test` no longer needs `dev-up` — but with no Docker daemon it fails in a way that looks
  like a code problem and is not.
- **Warnings are errors, vulnerability warnings included.** A newly disclosed CVE reddening a build
  you did not touch is expected behaviour, fixed by pinning the package forward — not by suppressing
  it.

The test count in `PROJECT_STATE.md` is a ratchet: a session that leaves it lower than it found it
has broken something. If it legitimately drops — a test deleted because the thing it tested is
gone — say so explicitly in the PR, because silence there reads as breakage.

No exceptions and no "will fix in CI". CI is the second opinion, not the first.

### 6. Ship

Commit, push, open a PR that names the issue.

- The PR body says what landed **and what was deliberately left out**. The second half is what makes
  a partial PR reviewable instead of misleading.
- `Closes #N` **only when the issue is genuinely finished.** For partial work, reference the issue
  without a closing keyword and leave it open.
- Comment on the issue with the same summary. The board is the source of truth for status, and a
  reader there should not have to open the PR to learn what changed.

### 7. Record

Update the docs **in the same PR as the code**. Merging is what makes the tick true, so a PR that
carries both leaves the repository consistent at every merge commit; a follow-up "docs" PR leaves a
window where the plan lies.

- Tick the box in `docs/SLICE_PLAN.md` and update the progress counters — both the header and the
  stage table — in that same commit.
- If the issue is now fully done, move it into **What exists** in `PROJECT_STATE.md`. If it is
  partial, add it to **Deliberately deferred** with what is missing and what unblocks it.
- Refresh **Last updated** in both files.
- Add to **Open questions** anything a future session would otherwise have to rediscover. This is
  the highest-value line you write all session and the easiest to skip: the question you just spent
  an hour answering, or the one you just walked into and deferred.

Then tell the maintainer what merged-state the PR will produce, and what is left on the issue.

---

## Partial work is normal here

Several items enter an issue on purpose and leave the rest — the slice plan marks them `(partial)`.
This is a deliberate mechanism, not sloppiness: it lets the vertical slice reach depth without the
issue list pretending the breadth is done.

What makes it work is saying so plainly, every time, in all three places: the PR body, the issue
comment, and `PROJECT_STATE.md`. What breaks it is implying completion — a `Closes #N` on partial
work, or a tick over a box whose second half is silently gone.

Partial is fine. Ambiguous is not.

## When to stop and ask rather than decide

- A decision the maintainer has reserved (see **Open questions**).
- Work outside the slice, or outside Phase 1 entirely. Phase 2 and 3 issues exist but are not open
  work until Phase 1 closes.
- A reduction that is not on ADR 0005's allowed list.
- The slice plan itself looking wrong. Say why; do not quietly route around it.

Everything else — how to structure the code, what to name things, which tests to write — is yours to
decide inside the guidelines.
