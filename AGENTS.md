# AGENTS.md

This file tells an AI agent how to work in the Ritocode repository.

## Start here

1. [`docs/CONCEPT.md`](docs/CONCEPT.md) — what Ritocode is: a catalogue of what breaks in AI-written
   code, and a trainer that teaches people to recognise it.
2. [`docs/SPEC.md`](docs/SPEC.md) — what gets built, screen by screen and rule by rule.
3. [`docs/PROJECT_STATE.md`](docs/PROJECT_STATE.md) — what exists, what is next, and the commands
   that verify a change. It is the part that moves.
4. [`docs/ROADMAP.md`](docs/ROADMAP.md) — the stages, and the order work is taken in.

Then, as needed:

- [`docs/adr/`](docs/adr/) — decisions already made, and why. Do not relitigate them in code.
- [`docs/AGENT_GUIDELINES.md`](docs/AGENT_GUIDELINES.md) — how to write code here.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md),
  [`docs/DATABASE_SCHEMA.md`](docs/DATABASE_SCHEMA.md) — the system, its entities and its tables.
- [`docs/CONTENT_FORMAT.md`](docs/CONTENT_FORMAT.md) — how problem cards, materials and tasks are
  written under `content/`.

## Where the work comes from

The backlog is the [project board](https://github.com/users/shoraLBRT/projects/3). Every MVP issue
is labelled `phase:mvp` and sits in a stage milestone, `S0` to `S8`.

```bash
gh issue list --repo shoraLBRT/ritocode --state open --label phase:mvp --limit 60
```

Issues are the source of truth for what is done. `docs/ROADMAP.md` holds what an issue list cannot:
the order, the dependencies, and what "done" means for each stage. Take the first open issue of the
lowest open stage whose dependencies are met.

A coding session does not take `type:content` issues (the catalogue, the tasks) or the maintainer's
own launch prerequisites ([#134](https://github.com/shoraLBRT/ritocode/issues/134)) unless the
maintainer asks. Stop and ask before building when an issue needs an architectural or business
decision that [`SPEC.md`](docs/SPEC.md) §13 and the open questions in `PROJECT_STATE.md` do not make.

## Non-negotiables

1. Follow the architecture in `ARCHITECTURE.md` and the ADRs. A module never references another
   module — see [ADR 0002](docs/adr/0002-modular-monolith-layout.md); the rule is enforced by
   `tests/Ritocode.Architecture.Tests`.
2. Respect the entities in `DOMAIN_MODEL.md`, and update that document when they change.
3. No microservices. The monolith stays modular until a boundary earns its own process.
4. Nothing a learner writes is ever executed. Diagnosis is reading and choosing.
5. Scoring is deterministic: the same answer to the same task version produces the same result, and
   a stored result is never rewritten by a later change to content or parameters.
6. A user's rows are reached only with the owner inside the query, and another user's row answers
   exactly like a missing one — enforced by the ownership architecture test.
7. Every API response follows [ADR 0003](docs/adr/0003-api-conventions.md) — the unified error body,
   the status-code mapping, and the pagination envelope.
8. No schema change without a migration.

## Working rules

Prefer explicit domain models, small functions, deterministic logic and clear APIs.
Avoid hidden side effects, dynamic runtime magic, and frameworks introduced without a stated reason.

- Work on a branch, one issue per branch. Never commit to `main`.
- Ship tests with the code. `dotnet build` and `dotnet test` must be clean — warnings are errors,
  vulnerability warnings included; a newly disclosed CVE is fixed by pinning the package forward.
- A decision that outlives the session goes in an ADR under `docs/adr/`, not in a commit message.
- Update `DOMAIN_MODEL.md` and `DATABASE_SCHEMA.md` when entities or tables change.
- Open a PR that references the issue, and comment on the issue with what landed and what did not.
- Leave an issue open if the work is partial, and say so explicitly rather than implying completion.
- Update `docs/PROJECT_STATE.md` in the same PR. A session that skips this makes the next one start
  from nothing.
- Keep authoring cheap. A mechanic that makes writing a task or a card harder needs a very good
  reason; prefer what the system can compute.
