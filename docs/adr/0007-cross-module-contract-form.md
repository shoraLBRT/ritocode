# 0007 — Cross-module contract form

- Status: Accepted
- Date: 2026-09-05
- Relates to: [`docs/DATABASE_SCHEMA.md`](../DATABASE_SCHEMA.md), [`docs/SLICE_PLAN.md`](../SLICE_PLAN.md), [#10](https://github.com/shoraLBRT/ritocode/issues/10)
- Builds on: [0002](0002-modular-monolith-layout.md), [0004](0004-persistence-and-migrations.md), [0005](0005-vertical-slice-before-breadth.md)

## Context

[ADR 0002](0002-modular-monolith-layout.md) says a module never references another module and that
cross-module needs "go through a contract published in `Ritocode.Shared`". That settles **where**.
It says nothing about **what shape** the contract takes, and the difference is not cosmetic: the
same rule is satisfied by a thin interface whose coupling is visible in a constructor signature and
by a mediator that dispatches by type and hides it. `ModuleBoundaryTests` cannot tell them apart —
neither adds an assembly reference.

[ADR 0004](0004-persistence-and-migrations.md) is what forces the question now. Five columns point
across a schema boundary with no `FOREIGN KEY`, because constraints across schemas would reinstate
the coupling the split removes. `DATABASE_SCHEMA.md` names all five and says which module validates
each:

| Column | Points at | Validated by |
| --- | --- | --- |
| `auth.linked_accounts.user_id` | `users.users.id` | Auth module on link |
| `workspaces.workspaces.user_id` | `users.users.id` | Workspaces module on create |
| `workspaces.workspaces.problem_version_id` | `problems.problem_versions.id` | Workspaces module on create |
| `submissions.submissions.workspace_id` | `workspaces.workspaces.id` | Submissions module on create |
| `submissions.submissions.user_id` | `users.users.id` | Submissions module on create |

"Validated by" has been an obligation with no mechanism since [#3](https://github.com/shoraLBRT/ritocode/issues/3).
[#10](https://github.com/shoraLBRT/ritocode/issues/10) in slice stage 3 is the first code that owes
it: Workspaces has to establish that a user and a problem version exist before it inserts a row, and
it may not open the Problems `DbContext` to find out — ADR 0005 lists that among the forbidden
reductions.

Unanswered, this gets decided by whoever writes #10, in a constructor, in one session, and the next
four references each get decided again.

## Decision

### 1. Thin read-interfaces, one per consumer need

A cross-module need is expressed as a C# interface in `Ritocode.Shared`, declared for the question
one consumer asks. The consumer takes it as a constructor parameter. That is the whole mechanism:
no mediator, no dispatch by type, no assembly scanning.

The property being bought is that **coupling appears in a signature**. A reader of
`WorkspaceCreationService`'s constructor sees that Workspaces depends on Problems and on Users, and
so does a reviewer of the diff that adds it. ADR 0002 says that friction is the point — it makes
coupling a reviewed decision instead of a `using` statement — and a mechanism that satisfies the
letter of the rule while removing the friction is not worth having.

An interface is named for **the question it answers**, not for the module that answers it:
`IProblemVersionLookup`, not `IProblemsService`. A name ending in `Service`, `Facade` or `Contract`
is the signal that it has stopped being one need.

Two consumers share an interface only when they ask the **identical** question. When a second
consumer needs a different subset, it gets its own interface rather than a method on the existing
one. Small interfaces are cheap; a growing one is a facade acquired by accretion.

### 2. Contracts answer facts, never policy

A contract returns what is true about a row. It does not answer a question phrased in the
consumer's policy.

For #10 that is the difference between:

```csharp
Task<ProblemVersionSummary?> FindAsync(Guid id, CancellationToken ct);   // yes
Task<bool> IsOpenableAsync(Guid id, CancellationToken ct);               // no
```

The second puts Workspaces' rule — *a workspace may only be opened on a published version* — inside
the Problems module, where the next change to that rule will not be looked for. The first hands over
`PublishedAt` and lets the consumer apply its own rule, which is where the rule belongs and where
its tests already are.

The same principle fixes the return shape. A contract returns **the data or its absence**, never an
`AppError` and never a `Result<T>`. `Result<T>` carries a stable `Code` that a client branches on
([ADR 0003](0003-api-conventions.md)); if Problems supplies it, Problems is choosing the error code
and message that a Workspaces endpoint will surface. Absence is `null`, and the consumer decides
whether that is a `NotFound` or a `Validation` failure and under which code.

So: `Task<TSummary?>` by default; `Task<bool>` only when the consumer genuinely needs nothing but the
answer. Every method is asynchronous and takes a `CancellationToken` — the implementation queries a
database, and a synchronous signature forecloses that permanently.

### 3. Contract types are their own DTOs

The types in a contract signature are declared in `Ritocode.Shared` beside the interface: flat
`record`s carrying only the fields the consumer needs.

```csharp
namespace Ritocode.Shared.Contracts.Problems;

public sealed record ProblemVersionSummary(
    Guid Id,
    Guid ProblemId,
    string Slug,
    int Version,
    DateTimeOffset? PublishedAt);
```

A module's domain entity is never used. Publishing it would put one module's internals in the one
assembly every module references, so every change to an entity becomes a change to every module's
compilation — the coupling ADR 0002 removes, reintroduced one level down.

### 4. Read-only, for the slice

No contract method mutates another module's state. Contracts answer questions.

This is a reduction in ADR 0005's sense and it is reversible: a write need gets its own ADR, and
whether the answer is a command interface or a domain event is a decision worth making against a
real case rather than in advance. One such case is already visible — `DATABASE_SCHEMA.md` notes that
user deletion "must notify each module rather than run a single `DELETE`", which is
[#43](https://github.com/shoraLBRT/ritocode/issues/43), outside the slice.

The rule has a mechanical form: **every contract method returns a `Task<T>` with a value.** A method
returning bare `Task` is a command, and there are none.

### 5. Where they live, and who implements them

```
src/Ritocode.Shared/Contracts/
  Problems/IProblemVersionLookup.cs, ProblemVersionSummary.cs
  Users/IUserLookup.cs, UserSummary.cs
```

Namespaced by the **owning** module — the one that answers — because there is exactly one
implementation and its owner is who a breaking change belongs to.

The owning module implements the interface and registers it in its own `IModule.RegisterServices`.
`Ritocode.Shared` declares contracts and implements none; a contract implemented in `Shared` would
be a module's logic living outside the module.

### 6. A contract call is a query, not a lock and not a transaction

A contract call is a separate database round trip against another module's schema, outside any
transaction the caller has open. Two consequences that would otherwise be found the hard way:

- **Check-then-write races.** The problem version validated a moment ago can be deleted before the
  insert lands. ADR 0004 already accepts this: there is no foreign key, orphan rows are possible,
  and cleanup is [#43](https://github.com/shoraLBRT/ritocode/issues/43). Validating is about
  rejecting the ordinary mistake, not about achieving referential integrity — the schema gave that
  up deliberately, for reasons stated in ADR 0004.
- **N+1.** A list endpoint calling a contract once per row is the obvious first shape and the wrong
  one. Where a consumer needs many, the contract takes many —
  `Task<IReadOnlyDictionary<Guid, TSummary>> FindManyAsync(IReadOnlyCollection<Guid> ids, ...)` — and
  the single-id method stays for the single-id case. `DATABASE_SCHEMA.md` already names the escape
  hatch beyond that: a read model owned by one module, never a cross-schema join.

### 7. Enforcement

The rules above become assertions in `tests/Ritocode.Architecture.Tests` in the same PR as the first
contract (slice stage 3), for the reason that file already states — documentation drifts, a failing
test does not:

1. Every public type under `Ritocode.Shared.Contracts` is either an interface or a `sealed record`.
   No classes with behaviour.
2. Every contract method returns a generic `Task<T>` and takes a `CancellationToken` as its last
   parameter. This is the executable form of rules 2 and 4.
3. Every contract interface is registered exactly once in the composed service collection, and its
   implementation lives in a `Ritocode.Modules.*` assembly.

Assertion 3 is the one that pays for itself. Routing calls through an interface moves the failure
for a missing registration from compile time to startup; the test moves it back to `dotnet test`,
which is where the rest of the boundary is already checked.

## Alternatives considered

**An in-process mediator.** Register request/handler pairs and dispatch by type. Fewer files, and it
satisfies ADR 0002's letter — no module references another. Rejected on the property that matters:
the coupling stops appearing in any signature. A module's dependencies can no longer be read off its
constructors, a reviewer cannot see a new cross-module edge in a diff, and the architecture test
cannot see one either, because every module is wired to the same dispatcher whether it uses it or
not. `docs/AGENT_GUIDELINES.md` warns against dynamic runtime magic and ADR 0002 rejected assembly
scanning for the same reason it would reject this: it hides what the composed system actually does.

**One facade per module — `IProblemsContract`.** Rejected: it grows without review. Every method
added is available to every consumer that already had the dependency, so the second and third
cross-module needs never get looked at, and each consumer depends on a surface far larger than it
uses. That is precisely the friction ADR 0002 wanted, deleted.

**Domain events instead of synchronous calls.** Modules publish facts, others react. Rejected for
this need rather than in general: #10 has to have the answer before it writes a row, and an
eventually-consistent answer to "does this problem version exist" is a workspace created on nothing.
Kept in reserve for the write-side case in §4, which is what events are actually good at.

**Publish the owning module's domain entities in `Ritocode.Shared` and let consumers read them.**
Zero DTO duplication. Rejected: it makes every module's compilation depend on every other module's
internal model, and it drags EF Core mapping concerns into the shared assembly. The duplication a
DTO costs is a few lines; the coupling it prevents is the reason the modules exist.

**Restore foreign keys across schemas and let PostgreSQL validate.** Correct by construction, and no
contract needed for four of the five references. Rejected by ADR 0004 already, and restated here
because it is the tempting answer: cross-schema constraints reinstate the coupling the split
removes, and they make extracting a module later a data migration rather than a code change.

**Defer the decision to #10.** Cheapest today. Rejected as the reason this ADR exists: the shape
would be chosen in a constructor by one session, the reasoning would not survive it, and the four
remaining references would each be decided again from scratch.

## Consequences

- Slice stage 3 builds two contracts — Workspaces asking Users and Problems — and the remaining
  three cross-schema references in `DATABASE_SCHEMA.md` follow the same shape when their modules
  arrive. The "Validated by" column in that table stops being an unbacked promise.
- Extracting a module into its own service replaces its implementation with a network client and
  changes nothing at the call site, which is what ADR 0002 predicted the boundary would buy.
- Consumers own their error vocabulary. A missing problem version surfaces under a code Workspaces
  chose, because Problems never got to choose one.
- The DI graph now spans modules, so a missing registration fails at startup rather than at compile
  time. Assertion 3 in §7 is what keeps that from being a regression.
- Cross-module consistency is eventual and races are accepted, per ADR 0004. Any code that reads a
  contract and then writes must be correct when the answer went stale in between.
- `Ritocode.Shared` grows a `Contracts/` area that every module can see. Keeping the interfaces one
  question wide is what stops it becoming a shared domain model by accretion; §7's assertions check
  the shape, and only review checks the granularity.
- No mechanism exists for one module to *change* another's state. The first real need for one —
  user deletion in [#43](https://github.com/shoraLBRT/ritocode/issues/43) — supersedes this ADR
  rather than editing it.
