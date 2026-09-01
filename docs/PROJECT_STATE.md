# Project State

**This file is the entry point for every new session.** It answers three questions: what exists,
what to build next, and how to verify it. Read it before touching anything; update it before
finishing.

- **Last updated:** 2026-09-01
- **Current phase:** Phase 1 (MVP) — see `docs/MVP_SCOPE.md`
- **Backlog board:** <https://github.com/users/shoraLBRT/projects/3>
- **Source of truth for task status:** GitHub issues. This file summarises; the board decides.

---

## Session workflow

Follow this loop. It is what makes the project continue without the maintainer re-explaining it.

1. **Orient.** Read `AGENTS.md`, this file, and `docs/adr/` (at least the index).
2. **Pick work.** Take the top unblocked item from [Next up](#next-up). Confirm it is still open:

   ```bash
   gh issue list --repo shoraLBRT/ritocode --state open --label phase:1 --limit 60
   ```

3. **Branch.** One issue per branch: `git checkout -b feat/<short-slug>` off `main`.
4. **Build it.** Follow `docs/AGENT_GUIDELINES.md` and the ADRs. Tests come with the code, not after.
5. **Verify.** Everything under [Verification](#verification) must pass. No exceptions, no
   "will fix in CI".
6. **Ship.** Commit, push, open a PR that names the issue (`Closes #N`). Comment on the issue with
   what landed and what was deliberately left out.
7. **Record.** Move the issue out of [Next up](#next-up) into [What exists](#what-exists), refresh
   *Last updated*, and add anything a future session would otherwise have to rediscover to
   [Open questions](#open-questions).

Rules that are easy to get wrong:

- **Never commit to `main`.** The maintainer merges PRs.
- **Never mark an issue done with failing or skipped tests.** Partial work ships as a PR that says
  plainly what is missing, with the issue left open.
- **A decision that outlives the session goes in an ADR**, not in a commit message.
- **Stay in phase.** Phase 2 and 3 issues exist but are not open work until Phase 1 closes.

---

## Stack

Decided in [ADR 0001](adr/0001-technology-stack.md).

| Layer | Choice |
| --- | --- |
| Backend | ASP.NET Core, .NET 10, minimal APIs, C# |
| Modules | One project per module, boundaries enforced by tests ([ADR 0002](adr/0002-modular-monolith-layout.md)) |
| API contract | RFC 9457 errors, offset pagination, FluentValidation ([ADR 0003](adr/0003-api-conventions.md)) |
| Tests | xUnit v3, `Microsoft.AspNetCore.TestHost` |
| Database | PostgreSQL (**not wired up yet**) |
| Frontend | React + Vite + TypeScript (**not started**) |

Package versions live in `Directory.Packages.props`. The SDK is pinned in `global.json`.
Warnings are errors, vulnerability warnings included — a red build on a newly disclosed CVE is
expected behaviour, fixed by pinning the package forward.

---

## Repository map

```
Ritocode.slnx
Directory.Build.props         shared MSBuild settings (net10.0, nullable, warnings-as-errors)
Directory.Packages.props      central package versions
.editorconfig                 style, plus analyzer rules deliberately disabled, each with a reason
src/
  Ritocode.Api/               composition root: pipeline, config, health, meta, module wiring
  Ritocode.Shared/            errors, Result<T>, paging, IModule, request correlation, error body
  Modules/Ritocode.Modules.*  one project per module; all still endpoint-free stubs
tests/
  Ritocode.Shared.Tests/        unit tests for the shared primitives
  Ritocode.Api.Tests/           in-memory host tests over the real composition root
  Ritocode.Architecture.Tests/  module boundary rules, executable
docs/
  adr/                        architecture decision records
  PROJECT_STATE.md            this file
```

---

## What exists

| Issue | State | What landed | Where |
| --- | --- | --- | --- |
| [#1](https://github.com/shoraLBRT/ritocode/issues/1) Backend skeleton | Done | Solution, 7 module projects with enforced boundaries, options-based config validated at startup, request-id middleware, unified error handler, `/health/live` + `/health/ready`, `/api/v1/meta/modules` | `src/`, `tests/Ritocode.Architecture.Tests` |
| [#2](https://github.com/shoraLBRT/ritocode/issues/2) API conventions | Done | [ADR 0003](adr/0003-api-conventions.md), `ErrorStatusCodeMap`, `ApiProblem`, `PageRequest`/`Page<T>`, `ValidationEndpointFilter<T>` | `src/Ritocode.Shared`, `docs/adr/0003-api-conventions.md` |

Nothing else from the backlog is implemented. Every module is a registered stub with no endpoints
and no services — the boundary is in place, the behaviour is not.

### Deliberately deferred

- **CI ([#31](https://github.com/shoraLBRT/ritocode/issues/31))** covers the backend only; the
  frontend job lands with the frontend, so the issue stays open.
- **No database.** `Api:BasePath` and CORS are the only real configuration. There is no connection
  string, no EF Core, no migrations. Readiness reports healthy because it has nothing to check —
  the first dependency added must register a health check tagged `HealthEndpoints.ReadyTag`.
- **No authentication.** Endpoints are anonymous. `AllowAnonymous()` on health and meta is
  deliberate so they keep working once authentication is switched on in
  [#6](https://github.com/shoraLBRT/ritocode/issues/6).

---

## Next up

Ordered. Items lower in the list depend on items above them.

1. **[#3](https://github.com/shoraLBRT/ritocode/issues/3) — PostgreSQL schema for core entities.**
   Model `users`, `linked_accounts`, `problems`, `problem_versions`, `workspaces`, `submissions`,
   `submission_reports` from `docs/DOMAIN_MODEL.md`. Decide EF Core vs. Dapper and raw SQL in an ADR
   before writing the first migration — it is the next stack-level fork.
2. **[#4](https://github.com/shoraLBRT/ritocode/issues/4) — Migration workflow and bootstrap.**
   Migrations must run from an empty database, and CI must detect schema drift. Blocked on #3.
3. **[#32](https://github.com/shoraLBRT/ritocode/issues/32) — One-command local environment.**
   `docker compose` with PostgreSQL and object storage. Do this alongside #3 and #4; without it every
   later session re-invents how to get a database.
4. **[#8](https://github.com/shoraLBRT/ritocode/issues/8) — Problem package manifest format.**
   Pure specification work with no database dependency, so it can proceed in parallel with #3.
5. **[#9](https://github.com/shoraLBRT/ritocode/issues/9) — Problem catalog service.** The first
   module with real endpoints, and the first consumer of the pagination contract. Blocked on #3, #8.
6. **[#37](https://github.com/shoraLBRT/ritocode/issues/37) — Backend integration test harness.**
   Testcontainers-backed PostgreSQL. Pull this forward if #3 lands and tests start needing a real
   database.

After that the MVP path runs #10 → #14 → #17: workspaces, then submissions, then evaluation.

---

## Open questions

Decisions a future session will hit. None block the items in [Next up](#next-up) except where noted.

- **EF Core or Dapper?** Blocks #3. Weigh migration tooling and the schema-drift check required by
  #4 against the explicit SQL that `docs/AGENT_GUIDELINES.md` favours. Whichever wins, write the ADR.
- **Session tokens: JWT or opaque plus a server-side store?** Needed for #6. Opaque tokens make
  revocation trivial, which matters once submissions can open real pull requests in Phase 3.
- **Object storage in local development.** `docs/ARCHITECTURE.md` assumes object storage for bundles
  and artifacts. MinIO in `docker compose` is the obvious answer for #32, but nothing is committed yet.
- **Sandbox runner host.** Docker-in-Docker, a dedicated runner VM, or a warm pool — undecided, and
  needed by #21. Not urgent, but it constrains how #17 is designed, so decide before building the
  orchestrator.
- **`net10.0` package pinning.** Framework-tied packages sit at `10.0.9` to match the development
  machine's runtime. When CI or a deployment target moves ahead, raise them together.

---

## Verification

Run from the repository root. All of these must be clean before opening a PR.

```bash
dotnet build Ritocode.slnx --warnaserror
```

```bash
dotnet test Ritocode.slnx
```

```bash
dotnet run --project src/Ritocode.Api --no-launch-profile --urls http://127.0.0.1:5199
```

With the host running, these are the current smoke checks:

| Request | Expected |
| --- | --- |
| `GET /health/live` | `200`, `{"status":"Healthy","checks":[]}` |
| `GET /health/ready` | `200`, `"status":"Healthy"` |
| `GET /api/v1/meta/modules` | `200`, all seven modules listed |
| any response | carries an `X-Request-Id` header |

Current baseline: **54 tests, all passing** — 33 shared, 16 API, 5 architecture.
A session that leaves this number lower than it found it has broken something.
