# System Architecture

A modular monolith: one API host, one PostgreSQL database, one schema per module, and a React
frontend. The target shape is fixed in [SPEC.md](SPEC.md) §9; this file maps it to the code and says
how far along each part is.

Nothing here executes code a learner writes. Diagnosis is reading and choosing, so there are no
sandbox runners, no evaluation workers and no queue.

## Modules

Each module is one project under `src/Modules/`, owns one database schema, and never references
another module ([ADR 0002](adr/0002-modular-monolith-layout.md)). A module asks another a question
only through a contract in `src/Ritocode.Shared/Contracts`
([ADR 0007](adr/0007-cross-module-contract-form.md)). Both rules are enforced by
`tests/Ritocode.Architecture.Tests`.

| Module | Owns | State |
| --- | --- | --- |
| **Auth** | Sign-in with GitHub and Google, sessions, linked accounts | The identity seam and the development identity exist ([ADR 0008](adr/0008-authentication-seam.md)); real sign-in is [#6](https://github.com/shoraLBRT/ritocode/issues/6) and [#7](https://github.com/shoraLBRT/ritocode/issues/7) |
| **Users** | Users; who is an admin comes from configuration | Exists; answers `IUserLookup` |
| **Content** | Problem cards, the treatment tree, materials, tasks and answer keys; ingest from `content/` and validation; the catalogue reads | The format, validation, the `content` schema and ingest exist ([#120](https://github.com/shoraLBRT/ritocode/issues/120), [#121](https://github.com/shoraLBRT/ritocode/issues/121)); the read APIs are [#9](https://github.com/shoraLBRT/ritocode/issues/9) |
| **Attempts** | Attempts, scoring, progress, signals | Not built: [#20](https://github.com/shoraLBRT/ritocode/issues/20), [#125](https://github.com/shoraLBRT/ritocode/issues/125) |

The host, `src/Ritocode.Api`, is the composition root: the only project that references every
module, listed once in `Setup/ModuleRegistry.cs`. `src/Ritocode.DbMigrator` applies every module's
migrations; the host never migrates itself. `src/Ritocode.ContentTool` is the content command line —
`validate` checks a content tree against [CONTENT_FORMAT.md](CONTENT_FORMAT.md) with no database.

## Shared infrastructure

`src/Ritocode.Shared` holds what every module uses and none owns: the unified error body and
`Result<T>`, paging, request correlation, the persistence base, the identity seam (`ICurrentUser`),
and the cross-module contracts.

## Storage

PostgreSQL only, one schema per module ([DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)). Content is small
text and is stored in the database; there is no object storage (it left with #121).

## Frontend

`frontend/`: React, Vite and TypeScript. `src/api` is the only code that knows the backend exists;
the rest is the shell around it. The screens of SPEC §4 are stage S3 onward.

## Deployment

One VPS in Russia running Docker Compose: a reverse proxy with TLS, the API, the static frontend,
PostgreSQL and Umami (SPEC §9.5). Stage S7.
