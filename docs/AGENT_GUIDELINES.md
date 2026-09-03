# Agent Contribution Guidelines

This file defines expectations for AI agents modifying the repository.

## Priorities

Agents should prioritize:

- simplicity
- determinism
- reproducibility
- testability

## Code Rules

Prefer:

- clear naming
- small functions
- explicit data models

Avoid:

- hidden side effects
- complex frameworks without clear benefit
- premature abstraction

## When Adding Features

Agents must:

1. update domain model documentation
2. add tests where appropriate
3. keep architecture boundaries intact

## Tests That Touch the Database

Any test needing a real PostgreSQL takes it from `tests/Ritocode.TestSupport`: reference the
project, declare `[assembly: AssemblyFixture(typeof(PostgresTestServer))]` once in the test
assembly, and ask for a database with `CreateDatabaseAsync`. Every caller gets its own database,
already migrated.

Do not build a fixture of your own — an in-memory provider, a shared database, a hand-rolled
container. ADR 0005 lists ad-hoc fixtures among the shortcuts that are forbidden, because the
first module that needs isolation makes every test written on one of them get rewritten.

## Safe Execution

User-submitted code must always execute inside sandbox runners.
Never execute arbitrary code inside API services.