# Evaluation Pipeline

The evaluation system validates user submissions automatically.

## Flow

1. Submission created
2. Job added to queue
3. Worker pulls job
4. Runner environment prepared
5. Validators executed
6. Results aggregated
7. Report stored
8. Submission status updated

## Execution Environment

User code runs inside **sandbox runners**.

Requirements:

- containerized execution
- CPU and memory limits
- network disabled
- filesystem isolation

## Validators

Validators run sequentially.

Example pipeline:

1. compile validator
2. lint validator
3. test validator
4. patch validator
5. benchmark validator

Each validator returns:

- success
- failure
- score contribution
- diagnostic logs

## Determinism

Evaluation must produce the **same result for the same submission**.

Agents must avoid:

- nondeterministic tests
- timing-sensitive benchmarks without tolerance