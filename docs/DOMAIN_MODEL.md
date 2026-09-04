# Domain Model

The core entities of the system, and which module owns each. The physical schema, indexes and
constraints are in [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md); this document is the conceptual view.

Every entity is owned by exactly one module. An entity is only ever read or written through its
owning module — see [ADR 0002](adr/0002-modular-monolith-layout.md).

## User

Owned by the **Users** module.

Fields:

- id
- email — stored lower-cased, unique
- username — stored lower-cased, unique
- created_at
- xp — never negative
- trust_level — `New`, `Established`, `Trusted`

Trust level only gates behaviour from Phase 3 onward, when contributions reach real repositories.

## LinkedAccount

Owned by the **Auth** module. Links a Ritocode account to an external identity.

Fields:

- id
- user_id — the Ritocode user
- provider — `GitHub` is the only value in Phase 1
- provider_user_id — the provider's immutable identifier, unique per provider
- provider_login — last known login at the provider, for display only, may be stale
- linked_at

The immutable id is what identifies the account; logins get renamed and must not silently detach an
account.

## Problem

Owned by the **Problems** module. Carries only what stays stable across versions.

Fields:

- id
- slug — stable identifier used in catalog URLs, unique
- title
- difficulty — `Easy`, `Medium`, `Hard`
- description — Markdown
- tags
- created_at

## ProblemVersion

Owned by the **Problems** module. One immutable revision of a problem.

Fields:

- id
- problem_id
- version — starts at 1, unique per problem
- snapshot_reference — object storage key of the problem bundle
- validator_config — validator pipeline configuration; the canonical JSON projection of a problem
  package's `validators` list, defined in
  [PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md#validator_config)
- created_at
- published_at — null while the version is a draft

A workspace is created from a version, never from a problem, so editing a problem never alters an
in-flight attempt. The catalog only ever resolves published versions.

## Workspace

Owned by the **Workspaces** module. A user's working copy of a problem version.

Fields:

- id
- user_id
- problem_version_id
- snapshot_reference — object storage key of the current working tree
- created_at
- updated_at — last write, and never earlier than created_at

## Submission

Owned by the **Submissions** module. One evaluation attempt against a workspace.

Fields:

- id
- workspace_id
- user_id — denormalised from the workspace so attempt history is a single-table query
- status
- score — null until the pipeline completes, otherwise 0–100
- created_at
- completed_at — set exactly when status becomes terminal

Status values:

- `Queued` — accepted, waiting for a worker
- `Running` — a worker is executing the validator pipeline
- `Completed` — the pipeline ran to completion; the verdict is the score and the report
- `Failed` — the pipeline could not run to completion; infrastructure, not a wrong answer

`Completed` and `Failed` are terminal.

## SubmissionReport

Owned by the **Submissions** module. One report per submission.

Fields:

- id
- submission_id — unique
- validator_results — per-validator outcomes; shape follows the validator plugin interface from
  [#18](https://github.com/shoraLBRT/ritocode/issues/18)
- logs_reference — object storage key of the captured runner logs, null when nothing was captured
- created_at
