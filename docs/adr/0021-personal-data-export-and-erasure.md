# 0021 — A user's data is exported and erased by each module, through one seam

- Status: Proposed
- Date: 2026-11-22
- Relates to: [#299](https://github.com/shoraLBRT/ritocode/issues/299), [SPEC.md](../SPEC.md) §9.3, §10.4
- Builds on: [0002](0002-modular-monolith-layout.md), [0004](0004-persistence-and-migrations.md),
  [0007](0007-cross-module-contract-form.md) — supersedes its §4 for this one need, as §4 says the
  first write need would

## Context

Under 152-FZ a person may withdraw consent and have their data deleted, and may ask what is kept.
#299 builds `GET /api/v1/me/export` and `DELETE /api/v1/me`. A user's rows live in three schemas —
`users` (the account), `auth` (linked accounts, sessions) and `attempts` (attempts, signals,
reveals, study marks) — and no module may open another's `DbContext` (ADR 0002). Someone has to ask
every module for its part.

ADR 0007 gives the form of a cross-module need: one interface per question, answered by the one module
its namespace names, registered once (`CrossModuleContractTests` checks it), and read-only (§4). Both
halves of this need break that form on purpose:

- the question is asked of **every** module that keeps a user's rows, not of one; a contract registered
  once by one owner cannot express "each of you, for your own rows";
- erasing is a write into another module's schema — the case §4 said would supersede it rather than be
  squeezed into a lookup.

## Decision

### 1. One seam, `IPersonalData`, implemented by every module that keeps a user's rows

`Ritocode.Shared.PersonalData.IPersonalData` has a module name, whether it holds the account itself,
`ExportAsync(userId)` and `EraseAsync(userId)`. Each module that keeps a user's rows registers one
implementation over its own schema; the caller resolves `IEnumerable<IPersonalData>` and asks them all.
It lives outside `Ritocode.Shared.Contracts` because it is not a contract in ADR 0007's sense — it is
a seam every module implements, as `IModule` is — and the contract rules (one registration, one owner)
would be wrong for it.

The coupling stays visible: a reader of a module sees the registration and the class, and the caller
names no module. Nothing in a module learns that another exists.

### 2. Each module reaches only its own rows, with the owner in every query

An implementation erases with `ExecuteDelete` over queries that put the user in the `WHERE`; in the
Attempts module through the owner-scoped lookups the ownership rule already requires
(`OwnedAttempts` and its siblings), so no new allowance was needed.

### 3. The account is erased last; erasing is idempotent

There is no transaction across the three schemas' contexts (ADR 0004: cross-module consistency is
eventual). Instead the Users module's part — the one with `HoldsTheAccount` — is erased after every
other, and erasing what is gone erases nothing. A deletion that fails part-way leaves an account that
can be deleted again, which finishes it. Sessions go first inside Auth's part, so the moment they are
gone no browser is signed in as the user.

### 4. The endpoints are Auth's

`GET /me/export` and `DELETE /me` sit beside `GET /me` and sign-out in the Auth module, which owns the
caller and clears the session's cookies. Deleting needs the username typed in the body — the server
checks it, not only the page — and, being state-changing, the CSRF token with a session. The export
names each module's part by its name and holds no token: sessions by their dates only. An attempt's
result and review carry the answer key, so they are in the export only for a task whose answer the
learner revealed (ADR 0017).

### 5. Checked mechanically

- `PersonalDataTests` (architecture): every module whose context maps an entity with a `Guid UserId`,
  and the Users module, registers exactly one `IPersonalData` of its own assembly; no other module
  does; exactly one, the Users module's, holds the account.
- `AccountEndpointTests` (API): a test walks every `uuid` column of every module schema from the
  database catalogue, so a table added later is swept without editing the test, and asserts the seed
  reached every table with a `user_id`.

## Consequences

- A module that adds a table naming a user adds its rows to its `IPersonalData`, or both tests fail.
- The admin area's lists and usage statistics read the same rows, so a deleted user is gone from
  them with no code of their own.
- The backups still hold the account for up to seven days; RUNBOOK › Personal data says how that
  promise is kept, and what a restore older than a deletion owes.
- Considered and not chosen: a domain event per module (no event infrastructure exists, and an event
  nobody handles fails silently where a missing registration fails a test); a single SQL script over
  all schemas (the coupling ADR 0002 removes, in a string no test reads).
