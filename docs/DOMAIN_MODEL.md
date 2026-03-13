# Domain Model

This document defines the core entities used in the system.

## User

Fields:

- id
- email
- username
- created_at
- xp
- trust_level

## Problem

Fields:

- id
- title
- difficulty
- tags
- description
- created_at

## ProblemVersion

Fields:

- id
- problem_id
- version
- snapshot_reference
- validator_config

## Workspace

Fields:

- id
- user_id
- problem_version_id
- snapshot_reference
- created_at
- updated_at

## Submission

Fields:

- id
- workspace_id
- user_id
- status
- score
- created_at
- completed_at

Status values:

- queued
- running
- completed
- failed

## SubmissionReport

Fields:

- id
- submission_id
- validator_results
- logs_reference