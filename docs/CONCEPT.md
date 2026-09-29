# Concept

**This file defines what Ritocode is.** It replaces the product description in `README.md`,
`docs/AGENTS_OVERVIEW.md` and `docs/MVP_SCOPE.md`, all of which describe the product this project
used to be. The decision to change, and the paths rejected on the way, are in
[ADR 0010](adr/0010-diagnosis-of-ai-written-code.md).

- **Last updated:** 2026-09-30
- **Status:** agreed with the maintainer; the detailed specification and the backlog are written next
- **Working prototype of the mechanics:** <https://claude.ai/artifact/NgUY8CZZcDnbgApen2nf6S>

---

## 1. What Ritocode is

> A catalogue of what breaks in code written by AI — the cost of each problem, the conditions under
> which it is acceptable, and the way to treat it — and a trainer that teaches people to recognise
> those problems in code they did not write.

Two halves, and each has a job. The **catalogue** is what people find, link to and eventually
contribute to. The **trainer** is what they come back for. A single object — the *problem card* —
joins them.

## 2. Why it exists

Developers increasingly do not write code; they instruct an agent and accept or reject what it
produces. The skill that decides the outcome has shifted from writing to judging, and nothing
trains or even measures the new one.

The gap is sharper than "people should review better". **Judgement has lost its feedback loop.** A
senior used to correct you, and consequences arrived slowly enough to learn from. An agent produces
a change in two minutes, you approve it in three, and nobody ever tells you whether you were right.

And the failure is usually not the model's. The agent builds a full account-management service
because nobody said there would be two users. It builds something that will not scale because
nobody said how much data there is. **The defect is in the brief as often as in the code**, and
seeing that is the skill.

## 3. The skill being taught

> Look at code an agent wrote, **understand what is wrong**, and know **what it is treated with**:
> a better brief, a rule in the repository, an analyser, or hands.

The core of it is **proportion**. The same codebase is excellent or terrible depending on what it
is for. A full account-management service for two users is a failure; the same service for a
hundred thousand users is a necessity. A linter never sees that difference. An agent does not see
it because nobody told it. Only an engineer holding the context sees it — and that is exactly the
competence this product trains.

Naming the problem is also what produces the remedy. The "right prompt" everyone talks about is
only available to someone who has already diagnosed what went wrong.

## 4. The six classes of problem

The teaching content is organised by **cause**, because the cause decides the treatment.

| Class | What it looks like | Cause | Treatment |
| --- | --- | --- | --- |
| **Disproportion** | A full service for two users; CQRS over a hundred rows; abstractions with a single implementation. Or the reverse: everything in memory, no pagination | The brief never said how many users, how much data, how long it lives | The brief |
| **No knowledge of the project** | Its own retry mechanism, its own error format, a dependency for something already present | No project rules given; the task was handed over in one large piece | A repository rule |
| **Hygiene and security** | Secrets in the repository, hardcoded addresses, empty `catch`, missing validation, a lost ownership check | Default behaviour when no fence exists | Automation |
| **Growth without restructuring** | God classes of thousands of lines, duplication between neighbouring files, dead code | The agent appends and almost never rebuilds | Thresholds plus deliberate restructuring |
| **False confidence** | Tests that mock the thing under test, documentation that describes something else, a silently dropped requirement | The model optimises for visible success | Verifiability |
| **Domain** | Money in floating point, time without zones, invariants any caller can break | The model does not know the domain and was not told its rules | Invariants in the brief and in tests |

Two properties of this table matter more than its contents. **Each class has its own remedy** — that
mapping is what a learner takes away. And **a person should not be catching class three by eye**:
recognising that a fence is missing is the engineering act; finding the individual key is the
scanner's job.

## 5. The problem card is the atom

Not an article arguing that something is bad. A structured object:

| Field | Content |
| --- | --- |
| Name and one-line description | What it is called and how it is recognised, in one line |
| Signs | What is visible in the code |
| Why AI does it | The mechanism that produces it |
| Cost | Concretely, what becomes more expensive — including the cost to the *next agent run* |
| When it is acceptable | The conditions under which this is fine |
| How to detect it automatically | The analyser, threshold or check, where one exists |
| How it is treated | And what the wrong treatment looks like |
| Sources | Fowler, Beck, Martin, Yegor Bugayenko, research, Sonar rules |
| Counter-arguments | Where the industry disagrees, linked to the opposing card |

**"When it is acceptable" is not a softening — it is the field the whole product turns on.** Without
it, a card is a slogan and the trainer teaches nitpicking. With it, the same card yields different
answers in different contexts.

The card is used three ways: as the teaching text, as an **answer option** in a task, and as a line
in the recipe a learner takes home. One object, three roles.

The catalogue ships with the product; community authorship is a later stage (§9).

## 6. What a task is

A task is a **diagnosis**, not a repair.

**Given to the learner:**

- **the project context** — what it is, how many users, how long it lives, who maintains it, what
  happens if it breaks;
- **the brief the agent was given**, in full, including what it fails to say;
- **the material** — a whole small project, not a sixty-line diff. Disproportion is only visible on
  the whole.

