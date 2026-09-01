# 0004 — Persistence and migrations

- Status: Accepted
- Date: 2026-09-01
- Relates to: [#3](https://github.com/shoraLBRT/ritocode/issues/3), [#4](https://github.com/shoraLBRT/ritocode/issues/4)
- Builds on: [0002](0002-modular-monolith-layout.md)

## Context

Issue #3 needs a schema for the entities in `docs/DOMAIN_MODEL.md`. Issue #4 needs migrations that
run from an empty database and a CI check that detects schema drift. [ADR 0002](0002-modular-monolith-layout.md)
already committed to modules owning their own persistence, which leaves three questions open: what
data access technology, how the module boundary appears in the database, and who applies migrations.

`docs/AGENT_GUIDELINES.md` asks for explicit data models and warns against frameworks adopted
without a clear benefit, which cuts against a heavyweight ORM. #4's schema-drift requirement cuts
the other way.

## Decision

### EF Core with Npgsql, configured explicitly

EF Core 10 with the Npgsql provider. The benefit that decides it is #4: `dotnet ef migrations
has-pending-model-changes` compares the model against the last migration and fails when they
diverge. That is the schema-drift check the issue asks for, already built and already tested.

To keep "explicit data models" true in practice:

- Every entity has an `IEntityTypeConfiguration<T>` that names its table, columns, keys and indexes.
  Nothing relies on convention to decide what the database looks like.
- No lazy loading. No `DbSet` exposed outside its owning module.
- Reads that do not feed an update use `AsNoTracking`.
- `snake_case` naming for tables and columns, via `EFCore.NamingConventions`, because the schema is
  read and queried by humans in `psql` as much as by the application.

### One DbContext and one schema per module

Each module owns a PostgreSQL schema and a `DbContext` restricted to it:

| Module | Schema | Tables |
| --- | --- | --- |
| Users | `users` | `users` |
| Auth | `auth` | `linked_accounts` |
| Problems | `problems` | `problems`, `problem_versions` |
| Workspaces | `workspaces` | `workspaces` |
| Submissions | `submissions` | `submissions`, `submission_reports` |

Each context keeps its own migration history table inside its own schema, so a module's migrations
are independent of every other module's.

A module's context maps only its own tables. A module physically cannot query another module's data
through EF, which is the database-level form of the rule the architecture tests enforce in code.

### Cross-module references carry no foreign key

`workspaces.user_id` points at a row in `users.users`, but there is no `FOREIGN KEY` constraint
across schemas. Within a module — `problem_versions` to `problems`, `submission_reports` to
`submissions` — foreign keys are declared normally.

This is the real cost of the boundary, and it is accepted rather than hidden:

- The owning module validates the reference on write. A workspace is created through the
  Workspaces module, which asks the Users module whether the user exists.
- Orphan rows become possible if a delete races a write. Cleanup is
  [#43](https://github.com/shoraLBRT/ritocode/issues/43)'s job, and user deletion must go through a
  process that notifies each module rather than a single `DELETE`.
- Every cross-module id column is indexed, since it can no longer inherit an index from a foreign
  key constraint.

### The application never migrates itself

Migrations are applied by `Ritocode.DbMigrator`, a console tool. The API host does not migrate on
startup: several instances starting at once would race, and a failed migration would take down
serving instances rather than one job.

The tool applies every module's migrations in `ModuleRegistry` order and is idempotent, so it is
safe to run on every deploy and on every `scripts/dev-up`.

## Alternatives considered

**Dapper plus a migration tool (DbUp, FluentMigrator, Grate).** Closest to "explicit SQL", and the
schema would be hand-written rather than generated. Rejected because #4 requires drift detection: with
hand-written SQL there is no model to compare the schema against, so the check would have to be built
from scratch — schema dumps compared across runs — for the same guarantee EF Core provides directly.
Dapper remains available for read-heavy queries later; it composes with EF Core rather than replacing it.

**A single shared `RitocodeDbContext`.** Simpler, and it would let every foreign key be a real one.
Rejected because it makes ADR 0002's boundary false at the database layer: any module could join any
table, and the coupling would be invisible until an extraction attempt. Real foreign keys are worth
less than a boundary that holds.

**Auto-migrate on host startup.** Convenient in development, wrong everywhere else, and the habit is
hard to remove once tests depend on it.

## Consequences

- Adding an entity means adding an `IEntityTypeConfiguration`, generating a migration for that
  module's context, and committing it. CI fails if the model and the migrations disagree.
- Five contexts mean five `dotnet ef` invocations. `scripts/db-migrations-add.ps1` wraps that so the
  module name is the only argument.
- Cross-module joins are impossible in SQL. A screen needing data from two modules composes it in
  the API layer from two calls. If that becomes a performance problem, the answer is a read model
  owned by one module, not a cross-schema join.
- Splitting a module into its own service later means pointing its context at a different database.
  Nothing else has to move, because nothing else was ever allowed to touch its tables.
