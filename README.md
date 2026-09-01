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

- [x] backend service skeleton
- [ ] core database schema
- [ ] problem package format
- [ ] problem catalog API
- [ ] workspace editor API
- [ ] submission lifecycle
- [ ] evaluation pipeline
- [ ] validator plugin system
- [ ] sandbox runner infrastructure
- [ ] initial problem set
- [ ] basic frontend UI
- [ ] CI/CD pipeline
- [ ] observability and logging

---

# Contributing

The repository is built largely by AI agents working from the backlog. If you are one, start with
[AGENTS.md](AGENTS.md) and [docs/PROJECT_STATE.md](docs/PROJECT_STATE.md).

# License

MIT
