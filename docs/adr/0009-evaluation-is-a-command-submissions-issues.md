# 0009 — Evaluation is a command the Submissions module issues

- Status: Accepted — the form was chosen by the maintainer on 2026-09-13, before stage 4's worker
- Date: 2026-09-13
- Relates to: [`docs/SLICE_PLAN.md`](../SLICE_PLAN.md), [#15](https://github.com/shoraLBRT/ritocode/issues/15), [#17](https://github.com/shoraLBRT/ritocode/issues/17), [#18](https://github.com/shoraLBRT/ritocode/issues/18), [#20](https://github.com/shoraLBRT/ritocode/issues/20)
- Builds on: [0002](0002-modular-monolith-layout.md), [0005](0005-vertical-slice-before-breadth.md), [0006](0006-sandbox-execution-model.md)
- Supersedes in part: [0007](0007-cross-module-contract-form.md) §4 — for the one kind of call named below, and nothing else

## Context

Stage 4 of the slice builds the queue and worker
([#15](https://github.com/shoraLBRT/ritocode/issues/15)) and then the orchestrator that runs the
validator chain ([#17](https://github.com/shoraLBRT/ritocode/issues/17)). The two halves of that work
are owned by two different modules, and the plan put them there on purpose:

- **Submissions** owns the state. `submissions` is the queue — the partial index
  `(status, created_at) WHERE status IN ('Queued','Running')` has been in its schema since #3 — and
  the lifecycle `Queued` → `Running` → `Completed` / `Failed` is methods on its entity since
  [#14](https://github.com/shoraLBRT/ritocode/issues/14). `submission_reports` is its table too.
- **Evaluations** owns the work: orchestration, validator plugins and verdict aggregation, per its
  module declaration and issues #17, #18, #19 and #20. [ADR 0006](0006-sandbox-execution-model.md)
  gives the orchestrator the deadline and the runner the container.

Whichever way the worker is placed, one module has to cause an effect in the other. [ADR 0007](0007-cross-module-contract-form.md)
§4 made every contract read-only for the slice and said a write need gets its own ADR rather than an
edit; [ADR 0005](0005-vertical-slice-before-breadth.md) forbids reaching into another module's
`DbContext`. So the shape of that crossing is a decision, and the first code to need it is the next box.
Left to #15, it would be made in a constructor by one session, and #17 would inherit whatever that was.

## Decision

### 1. Submissions owns the queue and the whole lifecycle

The Submissions module drains its own table, claims an attempt, runs it through evaluation, and
records the result. Every state change of a submission and every row of a report is written by
Submissions, in its own schema, through its own entity:

- **Claim** — `SELECT … FOR UPDATE SKIP LOCKED` over the queue index, `Submission.Start()`, commit.
  One short transaction.
- **Evaluate** — the command in §2, called with **no transaction open and no row locked**. It runs
  containers for minutes; a lock held across it would hold a connection for as long.
- **Record** — `Complete(score, at)` or `Fail(at)` and the report, in a second short transaction,
  applied only if the attempt is still the `Running` attempt this worker claimed.

The ownership rule of [#35](https://github.com/shoraLBRT/ritocode/issues/35) applies unchanged: the
drain reads by status and serves no user, so it is an allowance in `OwnershipRuleTests` that says so,
never a way around the rule.

### 2. Evaluations answers one command: evaluate this input

Evaluations is exposed to Submissions through a single contract in
`Ritocode.Shared/Contracts/Evaluations`, named for the command:

```csharp
public interface ISubmissionEvaluator
{
    Task<EvaluationOutcome> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken);
}
```

The request names what is evaluated and against what — the submission id (which keys the artifacts
and correlates the logs), the frozen input tree's reference as `submissions.input_reference` stores
it, and the problem version. The outcome is the verdict and what produced it: whether the pipeline
ran to the end, the score when it did, the per-validator results — carrying the runner's
`TimedOut`, `ResourceExhausted` and `Crashed` distinctly, as ADR 0006 §5 requires — and the reference
of the artifacts written. The exact records are #17's and #18's to write; this ADR fixes that they are
flat records in `Shared`, per ADR 0007 §3, and that nothing in them is a Submissions entity.

Evaluations reads what it needs through read contracts, as before — a version's validator
configuration from Problems, the frozen tree from object storage by the reference it was handed.

### 3. What this supersedes, exactly

ADR 0007 §4 said no contract method mutates another module's state. That stays true, and this ADR
adds **one** category beside read contracts — a **command contract** — admitted only while all of
these hold:

1. **The callee writes no other module's rows.** Its effects are containers it runs and objects it
   writes under keys derived from the request — `evaluation-artifacts/submissions/{id}/` — and nothing
   else. The caller records the result in its own schema.
2. **The result is returned, not published.** The caller gets the whole outcome from the call and
   decides what it means for its own state; the callee chooses no status of the caller's.
3. **Repeating the call is safe.** The same request produces the same verdict — the determinism claim
   of ADR 0006 §6 — and overwrites the same artifact keys, so a worker that died between evaluating and
   recording can evaluate again rather than needing to know what the first attempt did.

Everything else in ADR 0007 is untouched: the interface is one need wide, returns `Task<T>` with a
value and takes a `CancellationToken` last, is implemented `internal` by the owning module and
registered once — so its §7 assertions apply to this contract as they do to the read ones. A contract
that would write another module's rows still needs an ADR of its own; the first known case remains
user deletion in [#43](https://github.com/shoraLBRT/ritocode/issues/43).

### 4. How a run that could not finish is recorded

Following ADR 0006 §5 through: a run whose outcome is `TimedOut`, `ResourceExhausted` or `Crashed` is
not retried during the slice and becomes a **`Failed`** submission — and its report still carries the
per-validator results, so the runner's distinction between those three survives to the person reading
it. A pipeline that ran to the end is **`Completed`** with its score, whether the validators passed or
failed: a wrong answer is a score, not a failure. This settles the open question of where a report
carries a timeout or a resource exhaustion — in `submission_reports.validator_results`, per validator,
never in the submission status alone. The JSON shape of those results is #18's.

## Alternatives considered

**Evaluations drains, through write contracts into Submissions.** Closest to the wording of #17,
which lists "submission status updates" in the orchestrator's scope. Rejected: it needs several
writing contracts at once — claim, complete, fail, store a report — and the claim is a row lock taken
in Submissions' schema on behalf of another module, held or released across a module boundary. That
is the coupling ADR 0002 split the schemas to prevent, now with a transaction running through it, and
it makes Evaluations the author of a state machine it does not own.

**Domain events with an outbox.** Submissions publishes that an attempt is queued, Evaluations handles
it and publishes that an evaluation is done, and Submissions applies it. The loosest coupling, and the
shape a broker would take. Rejected for the slice: it needs an outbox table, a dispatcher and delivery
guarantees before the first verdict exists, and ADR 0005 already reserves "a broker instead of the
table" for when the table stops keeping up. Kept in reserve for the write-side cases ADR 0007 §4
pointed at, which are what events are good at.

**Collapse evaluation into the Submissions module.** No crossing at all. Rejected: the validator
plugins, the runner registry and aggregation are the part of the system most likely to earn its own
process, and ADR 0005's stage-two extraction of the worker is only a move if evaluation is already a
module of its own behind an interface.

## Consequences

- [#15](https://github.com/shoraLBRT/ritocode/issues/15) builds the queue inside Submissions: the
  claim over `SKIP LOCKED`, the guard that records a result only on the attempt still claimed, and the
  rule for an attempt left `Running` by a process that died — which §3.3 makes safe to evaluate again.
  That is the "one dispatch interface" of ADR 0005's reduction table, and it is internal to
  Submissions.
- `ISubmissionEvaluator` lands with its first implementation, in
  [#17](https://github.com/shoraLBRT/ritocode/issues/17): ADR 0007 §7's third assertion requires a
  contract to be registered exactly once, so the interface cannot precede the module that answers it.
  Nothing may register a stand-in that grades — a verdict not produced by a sandbox run is the first
  row of ADR 0005's forbidden list. Until #17, nothing claims an attempt, because a claimed attempt
  nobody can evaluate would sit `Running` forever.
- The worker loop is hosted in the API process during the slice. Neither module's domain code knows
  that; stage two's worker process composes Submissions, Evaluations and Problems, and moves no domain
  code.
- The concurrency cap of [#35](https://github.com/shoraLBRT/ritocode/issues/35) — how many evaluations
  run at once — sits naturally in the drain, which is the only place attempts start.
- `DATABASE_SCHEMA.md` gains no cross-schema write: every row of `submissions` and
  `submission_reports` is still written by the module that owns it.
- ADR 0007 is not edited. Its §4 now reads with this ADR beside it, and the next write need — the
  first that fails one of §3's three conditions — supersedes both rather than widening this one.
