# 0018 — Studying the problems as a use case of its own

- Status: Accepted (option A, 2026-11-18)
- Date: 2026-11-04
- Relates to: [#204](https://github.com/shoraLBRT/ritocode/issues/204)
- Builds on: [CONCEPT.md](../CONCEPT.md) §1, §6; [SPEC.md](../SPEC.md) §3.2, §4.2, §4.7;
  [ADR 0010](0010-diagnosis-of-ai-written-code.md)

## Context

Today a problem is learned in one way: by meeting it in a task. The catalogue (`/problems`) is a
reference — read, searched, linked to — and progress (SPEC §4.7) counts only what tasks measured:
per class and per card, met, found, missed, treated right.

The maintainer's feedback of 2026-11-04: **studying the problems** should be a use case of its own,
with its own progress. A learner who wants to know the 56 problems before, or beside, solving tasks
has nothing to do on the catalogue but read, and nothing records that they did.

This changes what the product is — CONCEPT.md §1 names two halves, a catalogue that is *found and
linked to* and a trainer that is *come back to* — so it is decided before anything is built.

Three constraints shape the options:

- **Authoring stays cheap** (AGENTS.md; the maintainer's standing rule). 56 cards took 20–30 hours;
  a mechanic that asks for more per card needs a very good reason.
- **Tasks must not leak.** A class tag on a task gives its answer away (SPEC §4.3), so does anything
  that says "this task contains card X" before the task is solved.
- **A finding has no location in the MVP** (SPEC §3.5; locations are [#207](https://github.com/shoraLBRT/ritocode/issues/207),
  *Later*). Nothing can cut "the lines where card X is" out of a material yet.

## Options

| | What studying a card is | Build | Authoring | Teaches |
| --- | --- | --- | --- | --- |
| **A. A mark** | Read the card on `/problems`, press *Изучено* | Small: a table of (user, card, date), two endpoints, a toggle, a progress section | None | Nothing by itself — it records reading, which is self-report |
| **B. Recognise by its signs** | A short drill: the card's *Signs* section is shown without its name; pick the name among four cards of the same class. Right answers move a card through Leitner boxes (1, 3, 7, 21 days); a card in the last box counts as known | Medium: the drill page, deterministic distractors and scheduling, a table of (user, card, box, due date), a progress section | None — generated from fields every card already has; the *Signs* text names nothing (checked on `god-class`, `swallowed-exception`, `parallel-v2-file`), and same-class neighbours are the hardest distractors because their summaries delimit each other (SPEC §3.2) | Telling neighbouring problems apart by their visible signs — the step-1 skill, without code |
| **C. Authored snippets** | Each card gets 2–3 snippets of 5–15 lines: "is this problem here?" and, with a one-line context, "what is it treated with?" | Medium | **High**: 110–170 snippets, each a small material that must be right; drafted by AI, still reviewed one by one | Recognition in real code, the closest to a task |
| **D. Snippets cut from tasks** | As C, but the snippet is the lines of a finding in an existing material | Medium, after #207 | Locations on every finding (#207) | As C, and the snippet is spoiled for that task |
| **E. "Practise this card"** | From a card, the tasks whose key contains it | Small | None | Leaks: the learner opens a task knowing one of its answers. Rejected |

## Decision (2026-11-18)

The maintainer chose **option A — the mark «Изучено» — with a chart of its progress on the profile.**
B, C and D are not built now; they are filed under *Later*. The proposal below is kept as it was
put, and the open questions are answered for A:

- **What studying is.** A signed-in learner marks a card *Изучено* on the opened card (`/problems`,
  SPEC §4.2) and can take the mark back. A folded card shows its mark, and a class heading counts
  the marked cards of the class.
- **A guest** (question 2: yes, without progress) can press *Изучено* too; the mark is kept in that
  browser only, is shown on `/problems`, goes into no progress, and is not carried over on sign-in.
- **The streak and activity** (question 1: yes) count a card marked on a day as practice on that
  day, like a submitted attempt: a day with a mark keeps the streak, and the day's activity counts
  attempts and marks. A mark taken back no longer counts.
- **Progress** (question 4: apart) gets a section of its own on the profile — cards studied out of
  the published cards, per class and in all, and a chart of the cards studied over time. Task
  mastery, the per-card task counts and the first-attempts rule are untouched; a card found in tasks
  does not become studied, and a mark changes no task's result.
- Question 3 (the mark beside the drill) falls away with the drill.
- A mark on a card later retired is kept but counted nowhere; it counts again if the card returns.
- The marks belong to the learner's progress, so the **Attempts** module owns them, in its own
  schema; the card is checked against Content through a contract, as attempts check tasks.

## Proposal (2026-11-04)

**A and B together, as one *study* mode; C and D stay open for later.**

- On `/problems`, each card gets *Изучено* (A) for a signed-in learner; a page `/problems/study`
  runs the drill (B) over the cards due today, a class at a time or all, ten at a time.
- **Progress** gets a section of its own, beside task mastery and never mixed into it: per class,
  cards *studied* (marked or in the last box) of all cards in the class, and per card the box it is
  in. Task scores, the per-card task counts and the first-attempts rule are untouched.
- **Scoring is not involved.** The drill has no points, only boxes; nothing it records changes a
  task's result (AGENTS.md non-negotiable 5).
- **Activity and the streak** count a finished drill of ten as a day's practice, like a submitted
  attempt — a choice for the maintainer (open question 1).

Why this pair: both cost **no authoring**, B is a real exercise rather than a checkbox, and B trains
the half of step 1 that tasks train least — telling apart the cards whose names a learner already
half-knows. C is the better exercise and the expensive one; it is worth doing when the catalogue has
settled, and D becomes cheap once #207 gives findings a location.

## Open questions for the maintainer (answered above, 2026-11-18)

1. Does a drill count toward the streak and activity, or do they stay "tasks only"?
2. A guest: may they run the drill without progress (as they may solve a task without a result), or
   is study a signed-in feature?
3. Is the mark (A) worth having beside B, or is "studied" only what the drill says?
4. Should a card met and found in tasks count as studied, or do the two kinds of progress stay apart?

## Consequences

- CONCEPT.md §1 names studying as part of the catalogue half; SPEC §4.2 gets the mark and §4.7 its
  progress, the streak and activity; DOMAIN_MODEL and DATABASE_SCHEMA get the marks' table with the
  issue that builds it.
- Follow-up issues: the mark, API and `/problems` ([#280](https://github.com/shoraLBRT/ritocode/issues/280)),
  and the profile's study section with the streak and activity counting marks
  ([#281](https://github.com/shoraLBRT/ritocode/issues/281)). Under *Later*: B
  ([#282](https://github.com/shoraLBRT/ritocode/issues/282)), C and D
  ([#283](https://github.com/shoraLBRT/ritocode/issues/283)).
