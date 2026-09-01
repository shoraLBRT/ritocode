# 0002 — Modular monolith layout

- Status: Accepted
- Date: 2026-09-01
- Relates to: [#1](https://github.com/shoraLBRT/ritocode/issues/1)

## Context

`docs/ARCHITECTURE.md` calls for a modular monolith and explicitly warns against premature
microservices. "Modular" needs a concrete meaning, otherwise the modules become folders that
reference each other freely and the boundary exists only in the documentation.

## Decision

One .NET project per module, under `src/Modules/`:

```
src/
  Ritocode.Api/                 composition root: pipeline, health, module wiring
  Ritocode.Shared/              cross-module primitives: errors, paging, module contract, HTTP glue
  Modules/
    Ritocode.Modules.Auth/
    Ritocode.Modules.Users/
    Ritocode.Modules.Problems/
    Ritocode.Modules.Workspaces/
    Ritocode.Modules.Submissions/
    Ritocode.Modules.Evaluations/
    Ritocode.Modules.Progress/
```

Reference rules, enforced by `tests/Ritocode.Architecture.Tests`:

1. A module references `Ritocode.Shared` and nothing else in the solution.
2. A module never references another module. Cross-module needs go through a contract published
   in `Ritocode.Shared`.
3. A module never references `Ritocode.Api`. Dependencies point inward, toward the composition root.
4. `Ritocode.Api` is the only project that references every module.

Each module exposes exactly one `IModule` implementation, which owns its service registrations and
its endpoint mapping. Modules are listed explicitly in `ModuleRegistry.All` — no assembly scanning,
so the composed set of the running system is readable in one file.

A module owns its own persistence. Sharing a physical PostgreSQL database is fine; sharing tables
across modules is not.

## Alternatives considered

**Folders inside a single project.** Less ceremony, and initially the same shape. Rejected: nothing
stops a `using` across the boundary, so the structure degrades silently. On a platform whose subject
is code quality, a boundary that cannot be violated by accident is worth seven `.csproj` files.

**Assembly scanning to discover modules.** Rejected by `docs/AGENT_GUIDELINES.md` ("avoid dynamic
runtime magic"), and it hides what a deployment is actually running.

## Consequences

- Adding a module is four steps: create the project, reference `Ritocode.Shared`, implement
  `IModule`, add it to `ModuleRegistry.All`. The architecture tests fail if the last step is skipped.
- The first genuine cross-module need will force a contract into `Ritocode.Shared`. That friction is
  the point: it makes coupling a visible, reviewed decision instead of a `using` statement.
- Extracting a module into its own service later means replacing its `Ritocode.Shared` contract with
  a network client — the boundary is already where the seam would be.
