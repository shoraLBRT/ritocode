# System Architecture

The system follows a modular monolith architecture in early phases.

Major subsystems:

- API Service
- Evaluation Workers
- Sandbox Runners
- Problem Catalog
- Workspace System

## Core Components

### API

Responsibilities:

- authentication
- problem catalog
- workspace management
- submission lifecycle
- user progress

### Evaluation Workers

Responsible for:

- pulling submission jobs
- executing validators
- aggregating results

### Sandbox Runner

Responsible for safe execution of untrusted code:

- isolated containers
- resource limits
- disabled networking
- artifact capture

### Storage

Main data stores:

PostgreSQL:
- users
- problems
- submissions
- progress

Object storage:
- problem bundles
- workspace snapshots
- evaluation artifacts

Redis (optional):
- queues
- short-lived caching