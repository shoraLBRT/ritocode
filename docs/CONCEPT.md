# Concept

**This file says what Ritocode is and why.** What it does, screen by screen and rule by rule, is in
[`SPEC.md`](SPEC.md); the files content is written in are in [`CONTENT_FORMAT.md`](CONTENT_FORMAT.md).
The decision to change the product, and the paths rejected on the way, are in
[ADR 0010](adr/0010-diagnosis-of-ai-written-code.md).

- **Last updated:** 2026-09-30 — cut back to what the maintainer confirmed while the specification
  was written. The earlier version promised a take-home "recipe" as the product's test of success;
  that was never agreed and is now a later iteration in [`SPEC.md`](SPEC.md) §12.
- **Prototype of the mechanics:** <https://claude.ai/artifact/NgUY8CZZcDnbgApen2nf6S> — it predates
  the specification, and where the two differ, the specification wins.

---

## 1. What Ritocode is

> A catalogue of what breaks in code written by AI — what each problem costs, when it is acceptable,
> and how it is treated — and a trainer that teaches people to recognise those problems in code they
> did not write.

Two halves. The **problem catalogue** is what people find and link to. The **trainer** — a catalogue
of tasks — is what they come back for. The **problem card** joins them: it is the teaching text, the
answer a learner picks, and the explanation they read afterwards.

## 2. Who it is for

**Vibe coders**: people who build software by instructing agents and know little about
architecture. The content starts from the basics.

## 3. Why it exists

More and more code is not written but accepted: an agent produces a change in two minutes, a person
approves it in three, and nobody ever tells them whether they were right. The skill that decides the
outcome has moved from writing to judging, and nothing trains it.

And the failure is often not the agent's. It builds a full account service because nobody said
there would be two users. It builds something that will not scale because nobody said how much data
there is. **The defect is in the brief as often as in the code**, and seeing that is part of the
skill.

## 4. The skill

> Look at code an agent wrote, **see what is wrong**, and know **what it is treated with**: a better
> brief, a rule in the repository, an automatic check, a fix by hand — or nothing, deliberately.

Its core is **proportion**. The same code is fine or terrible depending on what it is for. A god
class in a script that lives for a quarter is acceptable; the same class in a billing core must be
split and fenced. Money in a float and a swallowed error are wrong in both. A linter never sees that
difference, and an agent does not see it because nobody told it.

## 5. The six classes of problem

Problems are grouped by **cause**, because the cause decides the treatment.

| Class | Looks like | Cause | Usual treatment |
| --- | --- | --- | --- |
| **Disproportion** | A full service for two users, or everything in memory for a million rows | The brief never said the scale | The brief |
| **No knowledge of the project** | Its own retries, its own error format, a dependency for what exists | No project rules given | A repository rule |
| **Hygiene and security** | Secrets in the repository, an empty `except`, a lost ownership check | No fence exists | An automatic check |
| **Growth without restructuring** | God classes, duplication, dead code | The agent appends and rarely rebuilds | Limits, then restructuring |
| **False confidence** | Tests that mock what they test, a silently dropped requirement | The model optimises for visible success | Verifiability |
| **Domain** | Money in floating point, time without a zone | Nobody told it the domain's rules | Invariants in the brief and tests |

## 6. A task

A task is a **diagnosis, not a repair**. The learner gets the project's **context**, the **brief** the
agent was given, and the **material** — a small whole project. Then:

1. **Diagnosis** — pick, from the problem catalogue, what they see in the code.
2. **Treatment** — for each pick, choose from a fixed tree what it is treated with here, including
   leaving it deliberately.
3. **Review** — a score, and each problem beside the author's answer, with the full card.

**The same code in a different context is a different task with different right answers.** That is
the mechanic the product is built on.

## 7. What Ritocode is not

- **Not algorithmic exercises.** Nothing here is written from scratch.
- **Not a linter with a scoreboard.** Automation is one of the *answers*, not the subject.
- **Not grading a refactoring by running tests.** That path was tried and abandoned — see ADR 0010.
- **Not an opinion engine.** A card states cost and conditions, never that something is universally
  bad.
