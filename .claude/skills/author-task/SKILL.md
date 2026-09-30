---
name: author-task
description: Write a Ritocode task — a material (a small Python project an agent is supposed to have written) and one task per context over it, under content/materials/ and content/tasks/ — from the maintainer's idea, check it with content validate, and run the blind smoke test, in which a separate AI session that sees only what a learner sees answers the task and every difference from the answer key is reported. Use whenever the maintainer wants a new task or material ("write a task about …", "an easy task with secrets and money", "add a context to this material", "the next batch of tasks"), and before writing any task by hand. It reads the live catalogue and treatment tree every run.
---

# Author a task

A task is a diagnosis, not a repair: the learner reads a **material** — a small Python project — in
a **context**, with the **brief** the agent was given, picks the problems they see from the
catalogue and says what each is treated with here. The format is
[`docs/CONTENT_FORMAT.md`](../../../docs/CONTENT_FORMAT.md) §5–§6; what a task is and how it is
scored is [`docs/SPEC.md`](../../../docs/SPEC.md) §3.4, §3.5, §4.4 and §5. Read them before the
first task of a session.

You draft; **the maintainer decides**. What you write is a proposal they edit, and the smoke test
report is theirs to act on.

## 1. Read the live content — every run

Never work from memory or from a list in this file. Read, from the working tree:

- `content/taxonomy/classes.yaml`, `content/taxonomy/treatments.yaml` and `content/taxonomy/ru.yaml`
  — the classes, and every leaf of the treatment tree with its meaning.
- Every `content/problems/*/card.yaml` and `content/problems/*/ru.md` — the name, summary and class
  of each card, and the `Signs` and `Acceptable when` of the cards the task will use.
- The slugs under `content/materials/` and `content/tasks/`, so a new slug does not collide, and the
  existing tasks over a material you are adding a context to.

## 2. Take the maintainer's idea

You need four things. Ask for whatever is missing rather than inventing it:

1. **What the project is** — a sentence or two.
2. **The difficulty** — easy, medium or hard. It sets the size band (SPEC §3.4): easy is at most 80
   lines in 1–2 files, medium 300 in up to 6, hard 600 in up to 12.
3. **The problems it should contain** — cards from the catalogue, by slug or by name. **A problem
   with no card stops you**: the card is written first, with `author-card`, and the task after it.
4. **One to four contexts** — each becomes its own task over the same material, with its own key.
   What changes between contexts is proportion: users, data, lifetime, who maintains it, what
   happens when it breaks.

Class 2, *project knowledge*, has no tasks in the MVP (SPEC §3.1) — its problems need an existing
project the agent ignored, and every material is a new project.

## 3. Write the material

`content/materials/<slug>/material.yaml`:

```yaml
language: python
notes: >-
  For authors only, never shown to learners: which problems were planted,
  where, and which tasks use this material.
```

`content/materials/<slug>/files/` — the project exactly as the learner sees it. Rules (SPEC §3.4,
CONTENT_FORMAT §5):

- **Python**, and **English** in identifiers, comments and string literals.
- **Within the size band** of the difficulty. Exceed it only when a file's size is itself the sign
  (a god class of 1 800 lines on a hard task), and say so in the notes.
- **Lines of at most 79 characters.**
- **No hint comments.** Nothing that names or excuses a problem — no `# hardcoded for now`, no
  `# TODO: validate`. A problem shows in what the code does, never in what it says about itself.
- **Fake secrets, never shaped like a real provider's token** — no `AKIA…`, `ghp_…`, `sk_live_…`,
  `xoxb-…`. A plain password or a connection string makes the same point.
- Dependencies, if any, in `requirements.txt` or `pyproject.toml`; the overview lists them.

**Plant only what was asked for, and write everything else well.** Code written to contain problems
tends to contain extra ones, and a learner who finds one of those is charged for it (SPEC §5.2).
Before moving on, read the finished material against **every card in the catalogue**, not just the
planted ones, and remove whatever you find that was not asked for. Common strays: an empty or broad
`except`, magic numbers, hardcoded paths and hosts, unvalidated input, `datetime.now()` without a
zone, a function doing three jobs, duplicated blocks.

## 4. Write one task per context

`content/tasks/<slug>/task.yaml`:

```yaml
material: <material-slug>
difficulty: easy            # easy | medium | hard
findings:
  - card: <card-slug>
    leaves: [<branch.leaf>, <branch.leaf>]
```

`content/tasks/<slug>/ru.md`:

```markdown
---
title: <Russian title>
---

## Context

<Who uses it, how much data, how long it lives, who maintains it, what happens when it breaks.>

## Brief

«<The instruction the agent was given, verbatim — including what it failed to say.>»

## Notes

### <card-slug>

<Optional, shown beside that finding in the review: why it matters, or does not, in this context.>

## Lesson

<Optional closing text of the review.>
```

