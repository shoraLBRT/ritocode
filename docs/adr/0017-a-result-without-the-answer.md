# 0017 — A result without the answer, a deliberate reveal, and four task statuses

- Status: Accepted
- Date: 2026-11-12
- Relates to: [#229](https://github.com/shoraLBRT/ritocode/issues/229), [#235](https://github.com/shoraLBRT/ritocode/issues/235)
- Builds on: [ADR 0010](0010-diagnosis-of-ai-written-code.md); [SPEC.md](../SPEC.md) §4.3, §4.5, §4.8,
  §5.4, §9.3; [ADR 0007](0007-cross-module-contract-form.md)

## Context

Until #229 the review opened the whole answer key the moment *Check* was pressed: every finding found
or missed, the author's leaves beside the learner's, every extra pick by name, the notes and the
lesson. A learner who had not solved the task was handed the solution, and the next attempt was
copying, not diagnosing. The task list knew only "submitted an attempt or not", so a task failed
badly looked the same as one solved cleanly.

The maintainer asked (2026-11-11) for a result that says how well the answer did without saying what
the answer is, for revealing the answer as a separate, deliberate action, and for four statuses in
the task list. On 2026-11-12 the maintainer settled the open points of the issue: what "solved"
means, what a clean task does, that progress does not change, and that signals wait for the reveal.

## Decision

**After *Check* the result is the outcome only**: the score in points and the counts — findings
present, found, treated right, missed, and extra picks — and whether the attempt solved the task. It
names no card and no leaf. The API enforces it: before the reveal, `GET /attempts/{id}` and the
submit's answer carry `outcome` and leave `answer`, `result`, `review` and `signalledCards` empty.
Hiding it only in the page would leave the key one request away.

**Solved** is: every finding of the key found, each with at least one of the key's leaves, and no
extra pick. A wrong leaf beside a right one costs points (SPEC §5.2) but does not undo it, so solved
is not the scorer's `isCorrect`, which any lost point breaks. On a clean task, solved is an answer
that picked nothing; any pick leaves it tried. It is a pure function of the stored result
(`AttemptOutcome.Of`), computed on read like progress, so no column freezes a rule that may still be
tuned — and a stored result is never rewritten (SPEC §5.4).

**The reveal is per learner and task, kept for good**: `POST /attempts/{id}/reveal` from a submitted
attempt (`409 attempt_not_submitted` from an open one) writes a row of `attempts.reveals` — the
learner, the task, when — once; revealing again changes nothing, and a second reveal racing the
first is settled by the primary key. From then on every attempt at the task, earlier or later, is
answered in full, and a later attempt remains possible as practice. The reveal is its own row, not a
column of an attempt, because a submitted attempt never changes (SPEC §5.4) and because the answer
is revealed to the learner, not to one attempt. A reveal is a learner's row like an attempt, behind
the ownership rule (`OwnedReveals`).

**Four statuses**, a pure function of the learner's submitted attempts at the task and the reveal
(`TaskStatuses.Of`):

| Status | When |
| --- | --- |
| `new` | no submitted attempt |
| `tried` | submitted attempts, none solving it, the answer not revealed — whatever the score |
| `solved` | an attempt solved it no later than the reveal, if there was one |
| `gaveUp` | the answer was revealed before any attempt solved it |

So a task solved and then revealed stays solved, and one revealed and then solved in practice stays
gave up. `GET /tasks` carries `status` instead of `solved` for a signed-in caller, through
`ITaskStatusLookup`, which replaces `ISubmittedTaskLookup`; the attempt carries `taskStatus` too, so
the review can say what revealing will do. Signed out, no status, as before.

**Progress is built from first attempts as scored** (SPEC §4.7), and **per card it waits for the
reveal** (#235, the maintainer's option (b), 2026-11-12). A first attempt counts toward a card's met,
found, missed, picked when absent and treated right only once the learner has revealed that task's
answer; before that the per-card table — the page and `GET /me/progress` alike — names none of the
task's cards, picked or in the key. The per-class counts and the number of tasks still take every
first attempt: they name no card. `GET /me/progress` carries `notRevealed`, how many of those tasks
the cards leave out, and `/progress` says so in a note above the table, which shows alone while no
answer is revealed. The reveal adds no attempt and changes no score: it only lets a task's first
attempt into the per-card table (`ProgressCalculator` takes each first attempt with whether its
answer is revealed). The status is only the task list's view.

**Signals wait for the reveal** (SPEC §4.8): before it there is no named extra pick to signal, and
`POST /signals` answers `409 answer_not_revealed` whatever the card, so the refusal cannot be used to
probe the key.

## Consequences

- The review has two states: the outcome with *Попробовать снова* and *Показать ответ*, and, once
  revealed, the full review of SPEC §4.5. The reveal asks for confirmation and says what it does to
  the task's status (DESIGN.md › The review).
- The counts on a clean task say "0 of 0" before the reveal, and a score's maximum says how much the
  key holds: the counts the issue asked for carry that much by design.
- `/me/progress` named every card met, found and missed from first attempts until #235, so a learner
  who opened their progress before revealing could read which cards a task held. Since #235 the
  per-card table leaves those tasks out; a card met only in unrevealed tasks is not listed at all.
- **The per-class counts still narrow an unrevealed key** (#235). No class names a card — each holds
  nine or ten in the live catalogue, so a count alone points at none — but read against what the
  learner knows, their own picks, they say more than the outcome. *Met* per class says which classes
  an unrevealed key holds and how many findings in each, where the outcome gives only the total;
  *found* per class says which picks were found whenever the picks fall in different classes — a
  learner who picked one card of a class learns from that class whether it was in the key. The class
  counts minus the per-card table are exactly the unrevealed tasks' share. The issue recommended
  keeping the classes whole, and that is what was built; whether they should wait for the reveal too
  is an open question for the maintainer (PROJECT_STATE.md).
- The admin area's "tasks solved" keeps its old meaning — tasks with a submitted attempt.
- Computing the status reads a learner's submitted results at a page of tasks: a few dozen rows at
  the MVP's scale. A stored flag per attempt is the way out if it ever shows.

## Alternatives not taken

- **Hiding the breakdown in the page only.** The API would still send it; anyone could read it.
- **A `revealed_at` column on the attempt.** A submitted attempt is never changed, and the reveal
  belongs to the task as the learner knows it, not to one attempt.
- **A score threshold for solved** (e.g. 80 % of the maximum), or "all found, extras allowed": the
  maintainer chose the stricter rule, so solved means nothing was missed and nothing added.
