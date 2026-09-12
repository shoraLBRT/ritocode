# Ritocode

**Ritocode** is a platform for practicing **code review, code quality, and software design** using real-world code.

Instead of algorithmic puzzles, tasks focus on improving existing codebases:

- fixing code smells
- improving performance
- refactoring poor abstractions
- strengthening test suites
- enforcing clean architecture and design principles

The goal is to help engineers become better at **reading, evaluating, and improving code**.

---

# Why this exists

Modern development increasingly involves working with **generated or rapidly produced code**.  
Being able to **critically evaluate code quality, detect hidden problems, and improve maintainability** is becoming a core engineering skill.

Ritocode focuses on training those skills by giving engineers real code and asking them to improve it.

---

# Planned Features

High-level platform capabilities:

- code quality and refactoring tasks
- performance optimization exercises
- test quality and reliability improvements
- automated validation using tests, linters and benchmarks
- isolated sandbox execution for submissions
- repository-based tasks derived from real projects
- optional contribution workflows to real repositories
- developer profiles showing solved problems and contributions

---

# Roadmap (near term)

Tracked on the [project board](https://github.com/users/shoraLBRT/projects/3).
Current implementation status, and what is being built next, lives in
[docs/PROJECT_STATE.md](docs/PROJECT_STATE.md).

A box is ticked only when that piece of work is finished. Several items have landed in part on
purpose — the platform is being built as one vertical slice first, decided in
[ADR 0005](docs/adr/0005-vertical-slice-before-breadth.md) — and those are left unticked with a note
saying what exists and what does not. [docs/SLICE_PLAN.md](docs/SLICE_PLAN.md) has the ordering.

- [x] backend service skeleton
- [x] core database schema
- [x] problem package format
- [ ] problem catalog API — **partial**: `GET /api/v1/problems` and `GET /api/v1/problems/{slug}`
      serve published versions, over the ingest that turns a validated package into a problem, a
      published version and a bundle in object storage. Search, facets, tag and difficulty filters
      and explicit version resolution are not built
- [ ] workspace editor API
- [ ] submission lifecycle
- [ ] evaluation pipeline
- [ ] validator plugin system
- [ ] sandbox runner infrastructure
- [ ] initial problem set — **partial**: three authored C# problems, one easy, one medium and one
      hard, each shipping a known-good and a known-bad answer as fixtures. How large the full Phase 1
      set should be is not decided yet, and a revision to a published problem has no way to reach the
      catalog
- [ ] basic frontend UI — **partial**: a React + Vite + TypeScript shell in `frontend/` with routing
      and the API client that owns the error envelope. The designed catalog, problem and workspace
      screens are not built, and the app has no notion of a signed-in user
- [ ] CI/CD pipeline — **partial**: the backend builds, tests and is checked for migration drift on
      every push, and the frontend lints, builds and tests on its own job. No job publishes an
      artifact, builds an image, tags a release or deploys anything
- [ ] observability and logging

---

# Contributing

The repository is built largely by AI agents working from the backlog. If you are one, start with
[AGENTS.md](AGENTS.md) and [docs/PROJECT_STATE.md](docs/PROJECT_STATE.md).

# License

MIT
