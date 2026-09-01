# AGENTS.md

This file tells an AI agent how to work in the Ritocode repository.

## Start here

**Read [`docs/PROJECT_STATE.md`](docs/PROJECT_STATE.md) first.** It holds the current state of the
project: what is implemented, what to build next, the open decisions, and the commands that verify a
change. Everything else on this page is context that rarely changes; `PROJECT_STATE.md` is the part
that moves, and it carries the session workflow you are expected to follow.

Then, as needed:

- [`docs/adr/`](docs/adr/) — decisions already made, and why. Do not relitigate them in code.
- [`docs/AGENT_GUIDELINES.md`](docs/AGENT_GUIDELINES.md) — how to write code here.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) —
  the system and its entities.
- [`docs/MVP_SCOPE.md`](docs/MVP_SCOPE.md) — what Phase 1 must deliver.
- [`docs/PROBLEM_PACKAGE_SPEC.md`](docs/PROBLEM_PACKAGE_SPEC.md),
  [`docs/EVALUATION_PIPELINE.md`](docs/EVALUATION_PIPELINE.md) — the training content and how it is
  validated.
- [`docs/SCALING_PLAN.md`](docs/SCALING_PLAN.md) — where service boundaries may go later, and why
  not yet.

## Project goal

Ritocode is a platform for practising code review, code quality, refactoring, performance work and
test quality. Users solve tasks by **improving existing codebases** rather than writing algorithms
from scratch. Solutions are graded by deterministic validators, not by opinion.

## Where the work comes from

The backlog lives on the [project board](https://github.com/users/shoraLBRT/projects/3) as GitHub
issues, labelled by `phase:`, `priority:`, `type:` and `epic:`.

```bash
gh issue list --repo shoraLBRT/ritocode --state open --label phase:1 --limit 60
```

Issues are the source of truth for what is done. `docs/PROJECT_STATE.md` summarises them and, more
importantly, records the ordering and the reasoning that an issue list cannot express.

## Non-negotiables

1. Follow the architecture in `ARCHITECTURE.md` and the ADRs. A module never references another
   module — see [ADR 0002](docs/adr/0002-modular-monolith-layout.md); the rule is enforced by
   `tests/Ritocode.Architecture.Tests`.
2. Respect the entities in `DOMAIN_MODEL.md`, and update that document when they change.
3. No microservices. The monolith stays modular until a boundary earns its own process.
4. User-submitted code executes **only inside sandbox runners**. Never in an API or worker process.
5. Evaluation is deterministic: the same submission produces the same result.
6. Every API response follows [ADR 0003](docs/adr/0003-api-conventions.md) — the unified error body,
   the status-code mapping, and the pagination envelope.

## Working rules

Prefer explicit domain models, small functions, deterministic logic and clear APIs.
Avoid hidden side effects, dynamic runtime magic, and frameworks introduced without a stated reason.

- Work on a branch, one issue per branch. Never commit to `main`.
- Ship tests with the code. `dotnet build` and `dotnet test` must be clean — warnings are errors.
- Open a PR that references the issue, and comment on the issue with what landed and what did not.
- Leave an issue open if the work is partial, and say so explicitly rather than implying completion.
- Update `docs/PROJECT_STATE.md` before finishing. A session that skips this makes the next one
  start from nothing.

## Priority

Finish [`docs/MVP_SCOPE.md`](docs/MVP_SCOPE.md) — Phase 1 — before touching Phase 2 or Phase 3 work.
