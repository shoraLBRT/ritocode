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

## Safe Execution

User-submitted code must always execute inside sandbox runners.
Never execute arbitrary code inside API services.