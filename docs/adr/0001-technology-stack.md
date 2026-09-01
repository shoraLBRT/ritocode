# 0001 — Technology stack

- Status: Accepted
- Date: 2026-09-01
- Relates to: [#1](https://github.com/shoraLBRT/ritocode/issues/1)

## Context

The repository carried architecture and domain documentation but no code and no recorded stack
decision. The only signals were a .NET `.gitignore` and a locally installed .NET 10 SDK. Every
Phase 1 issue depends on this choice, so it had to be settled before the first line of code.

Constraints that shaped it, from `docs/ARCHITECTURE.md` and `docs/AGENT_GUIDELINES.md`:

- modular monolith first, microservices only when a boundary proves itself
- deterministic, reproducible evaluation
- user code executes only inside sandbox runners, never in an API process
- PostgreSQL as the primary store, object storage for bundles and artifacts

## Decision

| Layer | Choice |
| --- | --- |
| Backend | ASP.NET Core on .NET 10, C#, minimal APIs |
| Backend tests | xUnit v3, `Microsoft.AspNetCore.TestHost` for in-memory host tests |
| Validation | FluentValidation, applied through an endpoint filter |
| Database | PostgreSQL |
| Frontend | React + Vite + TypeScript |
| Sandbox runners | Containers, orchestrated from the evaluation worker |

Package versions are managed centrally in `Directory.Packages.props`; the SDK is pinned in
`global.json`. Compiler warnings are errors solution-wide, including NuGet vulnerability warnings.

## Alternatives considered

**Go for the backend.** Cheaper container footprint and a natural fit for runner orchestration.
Rejected: it contradicts the `.gitignore` already committed, and the runner is a separate process
that can be written in whatever language suits it later without dictating the API stack.

**TypeScript / NestJS.** One language across the whole product. Rejected: the evaluation
orchestrator is CPU- and process-heavy, and the .NET signal in the repository was explicit.

**Next.js for the frontend.** SSR would help catalog and profile pages. Rejected for now: the
backend is a pure JSON API, the workspace editor is heavily client-side, and an SSR layer adds a
deployment surface with no Phase 1 payoff. Revisit if catalog SEO becomes a goal.

## Consequences

- The API host targets `net10.0` and requires the ASP.NET Core 10 runtime. Packages tied to the
  shared framework are pinned to `10.0.9`, the runtime available on the development machine;
  raising them requires raising the runtime everywhere, CI included.
- `TreatWarningsAsErrors` means a newly disclosed vulnerability in a transitive package breaks the
  build. That is intentional — it is the earliest possible signal — but it makes a red build a
  routine event that a session must be prepared to fix by pinning forward.
- Analyzer rules deliberately turned off are listed with reasons in `.editorconfig`. Adding a
  suppression requires a reason in that file.
