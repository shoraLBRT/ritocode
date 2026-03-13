# AGENTS.md

This file helps AI agents quickly understand how to work in the Ritocode repository.

Agents should read the following documents before modifying the codebase:

- docs/AGENTS_OVERVIEW.md
- docs/ARCHITECTURE.md
- docs/DOMAIN_MODEL.md
- docs/MVP_SCOPE.md
- docs/SCALING_PLAN.md
- docs/AGENT_GUIDELINES.md
- docs/PROBLEM_PACKAGE_SPEC.md
- docs/EVALUATION_PIPELINE.md

## Project Goal

Ritocode is a platform for practicing:

- code review
- code quality improvements
- refactoring
- performance optimization
- test quality improvements

Users solve tasks by **improving existing codebases** rather than writing algorithms from scratch.

## Agent Responsibilities

When contributing code agents must:

1. Follow the architecture described in `ARCHITECTURE.md`
2. Respect domain entities defined in `DOMAIN_MODEL.md`
3. Avoid introducing microservices prematurely
4. Ensure user code executes **only inside sandbox runners**
5. Keep evaluation deterministic

## Development Rules

Prefer:

- modular monolith structure
- explicit domain models
- deterministic validation logic
- clear APIs

Avoid:

- hidden side effects
- dynamic runtime magic
- complex frameworks without strong justification

## MVP Priority

Focus on completing **MVP_SCOPE.md** before implementing advanced features.