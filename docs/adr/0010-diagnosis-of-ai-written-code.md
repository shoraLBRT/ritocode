# 0010 — Ritocode teaches diagnosis of AI-written code

- Status: Accepted — decided by the maintainer on 2026-09-30
- Date: 2026-09-30
- Relates to: [`docs/CONCEPT.md`](../CONCEPT.md), the mechanics prototype at <https://claude.ai/artifact/NgUY8CZZcDnbgApen2nf6S>
- Supersedes: 0005 — the milestone and its slice plan, not the engineering rules it restates
- Leaves in place, off the critical path: 0006, 0009 — later removed with the code they governed (#119, #40)

## Context

The project's stated purpose has always been to help developers evaluate code they did not write,
in a world where an agent writes most of it. The implementation drifted away from that purpose
without anyone deciding to let it.

The drift had one cause, and it is worth naming precisely: an unexamined axiom that **a solution is
graded only by deterministic validators running the learner's code**. That axiom is sound for
algorithmic exercises, where a right answer exists and a test can find it. Applied to judgement, it
shrank the product at every step:

1. "Improve this code" cannot be graded by tests, because a beautiful answer and an untouched
   workspace both pass.
2. So every authored problem had to hide a behavioural defect that tests could catch.
3. So the product became defect-hunting — exactly what a compiler, an analyser or a model already
   does, and the opposite of what the project exists for.
4. Each authored task needed the maintainer to invent the defect, the fixtures and the grading. The
   content pipeline required human attention per unit, which does not scale.

Stage 4 of the slice was complete and stage 5 had begun when the maintainer stopped the work and
said the project no longer matched the idea. What follows replaces the product, not the engineering
standards.

The insight that resolved it: **the failure in AI-written code is as often in the brief as in the
code.** An agent builds an account-management service for two users because nobody told it there
would be two. It builds something unscalable because nobody said how much data there is. Seeing
that — and knowing whether the answer is a better brief, a repository rule, an analyser, hands, or
nothing at all — is a real, teachable, unserved skill.

## Decision

**Ritocode is a catalogue of what breaks in AI-written code and a trainer that teaches people to
recognise it.** The full model is [`docs/CONCEPT.md`](../CONCEPT.md); this ADR records the decision
and its cost. In short:

1. The unit of practice is a **diagnosis of a whole small project**, not a repair and not a
   line-by-line review of a diff. Disproportion is invisible on a sixty-line diff.
2. The unit of content is a **problem card** carrying cost, conditions under which the problem is
   acceptable, detection and treatment — never a verdict that something is universally bad.
3. A task presents **context, the brief the agent was given, and the material**. The learner picks
   problems from the entire catalogue, then chooses a treatment for each from a fixed two-level
   tree, then receives a score, a merged recipe and a review.
4. **The same material under a different context has different right answers.** This is the
   mechanic the product is built on, not a feature of it.
5. Ground truth comes from the **task's answer key** — the context we set, the constraints we
   deliberately left out of the brief, and the conditions written in the cards — rather than from
   executing the learner's code. The axiom above is hereby withdrawn: determinism of grading is
   retained, execution as the source of it is not.

The name Ritocode is kept.

## Alternatives considered

**Finish the refactoring trainer as planned.** Stage 4 was done and the runner already worked.
Rejected: it answers the product question with the wrong product. Its grading can only see
behaviour, so it teaches defect-hunting and calls it code quality.

**Grade design by the cost of the next change.** Two-stage tasks: refactor, then receive a hidden
new requirement and be measured by how large the resulting change is. Rejected for now, and it
remains the most interesting deterministic way to measure design. It needs stages in the task
format and in the submission lifecycle, it still asks the learner to write code, and it measures
design without teaching what to call the problem — which is the skill actually missing.

**Generate tasks by transforming a known-good codebase.** Apply a catalogue of mechanical defect
injections to clean repositories, so the answer key exists by construction and no human validates
any single task. Rejected by the maintainer: it solves the content-scaling problem and produces
exercises that feel manufactured — the interesting failures of real agents are not a list of
rewrites. Worth revisiting as a source of *material*, never as the definition of the product.

**Line-by-line review of a diff.** Closer to daily work than a whole project, and easier to score
by line ranges. Rejected as the primary unit: the problems this product is about — proportion,
duplication of what already exists, a design that will not survive the next year — are not visible
inside one diff. Diff review may return later as a second task type.

**AI review as the score.** A model compares the learner's findings against a rubric and explains
the verdict. Rejected for the core loop: non-deterministic, costly, sends the learner's work to a
third party, and replaces a defensible answer key with an opinion. Possible later as commentary
that does not affect the score.

**A community wiki of best and bad practices first.** Developers write the articles, others vote.
Rejected as a starting point on the evidence of the Portland Pattern Repository: community
catalogues need an audience and a core corpus before contribution starts, and voting turns
technical questions into elections. Community authorship stays as stage three, with
counter-arguments attached to cards rather than votes on truth.

**A mirror over the developer's own repository.** Rather than authored tasks, read the learner's
real history — what they approved, what was hot-fixed a week later — and report their blind spots.
Not rejected on merit and not chosen now: it removes the content problem entirely but requires
repository integration before there is any product to integrate, and it cannot teach vocabulary the
learner does not yet have. It is the natural successor once the catalogue exists.

## Consequences

- **ADR 0005 is superseded**: the vertical slice, its plan
  and its definition of done describe the previous product. Its engineering rules — user code only
  in a sandbox, no `user_id` from a request body, ownership checked in the query, no schema change
  without a migration — survive because they are restated in `AGENTS.md` and enforced by tests.
- **ADR 0006 and ADR 0009
  remain correct** for the code that implements them, and leave the critical path. Diagnosis runs
  nothing the learner wrote. The sandbox runner of #21 stays in the repository, unregistered work
  that becomes relevant again only if a task type asks for code or for a proof-by-test.
- **The problem package format is replaced.** `docs/PROBLEM_PACKAGE_SPEC.md` describes a refactoring
  exercise with validators, fixtures and weights. The new format carries context, the agent's brief,
  material, an answer key and traps. Ingest, the catalogue and the storage layout survive; what they
  carry changes.
- **`README.md`, `docs/MVP_SCOPE.md`, `docs/AGENTS_OVERVIEW.md`, `docs/PROJECT_STATE.md`,
  `docs/SLICE_PLAN.md` and `docs/EVALUATION_PIPELINE.md` no longer describe this product.** Each is
  marked as superseded in this change; each is rewritten or retired when the specification lands.
- **The backlog is rewritten.** The 30-odd open Phase 1 issues were scoped against the previous
  product. Sorting them — closing, re-scoping, replacing — is the first work after the
  specification, not before it.
- **Determinism survives the change.** A task still produces the same verdict for the same answer,
  because the answer key is data. What changed is where the truth comes from.
- This ADR is superseded, not edited, if the concept changes again.
