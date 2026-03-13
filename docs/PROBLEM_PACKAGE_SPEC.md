# Problem Package Specification

This document defines the structure of a Ritocode problem.

Problems are distributed as **problem packages**.

## Package Structure

Example:

problem/
  problem.yaml
  starter/
  tests/
  validators/

## problem.yaml

Fields:

title
difficulty
tags
description
language
validator_config

Example:

title: Remove code smell
difficulty: medium
tags:
  - refactoring
  - code-quality

## Starter Code

The `starter/` directory contains the code the user begins with.

## Tests

The `tests/` directory contains automated tests used during evaluation.

## Validators

Validators determine if the submission is correct.

Typical validators:

- compile validator
- unit test validator
- lint validator
- patch scope validator
- performance benchmark validator

## Constraints

Problem packages must be:

- deterministic
- reproducible
- self-contained