**Step 1 — Diagnosis.** The learner picks from the **entire catalogue**, searchable by name and
description, grouped by the six classes. Not a shortlist: with a shortlist the answer is recognised
rather than found. Cards show only name and one-line description here; everything else would leak
step 2. Full cards are readable during the task — a reference is on the desk in real work too.

**Step 2 — Treatment.** For each card the learner selected, a two-level tree. Multiple selections
are allowed at both levels.

1. **The brief** — scale, lifetime, who maintains it, boundaries of the task, required properties,
   reuse what exists, explicit prohibitions.
2. **A repository rule** — structure and layer boundaries, size and complexity thresholds, required
   mechanisms, banned APIs, test requirements.
3. **Automation** — analyser, metric gate, secret scanner, dependency check, architecture test,
   test-quality check, formatter, performance check.
4. **By hand now** — split by reason for change, remove duplication, extract configuration, protect
   an invariant, fix a representation, handle an error explicitly, delete the excess, write tests.
5. **Accept deliberately** — a condition from "when it is acceptable" holds; the cure costs more
   than the disease; defer as debt with a review condition.

The fifth branch is mandatory. Without it the product teaches nitpicking rather than proportion.

**The second level comes from the card, not from the task author.** A card already carries "how to
detect", "how it is treated" and "when it is acceptable" — those are the admissible branches. The
task author states only *which branch is right in this context*. This is what keeps authoring cheap:
the card says what a problem **can** be treated with, the context says what it **must** be treated
with here.

**Step 3 — Recipe and review.** One score. Under it, the composition: how many real problems were
found, how much was flagged that was not there, how many treatments were exact. Then a
problem-by-problem review where the learner's treatment sits beside the reference and the full card
opens on the ones they got wrong. Identical treatments across different problems are **merged
automatically** into one recipe, so the lesson "there are fewer cures than diseases" arrives as a
conclusion rather than as another exercise.

### Scoring

One number. A correct card scores, a card that is not there costs, a missed one simply does not
score. Treatment scores per branch and per leaf. No severity rating: the learner marks the problem,
and the context decides what it costs.

### The mechanic that carries the idea

**The same material in a different context has different right answers.** A god class in a
throwaway utility is accepted; the same class in a billing core is split and fenced with a
threshold. Secrets, money in floating point and a swallowed error are wrong in both. The learner
discovers which problems depend on context and which never do — and, in the same movement, that
where the answer moves with the context, the fault is usually in the brief rather than in the agent.

Practically, this also means one piece of material yields several tasks.

## 7. What the learner takes away

Not points. **A recipe usable tomorrow morning**: a paragraph of constraints to add to their briefs,
a list of rules for their repository's conventions file, a set of analysers to switch on once.

This is the test of whether the product works. If after ten diagnoses a person has their own
checklist for instructing agents, it works. If they have only a score, it does not.

## 8. What Ritocode is not

- **Not algorithmic exercises.** Nothing here is written from scratch.
- **Not a linter with a scoreboard.** What a compiler or an analyser finds is not what a person is
  trained on; automation is one of the *answers*, not the subject.
- **Not grading a refactoring by running tests.** That path was tried and abandoned — see ADR 0010.
- **Not an opinion engine.** A card states cost and conditions, never a verdict that something is
  universally bad.
- **Not a blog aggregator.** An article that argues a case is welcome as a source, not as the unit.

## 9. Open questions

Carried forward deliberately; the detailed specification decides them.

- **Where material comes from.** Generated reference projects, small open-source repositories, and
  real agent output collected from real runs. The cost per task differs sharply between them.
- **Difficulty levels.** The maintainer's proposal: an easy mode narrows the catalogue to 10–15
  candidate cards instead of the full set. Secondary to the core mechanics.
- **Mobile.** Diagnosis is a reading-and-choosing activity, so it suits a phone better than an
  editor ever did. Worth designing for early even if it ships later.
- **Free-text answers.** Dropped for now because they cannot be graded objectively. They may return
  as an optional, unscored input once there is something to compare them against.
- **Community authorship.** Cards written by developers, with counter-arguments attached rather than
  votes on truth. Stage three, after a core catalogue and an audience exist.
- **Fairness of traps.** A plausible but wrong card currently costs as much as a correct one earns.
  Whether that is the right balance is a tuning question, not a structural one.
- **Real findings outside the answer key.** A learner may spot a genuine defect the author never
  planted. That must be a signal to the author, not an error for the learner.

## 10. Where the existing code stands

The platform was built for the previous product. What survives, what waits:

- **Survives:** the modular monolith and its boundary rules, API conventions, persistence and
  migrations, the identity seam, the catalogue and ingest, submissions and attempt history, the
  frontend shell and its API client, CI.
- **Waits:** the workspace editor, file read and write, and the sandbox runner. Diagnosis does not
  execute the learner's code. These stay in the repository and become relevant again only if a task
  type appears that asks someone to change code or prove a defect with a test.
- **Replaced:** the problem package format, which describes a refactoring exercise with validators,
  and everything downstream of it — validator plugins, the evaluation pipeline, scoring.

Nothing is deleted before the specification says what replaces it.
