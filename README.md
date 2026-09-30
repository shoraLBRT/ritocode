# Ritocode

**A catalogue of what breaks in code written by AI, and a trainer that teaches people to recognise
it.**

More and more code is not written but accepted: an agent produces a change in two minutes, a person
approves it in three, and nobody tells them whether they were right. Ritocode trains the judgement
that decides the outcome. A learner reads a small project an agent wrote, with the context it was
written for and the brief the agent was given, names the problems they see, and chooses what each is
treated with — a better brief, a rule in the repository, an automatic check, a fix by hand, or
nothing, because in this context it is fine.

The same code in a different context has different right answers. That is the mechanic the product
is built on.

- **What it is and why:** [docs/CONCEPT.md](docs/CONCEPT.md)
- **What gets built:** [docs/SPEC.md](docs/SPEC.md)
- **In what order:** [docs/ROADMAP.md](docs/ROADMAP.md), tracked on the
  [project board](https://github.com/users/shoraLBRT/projects/3)
- **Where the project stands:** [docs/PROJECT_STATE.md](docs/PROJECT_STATE.md)

The product was redefined on 2026-09-30 ([ADR 0010](docs/adr/0010-diagnosis-of-ai-written-code.md)).
The platform built for the previous product — a refactoring trainer graded by running the learner's
code — is being reshaped for this one; what it was is kept at the tag `pre-diagnosis`.

## Stack

ASP.NET Core on .NET 10 as a modular monolith, PostgreSQL, and React with Vite and TypeScript
([ADR 0001](docs/adr/0001-technology-stack.md)).

## Contributing

The repository is built largely by AI agents working from the roadmap. If you are one, start with
[AGENTS.md](AGENTS.md).

## License

Code: MIT. Content under `content/` is licensed separately — see
[docs/CONTENT_FORMAT.md](docs/CONTENT_FORMAT.md) §9.
