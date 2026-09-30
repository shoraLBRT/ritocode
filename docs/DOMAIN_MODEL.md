# Domain Model

The entities that exist in the code, and which module owns each. The physical schema, indexes and
constraints are in [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md); this document is the conceptual view.
The entities the new product is building toward — problem cards, materials, tasks, attempts,
signals — are described in [SPEC.md](SPEC.md) §3 and §5, and join this file as they are built.

Every entity is owned by exactly one module. An entity is only ever read or written through its
owning module — see [ADR 0002](adr/0002-modular-monolith-layout.md).

## User

Owned by the **Users** module. A platform account.

Fields:

- id
- email — stored lower-cased, unique
- username — stored lower-cased, unique
- created_at

## LinkedAccount

Owned by the **Auth** module. Links a Ritocode account to an external identity.

Fields:

- id
- user_id — the Ritocode user
- provider — `GitHub` today; Google arrives with sign-in
  ([#7](https://github.com/shoraLBRT/ritocode/issues/7))
- provider_user_id — the provider's immutable identifier, unique per provider
- provider_login — last known login at the provider, for display only, may be stale
- linked_at

The immutable id is what identifies the account; logins get renamed and must not silently detach an
account.

## Problem and ProblemVersion — previous product

Owned by the **Problems** module, and **replaced** by the Content module of
[#121](https://github.com/shoraLBRT/ritocode/issues/121). They describe a refactoring exercise
loaded from the old package format ([PROBLEM_PACKAGE_SPEC.md](PROBLEM_PACKAGE_SPEC.md)) into a
bundle in object storage ([STORAGE_LAYOUT.md](STORAGE_LAYOUT.md)); all three go together.

- **Problem:** id, slug (unique), title, difficulty (`Easy`, `Medium`, `Hard`), description in
  Markdown, tags, created_at.
- **ProblemVersion:** one immutable revision of a problem — id, problem_id, version (from 1, unique
  per problem), snapshot_reference to the bundle, validator_config, workspace_root, editable_files,
  the three size limits, created_at, published_at (null while a draft). The catalog resolves only
  the highest **published** version.

The columns that served workspaces — `workspace_root`, `editable_files` and the limits — have had no
reader since [#119](https://github.com/shoraLBRT/ritocode/issues/119) removed the Workspaces module.
