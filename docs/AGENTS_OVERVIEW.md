# Ritocode – Agent Overview

This document is intended for AI agents and automated contributors working in this repository.
It provides a fast overview of the project's purpose, constraints, and development priorities.

## Project Purpose

Ritocode is a platform for practicing code review, code quality, and software design using real-world code.

Instead of algorithmic puzzles, users work on tasks that involve:

- refactoring bad code
- fixing performance issues
- improving tests
- removing code smells
- enforcing architecture and design constraints

The platform evaluates solutions automatically using deterministic validators.

## Key Principle

Ritocode trains engineers to evaluate and improve existing code, not just write new code.

This becomes increasingly important as developers work with:

- generated code
- rapidly produced code
- large existing codebases

## Platform Flow

1. User selects a problem
2. A workspace is created from a problem snapshot
3. User modifies code
4. User submits a solution
5. Evaluation pipeline runs validators
6. Results and feedback are returned

Later phases may allow users to submit fixes to real repositories.

## Engineering Philosophy

Agents contributing to this repository should prioritize:

- deterministic behavior
- reproducible environments
- clear domain boundaries
- observable systems
- safe execution of user code

Avoid introducing unnecessary complexity or premature microservices.