- **The key lists every card present in the code**, including one that is fine here — that finding's
  leaf is then `accept.*`. The exception is a card whose name is itself a verdict relative to
  context, such as over-engineering: it is in the key only when the context makes it a problem
  (SPEC §3.5).
- **Leaves are what is right in this context.** List every leaf a competent person could defend —
  several leaves mean *any of them is right* (SPEC §5.2). Use only leaves that exist in
  `treatments.yaml`, and only card slugs that exist in `content/problems/`.
- **The context carries the proportion.** It must say what the key depends on — scale, lifetime,
  who maintains it, the cost of failure — in plain words, without naming a card or a treatment.
- **The brief is the agent's instruction as given**, in quotation marks. What it failed to say is
  often the lesson.
- A **clean task** — `findings: []` — is a valid task.
- Never set a weight: it belongs to the card.

## 5. Validate

```bash
dotnet run --project src/Ritocode.ContentTool -- validate content
```

Fix every error. A warning may stay if you say why — while the catalogue is small, an easy task's
shortlist cannot reach its size, and that warning is expected.

## 6. The blind smoke test — every task, before it is handed over

A **separate AI session** answers the task seeing **exactly what a learner sees** and nothing else.
Its answer is compared with the key, and every difference is reported.

**Isolation from the key is the whole point of the test.** The smoke session must never see:

- `task.yaml` — the findings and their leaves;
- the task's `## Notes` and `## Lesson`;
- `material.yaml` and its notes;
- any full card — only names and summaries;
- this conversation, the maintainer's idea, or anything you wrote while planning.

So it never runs as a subagent of this session, never inside the repository, and never with tools.
It gets one file, the learner's view, and answers from that alone.

**Run it like this**, once per task — a fresh session per task, even for tasks over the same
material, so one context's answer cannot colour another's:

```bash
dotnet build src/Ritocode.ContentTool
smoke=$(mktemp -d)       # a new, empty directory outside the repository
dotnet run --no-build --project src/Ritocode.ContentTool -- learner-view <task-slug> > "$smoke/view.md"
cat .claude/skills/author-task/smoke-prompt.md "$smoke/view.md" > "$smoke/prompt.md"
(cd "$smoke" && claude -p --safe-mode --tools "" --strict-mcp-config --no-session-persistence \
  < prompt.md > answer.md)
```

- `learner-view` renders the task as the task screen receives it: title, difficulty, context,
  brief, the material with its overview, the cards to pick from — **name and summary only, and the
  shortlist for an easy task** — and the whole treatment tree. It never prints the key, the notes,
  the lesson, a weight or a card's sections; a test holds it to that.
- `--tools ""` leaves the session with no tools, so it cannot read the repository even by
  accident; `--safe-mode` and `--strict-mcp-config` keep out project and user instructions, skills
  and MCP servers; running from the empty directory keeps the repository out of its context.
- Read `$smoke/view.md` yourself before sending it: it must hold nothing from the list above.
- `smoke-prompt.md` is fixed on purpose — edit it in the repository, not per run — so every task is
  tested against the same instructions.

**If `claude` cannot run here**, stop and tell the maintainer. Do not substitute a subagent of this
session: it shares the repository and could read the key.

## 7. Compare and report

Parse `answer.md` and compare it with the key, card by card. Report every difference, and for each
one say what it most likely means and propose one action:

| The smoke answer… | Most likely | Proposed action |
| --- | --- | --- |
| picked a card **not in the key** | an unintended problem is in the code — or the summary of that card reads onto this code | look at its evidence line: remove the problem from the material, or add the finding to the key |
| **missed** a card in the key | the planted problem is too faint, or hidden behind another | make it plainer in the code, or check the card's summary recognises it |
| found a card with **no matching leaf** | the context does not carry the proportion the key depends on — or the key is too narrow | say the missing fact in the context, or add the leaf the answer defended |
| found a card with **extra leaves** as well | usually harmless; worth a look when the extra leaf is defensible | add the leaf to the key if it is right here |
| listed something **outside the list** | a problem no offered card names | remove it from the material, or note it for a card that does not exist yet |

When the answer matches the key exactly, say so — that is a result, not an absence of one. One run
is the minimum; if an answer looks like chance, run a fresh session again and report both.

Then hand over, per task: the slug, the key, the smoke answer, the table of differences with the
proposed actions, and the validation result. **Apply nothing from the report yourself unless the
maintainer asks** — which difference is the code's fault and which is the key's is their call
(SPEC §7.1). After a change to the material or the key, run the smoke test again with a new session.

Do not commit on the maintainer's behalf unless they asked you to.

## Never

- Never let the smoke session see the key, the notes, the lesson or a full card.
- Never write a hint comment into a material.
- Never invent a card or a leaf in a key; a problem without a card waits for `author-card`.
- Never overwrite an existing material or task. A new context over an existing material is a new
  task directory; a changed material is the maintainer's decision, because every task over it
  changes with it.
