# Ritocode — Product Specification

- **Status:** Draft for the maintainer's review
- **Date:** 2026-09-30
- **Reads with:** [`CONCEPT.md`](CONCEPT.md) says what Ritocode is and why. This file says what it
  does, screen by screen and rule by rule. [`CONTENT_FORMAT.md`](CONTENT_FORMAT.md) defines the files
  a card, a material and a task are written in.
- **Decided in:** a question-and-answer session with the maintainer on 2026-09-30. Every product
  decision below is traceable to an answer in that session; the log is in
  [Appendix A](#appendix-a--decision-log). Where this file had to choose without an answer, it
  says so and marks the choice as a **default** the maintainer can overturn in review.

This file replaces `MVP_SCOPE.md`, `PROBLEM_PACKAGE_SPEC.md` and `EVALUATION_PIPELINE.md` as the
description of what gets built. Those files, and the code that implements them, are removed as the
first stage of the new plan (§11).

---

## 1. Glossary

The same words are used in the product, in these documents and in code. Russian terms are the ones
the interface shows.

| Term | Russian | Meaning |
| --- | --- | --- |
| **Problem** | проблема | Something that goes wrong in code an agent wrote — a god class, a secret in the repository, money in a float. |
| **Problem card** | карточка проблемы | The structured description of one problem: signs, cost, when it is acceptable, how it is treated. The atom of content. |
| **Problem catalogue** | каталог проблем | All problem cards. Public. Also the list a learner picks from in step 1. |
| **Class** | класс | One of six groups of problems, organised by cause (§3.1). |
| **Material** | материал | A small Python project an agent is supposed to have written. Shared by one or more tasks. |
| **Context** | контекст | What the project is for: users, data, lifetime, who maintains it, what happens if it breaks. |
| **Brief** | задание агенту | The instruction the agent was given, in full, including what it failed to say. |
| **Task** | задача | One material + one context + one brief + one answer key. The unit of practice. |
| **Task catalogue** | каталог задач | All published tasks. |
| **Answer key** | правильный ответ | The task author's list of findings: which cards are present in this material, and which treatments are right for each in this context. |
| **Finding** | находка | One entry of the answer key: a card and the treatments right for it here. |
| **Treatment** | лечение | What a problem is treated with. A two-level tree of **branches** and **leaves** (§3.3). |
| **Attempt** | попытка | One learner's answer to one task, and its result. |
| **Signal** | сигнал | A learner's claim that a card they picked is really present although the answer key does not list it. Goes to the author. |
| **Weight** | вес | How much a card matters, 1–3, set on the card. Secrets outweigh magic numbers everywhere. |
| **Incident** | инцидент | A second task type, outside the MVP: a whole production application investigated through a fixed pool of systems. Its terms are in §14.1. |

In C#, a task is `DiagnosisTask`, so that it never collides with `System.Threading.Tasks.Task`.

---

## 2. Audience and what "launched" means

### 2.1 Who it is for

**Junior developers who build with AI agents** — people who can read the code an agent wrote but
rarely read all of it, and know little about architecture
([ADR 0015](adr/0015-arena-home-and-junior-audience.md); until 2026-10-28, vibe coders). The
content therefore starts from the basics of design: problem cards are written for someone who has
never heard the word "coupling", and easy tasks are short enough to finish in five minutes.

The product teaches them to look at what an agent produced, name what is wrong, and know what it is
treated with — a better brief, a rule in the repository, an automatic check, a fix by hand, or
nothing, because in this context it is fine.

### 2.2 The MVP

The MVP is **launched** when both hold:

1. All 20 tasks can be solved end to end at a **public address on the project's own domain**, by
   anyone who signs in.
2. The maintainer and two acquaintances have solved them and found them useful.

There is no deadline; the MVP ships when it is ready. It is **free**: no billing, no plans, no paid
content, and nothing in the design has to anticipate them.

---

## 3. Content model

### 3.1 The six classes

Problems are grouped by **cause**, because the cause decides the treatment.

| Class | Looks like | Cause |
| --- | --- | --- |
| **Disproportion** | A full service for two users; layers with one implementation. Or the reverse: everything in memory, no pagination | The brief never said how many users, how much data, how long it lives |
| **No knowledge of the project** | Its own retry mechanism, its own error format, a dependency for what already exists | No project rules given; the task handed over in one large piece |
| **Hygiene and security** | Secrets in the repository, hardcoded addresses, an empty `except`, a lost ownership check | Default behaviour when no fence exists |
| **Growth without restructuring** | God classes, duplication between neighbouring files, dead code | The agent appends and almost never rebuilds |
| **False confidence** | Tests that mock what they test, documentation that describes something else, a silently dropped requirement | The model optimises for visible success |
| **Domain** | Money in floating point, time without a zone, invariants any caller can break | The model does not know the domain and was not told its rules |

**Class 2 — no knowledge of the project — has cards in the catalogue but no tasks in the MVP.** It
can only be diagnosed on material that contains the existing project the agent ignored, and the MVP
has only new projects (§3.4). Its cards still appear in step 1, where they are simply never right.

### 3.2 The problem card

A card describes one problem. Its fields:

| Field | Required | Shown where |
| --- | --- | --- |
| `slug` — stable English identifier, also the anchor on the catalogue page | yes | everywhere |
| `class` — one of the six | yes | everywhere |
| `weight` — 1, 2 or 3 | yes | nowhere; used by scoring |
| **Name** | yes | everywhere |
| **Summary** — one line: what it is and how it is recognised | yes | everywhere |
| **Keywords** — extra words search should match ("пароль", "токен" for secrets) | no | never shown; used by search |
| **Signs** — what is visible in the code | yes | catalogue page, review |
| **Why AI does it** — the mechanism that produces it | no | catalogue page, review |
| **Cost** — what becomes more expensive, including for the next agent run | yes | catalogue page, review |
| **When it is acceptable** — the conditions under which it is fine; may say "never" | yes | catalogue page, review |
| **How to detect it automatically** | no | catalogue page, review |
| **How it is treated** — and what the wrong treatment looks like | yes | catalogue page, review |
| **Sources** — Fowler, Beck, Martin, OWASP, Sonar rules, research | no | catalogue page, review |
| **Counter-arguments** — where the industry disagrees; plain text, no links to other cards | no | catalogue page, review |

Rules:

- **During a task, only the name and the summary are visible.** Every other field is absent from the
  task screen and from the data the task screen receives — not hidden by CSS, not sent. A learner
  can still open the public catalogue in another tab; the product does not try to prevent that. It
  is a trainer, and whoever looks it up cheats only themselves.
- **Weight belongs to the card, not to the finding.** A task author never sets it.
- **Cards carry no relations** — no "opposite of", "includes" or "often confused with".
- **Neighbouring cards delimit each other in their summaries.** Where two cards could describe the
  same line — "hardcoded configuration" and "secrets in the repository" — each summary says what it
  is not ("addresses, paths and parameters; credentials are *Secrets in the repository*"). When a
  line genuinely shows both, the task lists both as findings.
- **Every problem belongs in the catalogue**, including problems no task uses yet. Unused cards are
  what makes step 1 a search rather than a recognition exercise.
- **The catalogue is not only classic code smells.** It must include problems characteristic of
  agents, at least: an invented API or package; a parallel `v2` file beside the original; a test
  weakened until it passes; a stub returning fake data behind a TODO; comments narrating the change
  ("now uses X"); defensive code for states that cannot happen; compatibility shims and flags nobody
  asked for; inconsistent style between files written in different runs; unrelated code changed
  outside the task.

**Size at launch: 55–60 cards, 8–10 per class.** The reasoning: 20 tasks averaging 3–4 findings
give 60–80 findings; for a card to teach, it must appear at least twice, so tasks use roughly 25–35
distinct cards. With no traps, unused cards are the only distractors, and for step 1 to be a search
the used cards should be well under half the catalogue — hence 50–70. Above about 80, scanning the
list becomes a chore on a phone. At 20–30 minutes per card with an AI draft, 60 cards are 20–30
hours of the maintainer's time.

### 3.3 The treatment tree

Treatments form a fixed two-level tree. The first level — the **branch** — is *where the measure
lives*. The second — the **leaf** — is *what the measure is*. The tree is product structure: it
changes rarely, lives in one content file, and is shown **whole** in step 2 for every card.

| Branch | Leaves |
| --- | --- |
| **`brief` — Tell the agent in the task** (Постановка задачи агенту) | `scale` — how many users and how much data · `lifetime` — how long it lives and who maintains it · `scope` — what not to touch, what not to add · `requirements` — required properties: security, precision, compatibility · `reuse` — use what the project already has |
| **`rule` — A rule in the repository** the agent reads on every task: AGENTS.md, CLAUDE.md, editor rules (Правило в репозитории) | `structure` — structure and layer boundaries · `limits` — size and complexity limits · `conventions` — required mechanisms: errors, logging, configuration, secrets · `forbidden` — banned APIs and dependencies · `tests` — testing requirements |
| **`auto` — An automatic check** (Автоматическая проверка) | `linter` — linter or static analyser · `types` — type checker · `secrets` — secret scanner · `metrics` — size and complexity gate in CI · `dependencies` — dependency check · `architecture` — architecture test · `test-quality` — coverage or mutation check |
| **`manual` — Fix by hand now** (Исправить руками сейчас) | `split` — split by responsibility · `dedupe` — remove duplication · `extract-config` — move values to configuration · `handle-errors` — handle errors explicitly · `validate` — check inputs and access · `representation` — fix how data is represented: money, time, identifiers · `invariant` — protect an invariant · `remove` — delete the excess: dead code, a layer, a dependency · `tests` — write or fix tests |
| **`accept` — Leave it, deliberately** (Оставить осознанно) | `fits-context` — acceptable in this context · `not-worth-it` — the fix costs more than the problem · `debt` — record as debt with a condition to revisit |

A leaf is addressed as `branch.leaf` — `auto.secrets`, `accept.fits-context`.

- **The `accept` branch is mandatory.** Without it the product teaches nitpicking rather than
  proportion.
- Treatments are **stack-independent**: "a linter", not "ruff rule S105".
- The same *what* can legitimately live in two places — a size limit written as a rule and enforced
  as a CI gate. The tree does not pretend these are one thing; the answer key handles it by listing
  both leaves where both are right (§5.2). Declaring such pairs equivalent at the tree level, so an
  author need not list both, is a later iteration (§12).

### 3.4 Material

Material is the code the learner reads.

- **Python only in the MVP.** Other stacks, and AI translation of a task into the learner's stack,
  are later iterations.
- **New projects only.** The agent built the whole small project. Material that shows an existing
  project and the agent's change on top of it is a later iteration, and with it class 2.
- **Generated to order and simulated by default.** The maintainer, with an AI assistant, produces
  the code the task needs (§7). Nothing is labelled "simulated"; everything is, unless a later
  iteration labels real-project material as such.
- **Written in English** — identifiers, comments, string literals. Only the context, the brief and
  the cards are localised, so an English launch translates text, not code.
- **No hint comments.** Nothing like `# hardcoded for simplicity` or `# TODO: validate`. A problem
  is visible in what the code does, not in what it says about itself.
- **Lines of at most 79 characters**, so code reads on a phone with little horizontal scrolling.
- **One material serves one to four tasks.** Tasks over the same material differ in context, so
  their answer keys differ.

**Size follows difficulty.** Estimates assume a junior developer reads simple Python for understanding
at 20–30 lines a minute — slower than skimming, faster than line-by-line review (7–8 lines a minute).
They are **estimates to be calibrated** from recorded attempt times (§8).

| Difficulty | Reading | Total lines, at most | Files | Whole task |
| --- | --- | --- | --- | --- |
| Easy | ~2 min | 80 | 1–2 | ~5 min |
| Medium | 4–6 min | 300 | up to 6 | 10–15 min |
| Hard | 8–10 min | 600 | up to 12 | 20–25 min |

A hard task may exceed its band when a file's **size is itself the sign** — a god class shown as a
file of 1 800 lines that nobody is expected to read. Content validation warns on an exceeded band
rather than failing, and the author decides.

### 3.5 The task

A task is a **diagnosis, not a repair**. It carries:

- **Title.**
- **Difficulty** — easy, medium or hard. Set by the author.
- **Material** — a reference to one material.
- **Context** — free prose, optionally a short list of facts. Visible from the start of the task.
- **Brief** — the text the agent was given.
- **Answer key** — zero to about ten findings. Each finding is a card and **one or more leaves**,
  plus an optional note shown in the review ("in a quarter-long utility this costs nothing").
- **Lesson** — optional closing text shown in the review.

Rules:

- **Tasks vary from zero findings to about ten.** Clean tasks — nothing wrong, or everything
  acceptable in this context — exist on purpose, so no learner concludes that "there are always
  seven".
- **A card whose name is a verdict relative to context** — over-engineering, under-engineering — is
  in the key only when the context makes it a problem. In a context that justifies the heavy design,
  it is not a finding, and picking it costs what any extra pick costs.
- **Tasks over the same material are independent tasks.** No order is imposed. After solving one,
  the review offers the others as "the same code in another context".
- **A finding is a card and its treatments; it has no location in the MVP.** The answer key still
  stores each finding as an object rather than a bare card identifier, so that a location — file
  and lines — can be added later as an optional field without changing what a finding is.

### 3.6 Localisation

Localisation is built in from the first commit; only the Russian content is written in the MVP.

- Content files keep language-neutral data (slugs, classes, weights, answer keys, leaf identifiers)
  apart from localised text, one text file per locale. Adding English means adding `en` files, not
  editing the existing ones. See [`CONTENT_FORMAT.md`](CONTENT_FORMAT.md).
- Interface strings live in a translation catalogue from the start; none are written into
  components.
- Pages declare their language (`lang="ru"`). Dates and numbers are formatted through `Intl`.
- The URL scheme for a second locale is chosen when that locale ships; nothing in the MVP assumes
  there will only ever be one.

---

## 4. Learner experience

### 4.1 Pages

| Route | Page | Access |
| --- | --- | --- |
| `/` | Home, the arena: what Ritocode teaches, the daily task, tasks and problems to start from, progress (below) | public |
| `/problems` | Problem catalogue, one page, every card in full, anchor per card (`/problems#secrets-in-repo`) | public |
| `/tasks` | Task catalogue | public |
| `/tasks/{slug}` | Solving a task | public to solve; sign-in to check |
| `/tasks/{slug}/attempts/{id}` | Review of an attempt | owner only |
| `/progress` | Progress | signed in |
| `/privacy` | Privacy policy | public |
| `/admin/...` | Admin (§6) | admins only |

`/` and `/problems` must be **readable by search engines without running JavaScript** — including
Yandex, which matters for a Russian-language audience. They are prerendered to static HTML at build
time from the content export; the single-page application takes over once loaded. This revisits
ADR 0001's "revisit if catalog SEO becomes a goal" without adding a server-rendering layer.

**The home page is an arena** ([ADR 0015](adr/0015-arena-home-and-junior-audience.md),
[DESIGN.md](DESIGN.md) › The arena; decided by the maintainer on 2026-10-28 in
[#190](https://github.com/shoraLBRT/ritocode/issues/190)):

- **The lead** says what the product teaches — to see the problems in code an agent wrote — with the
  real counts of tasks, problems and classes. Its wording is still to be worked out; until then it is
  a placeholder on that idea.
- **The daily task.** One task a day, changing at midnight Moscow time. For a signed-in learner it is
  chosen among the tasks they have not solved; for a guest, and for a learner who has solved them
  all, among all tasks. The choice is a pure function of the date and the list, so everyone in the
  same state sees the same task and the prerendered page is stable. The card counts down to the
  next one («До новой задачи 05:12:40») and shows it when the count reaches zero; its button names the
  whole task, «Решить задачу», not its first step.
- **A starting point, not a copy of the catalogues** ([#200](https://github.com/shoraLBRT/ritocode/issues/200)):
  - **Tasks for you** — six tasks in the table of §4.3 (status, title, expected time, difficulty),
    the learner's open ones first (new or tried), then those whose answer is revealed, then solved
    ones; for a guest a pick of all tasks. The daily task is left out, and is chosen among the open
    ones. No class tags and no difficulty filter — that is `/tasks`, a link away.
  - **Problems to read** — four cards of the catalogue: class, name and summary, each opening at its
    anchor in `/problems`, and a link to the whole catalogue.
  - Both are picked in an order fixed by the Moscow day, like the daily task: the same for everyone
    on the same day, so the prerendered page and the application agree.
- **Progress** for a signed-in learner: found out of met, by class (§4.7), the **streak** and
  the **activity** of the last twelve weeks. A guest sees a sign-in prompt there.
- **Without JavaScript** the prerendered page carries the lead, the catalogue's counts and the
  problems to read; the content export has no tasks, so the daily task and the suggested tasks are
  filled in by the application.

### 4.2 Problem catalogue

One page. Cards are grouped by the six classes. A search box filters by name, summary and keywords,
and a class filter shows one class or all. A card is shown **folded** — its class, name and summary,
like a playing card — and **opens** at the centre of the screen with every field of §3.2; the
maintainer chose this on 2026-11-10 ([#202](https://github.com/shoraLBRT/ritocode/issues/202)).
Every card has an anchor: `/problems#slug` opens that card, so a card can be linked to without a page
of its own. Without JavaScript the prerendered page carries every card in full, and a card unfolds in
place.

### 4.3 Task catalogue

A list of tasks: title, difficulty, an expected time derived from difficulty, and — for a signed-in
learner — its **status** ([ADR 0017](adr/0017-a-result-without-the-answer.md), #229):

| Status | When | In the table |
| --- | --- | --- |
| New | no submitted attempt | an empty ring, as before |
| Tried | submitted attempts, none solving it, the answer not revealed — whatever the score | its own mark |
| Solved | an attempt solved it before the answer was revealed, if it ever was | a check |
| Gave up («ответ открыт») | the answer was revealed before any attempt solved it | an eye |

An attempt **solves** a task when every finding of the key is found, each with at least one of the
key's leaves, and nothing extra is picked; on a clean task, when nothing is picked. A task solved and
revealed afterwards stays solved; one revealed first stays gave up, whatever later practice scores.
Filters: difficulty, and the four statuses. **No class tags** on tasks: they would give the answer
away. Ordered by difficulty, then by publication. Signed out, there is no status.

### 4.4 Solving a task

The screen is a workspace ([DESIGN.md](DESIGN.md), [ADR 0014](adr/0014-visual-identity-and-task-workspace.md)):
the three steps in the header; a sidebar with the **context** — the brief always shown, the rest of
the context on expanding — and **the answer**, the step at hand; the **material** beside it. On a
phone they become three tabs — Context, Code, Answer.

**Material viewer.** The open file's path is a button that opens the file tree, with a line count
per file and an overview generated at ingest — total lines and file count; buttons lead to the
previous and the next file. Syntax highlighting and line numbers; read-only. The dependencies the
project declares are part of the overview the API serves, but the screen does not show them.

**Step 1 — Diagnosis: what do you see?** The learner picks cards from the catalogue — only name and
summary are shown. **Search comes first**: they describe in their own words what they see, and the
cards that share a word appear as they type, ranked, the matched words marked; class filters narrow
them. Browsing the six groups stays possible, folded and deliberately quiet. The question is "what do you see
here that should be named", not "what is wrong": a god class that is fine in this context is still
a god class, and whether to leave it is step 2's decision.

- **Medium and hard tasks** offer the entire catalogue.
- **Easy tasks** offer a shortlist: the cards in the answer key plus 15–20 others drawn from the
  whole catalogue. The shortlist is computed at ingest, deterministically from the task's slug, so
  every learner sees the same list. The author does nothing to produce it.
- Picking nothing is a valid answer. On a clean task it is the right one.

**Step 2 — Treatment: what do you do with it here?** For each picked card in turn — the picked
cards listed with their state — the whole treatment tree, searched the way the cards are or opened
branch by branch; each leaf says where its measure lives. The learner ticks one or more leaves in
one or more branches. Every picked card needs
at least one leaf before the answer can be checked. The learner can go back to step 1 at any time.

**Check.** The answer is submitted and scored (§5), and the review opens.

### 4.5 Review

The review opens in the task's workspace with the third step current. It has two states
([ADR 0017](adr/0017-a-result-without-the-answer.md), #229).

**Until the learner reveals the answer** it is the outcome only, so that the next attempt is still
diagnosing, not copying:

- the **score** in points and whether the task is solved (§4.3);
- the **counts**, drawn as small diagrams (DESIGN.md › The review): findings found out of present,
  found ones treated right, findings not found, extra picks;
- *Try again*, first, *Next task* beside it, and the code to read again.

It never says which pick was right or extra, which card was missed, or which leaves the key holds;
the lesson, the author's notes and the same code in another context wait too. The API does not send
them until the reveal. **Revealing is a deliberate action** — *Показать ответ*, behind a short
confirmation that says what it does to the task's status (gave up, unless already solved). The
reveal is kept for good, for every attempt at the task; later attempts remain possible as practice.

**Once revealed**, the full review: the score and every line in the sidebar, the chosen line in full
beside it — or the code again.

- **Score** in points, with its composition beneath: findings found out of findings present, extra
  picks, treatments that matched.
- **Every finding in the key**, marked found or missed. For each: the learner's leaves beside the
  author's, the author's note if there is one, and the full card, expandable.
- **Every extra pick**, marked "not in this task's answer", with the full card and a button —
  *"I'm sure it is here"* — that opens a one-line comment and sends a signal (§4.8).
- **The lesson**, if the task has one.
- **The same code in another context** — links to the other tasks over this material.
- **Try again** — a new attempt, marked as practice (§5.4).
- **Next task** — the next task in the order of the task list (§4.3) that the learner has not
  solved, carrying on from the start past the end; none when every other task is solved (#199,
  DESIGN.md › The review).

On a clean task with nothing picked, the review says so plainly: there was nothing to find, and the
learner found nothing.

### 4.6 Signed out, then signed in

Anyone can open a task and work through both steps without an account. **Checking requires signing
in.** The flow:

1. The learner presses *Check* while signed out. Their answer is saved in the browser, keyed by the
   task.
2. A sign-in prompt offers GitHub and Google.
3. After the provider returns, the learner lands **on the same task**, not on the home page. The
   saved answer is restored and submitted automatically, and the review opens.

The return address travels through the sign-in round trip and is accepted only as a local path, so
it cannot be used to redirect anywhere else. If the browser could not keep the answer, the learner
sees the task restored to its start and a short note; they do not lose anything else.

### 4.7 Progress

Built from **first attempts only** (§5.4):

- **Per class** — how many findings of that class were found out of those met, and how many found
  ones were treated right.
- **Per card** — met, found, missed, picked when absent, treated right — only from the tasks whose
  answer the learner has revealed (§4.5,
  [ADR 0017](adr/0017-a-result-without-the-answer.md)). Before the reveal a task's first attempt
  counts in its classes and in the number of tasks, but names none of its cards; the page says how
  many tasks wait for their answer to be revealed. The reveal changes no score: the first attempt
  stays the one scored.

Two counts of practice, from **every** attempt, not only first ones:

- **Activity** — the number of attempts submitted on each day, Moscow time.
- **Streak** — how many days in a row, up to today, have at least one submitted attempt. A day
  without one breaks it; today still counts as kept until it ends.

No XP, no levels, no leaderboard.

### 4.8 Signals

A signal is a learner saying "this card is really here, the answer key missed it". It is sent only
once the answer to the task is revealed (§4.5): before that no extra pick is named. It records the
attempt, the task, the card, the learner and an optional comment of up to 500 characters. It changes
nothing about the attempt's score. It reaches the admin's signal list (§6), where the author decides
whether to update the task.

### 4.9 What the learner takes away

In the MVP: **the score and the review.** Two things are recorded for later (§12): a block of "what
to tell the agent next time" on the review, assembled from the cards; and a personal memo that
accumulates across tasks and can be copied in one go.

---

## 5. Scoring

### 5.1 Principles

- **Deterministic.** The same answer to the same task version gives the same result. Scoring is a
  pure function of the answer, the answer key, the card weights and the scoring parameters.
- **No traps.** An author never marks a card as a deliberate wrong option. Any card not in the key
  is simply extra.
- **Tolerant of alternatives.** When the author lists several leaves for a finding, any one of them
  is right.
- **Numbers are parameters.** The values below are defaults in configuration, to be tuned once real
  attempts exist. Tuning them does not rewrite past results (§5.4).

### 5.2 The rules

With *w* the card's weight:

| Event | Points |
| --- | --- |
| A card in the key is picked | **+10 · w** |
| A card in the key is not picked | **−3 · w** |
| A card not in the key is picked | **−3**, flat |
| For a found card, at least one picked leaf is among the key's leaves | **+5 · w** |
| For a found card, each picked leaf that is not among the key's leaves | **−2** |

- Leaves picked for an extra card are not scored; there is no key to compare them with.
- The total is floored at zero.
- The maximum is the sum of **15 · w** over the findings in the key.
- The result is shown in **points** — "38 of 45" — never as a percentage: a clean task has a maximum
  of zero, and a percentage of zero means nothing. On a clean task the result is the penalties for
  extra picks, and the review states the outcome in words.

**Why extra picks cost something.** With no traps and no cost, ticking the whole catalogue would
collect every +10 and avoid every miss. A small flat cost makes that unprofitable while keeping an
honest extra finding cheap — and the signal button turns such a finding into information for the
author.

**Why "any of" and not "all of".** Several leaves in a key can mean alternatives ("a CI gate or a
written limit") or a combination ("split it *and* add a limit"). Scoring cannot tell which the author
meant. "Any of" never punishes a learner who chose a valid alternative; the review shows the full
list, so the combination still gets taught.

### 5.3 Worked example

A medium task has three findings: secrets (weight 3, key: `auto.secrets`), a god class (weight 2,
key: `accept.fits-context`) and money in a float (weight 3, key: `manual.representation`). Maximum:
45 + 30 + 45 = **120**.

The learner picks secrets with `auto.secrets` and `manual.extract-config`, the god class with
`manual.split`, and magic numbers, which the key does not list. They miss the float.

- Secrets: +30 found, +15 a leaf matched, −2 an extra leaf → **+43**
- God class: +20 found, no leaf matched, −2 an extra leaf → **+18**
- Money in a float: missed → **−9**
- Magic numbers: extra → **−3**

Total **49 of 120**. The review shows the god class was right to see and wrong to split here, and
that the float was the costliest miss.

### 5.4 Attempts and history

- **The first submitted attempt at a task counts toward progress.** Later attempts are practice:
  scored and reviewed the same way, marked as practice, and excluded from progress.
- An attempt stores the learner's answer, the result broken down line by line, and the content
  revision it was scored against. **A later change to the task, a card or the scoring parameters
  never rewrites a stored result.** Re-scoring, if ever needed, is an explicit operation.
- **Revealing the answer** (§4.5) is the learner's, per task, not an attempt's: it is kept beside
  the attempts with its time, once, and changes none of them. Whether an attempt solved the task and
  the task's status (§4.3) are read from the stored results and that time. A reveal changes nothing
  in progress (§4.7): the first attempt counts as it was scored.

---

## 6. Accounts and administration

### 6.1 Sign-in

- **GitHub and Google**, through OAuth. No passwords.
- **One user per verified e-mail address.** Signing in with the second provider under the same
  verified address reaches the same account; an address the provider does not mark as verified is
  never used to link.
- A session is a secure, HTTP-only cookie. State-changing requests are protected against cross-site
  request forgery.
- Signing in shows a notice linking the privacy policy (§10.4).

### 6.2 Admin

Admins are named in configuration by e-mail; there is no role management screen. The admin area has
two parts in the MVP.

**Signals.** Every signal, newest first: task, card, learner, comment, date. Filter: open or
resolved. One action: mark resolved. The author changes the task in the content repository, not
here.

**Users and attempts.**

- **Users:** e-mail, provider, date registered, attempts made, tasks solved.
- **Attempts:** learner, task, first or practice, started, submitted, time taken, score, and the
  step reached for attempts that were never submitted — which is "where they gave up".

Adding or editing tasks through the admin area is a later iteration (§12). In the MVP, content
reaches the product only through the repository (§7).

---

## 7. Authoring in the MVP

Only the maintainer writes content in the MVP. Content is files in `content/` in this repository,
under its own licence ([`CONTENT_FORMAT.md`](CONTENT_FORMAT.md)), reviewed through pull requests
like code and loaded into the database on deployment.

### 7.1 Two Claude Code skills

Authoring is assisted by two skills in `.claude/skills/`. Both **read the current catalogue and the
current treatment tree from `content/` on every run**, so they never work from a stale list.

**`author-card`** drafts a problem card from a name or an idea: every field, in Russian, in the file
format, with the summary written to delimit it from its nearest neighbours in the existing
catalogue. The maintainer edits and commits.

**`author-task`** turns the maintainer's idea — what the project is, the difficulty, the problems it
should contain, one or more contexts — into files:

1. writes the material in Python within the size band, following §3.4 (English, no hint comments,
   79-character lines);
2. writes one task per context: context, brief, answer key using existing card slugs and leaf
   identifiers only, optional notes and lesson;
3. runs content validation (§7.2);
4. runs the **blind smoke test**: a separate AI session receives exactly what a learner would —
   context, brief, material, card names and summaries, the treatment tree — and answers the task.
   Its answer is compared with the key, and every difference is reported to the author;
5. hands the result to the maintainer, who either adds a finding the smoke test uncovered to the
   key or removes the unintended problem from the code, and then opens a pull request.

The smoke test exists because code written to order with planted problems tends to plant extra ones
nobody intended — and under §5.2 a learner who finds one of those is charged for it.

### 7.2 Content validation

One command, run by the skills, by CI on every pull request, and by ingest before it writes
anything. It reports every fault at once:

- **Errors:** a slug that is missing, duplicated or malformed; a finding that names a card or a leaf
  that does not exist; a task pointing at a missing material; a required field missing in the
  default locale; a card removed while a published task still uses it.
- **Warnings:** material outside its difficulty's size band; lines longer than 79 characters; an
  easy task whose shortlist could not reach the target size.

### 7.3 Ingest

On deployment, ingest loads `content/` into the database in one transaction: cards, the treatment
tree, materials, tasks and their shortlists, stamped with the commit it came from. It upserts by
slug. A task removed from the repository is **unpublished**, never deleted, because attempts refer
to it. A card removed from the repository is **retired** — hidden from the catalogue — for the same
reason.

---

## 8. Measurement

Two sources, for two different questions.

**Internal journal — how signed-in learners use tasks.** Kept in the product's own database, as part
of attempts: when a task was opened, which step was reached, when it was submitted, how long it took.
This is what calibrates the size bands (§3.4) and the scoring parameters (§5), and what the admin
sees (§6.2).

**Umami — how anyone uses the site, signed in or not.** Self-hosted on the same server, cookie-less,
so no consent banner is needed and no data leaves the server. It records page views, referrers and
these events: task opened, step 2 reached, check pressed while signed out, sign-in completed, attempt
submitted, signal sent, catalogue search used. The events answer where signed-out visitors drop off,
which the internal journal cannot see.

---

## 9. Architecture

The engineering rules in `AGENTS.md` and ADRs 0001–0004, 0007 and 0008 continue to hold: the
modular monolith, modules that never reference each other, the API conventions, migrations for every
schema change, ownership checked inside the query, the unified error body.

### 9.1 Modules

| Module | Owns | Replaces |
| --- | --- | --- |
| **Auth** | OAuth sign-in with GitHub and Google, sessions, linked accounts | Auth, extended |
| **Users** | Users; who is an admin comes from configuration | Users, trimmed: `xp` and `trust_level` go |
| **Content** | Cards, the treatment tree, materials, tasks, answer keys, shortlists; ingest and validation; the catalogue read APIs | Problems |
| **Attempts** | Attempts, scoring, progress, signals | Submissions and Progress |

Workspaces and Evaluations are removed (§11). Content is written only by ingest and read by
everything else. Attempts reaches the answer key and the card weights through one contract that
Content answers, per ADR 0007.

### 9.2 Data

- **PostgreSQL only.** Material is small text — at most a few hundred lines per task — and is stored
  in the database. **Object storage is removed** along with MinIO, the storage client and the
  storage layout, because nothing left in the product needs it. One fewer service to run on one
  server. *(Default — see §13.)*
- Localised content text is stored per locale in the content tables, so a second locale is data,
  not a migration.
- An attempt stores its answer and its result as structured JSON beside its columns, and the content
  revision it was scored against.

### 9.3 API

Under `/api/v1`, following ADR 0003.

| Method and path | Purpose | Access |
| --- | --- | --- |
| `GET /problems` | The whole problem catalogue, full cards | public |
| `GET /treatments` | The treatment tree | public |
| `GET /tasks` | The task catalogue; each task's status (`new`, `tried`, `solved`, `gaveUp`) when signed in | public |
| `GET /tasks/{slug}` | A task to solve: context, brief, material, the card list to pick from with name and summary only | public |
| `POST /attempts` | Start an attempt at a task | signed in |
| `PATCH /attempts/{id}` | Record the step reached | owner |
| `POST /attempts/{id}/submit` | Submit the answer; returns the outcome — score and counts — and the key only if the answer is already revealed | owner |
| `POST /attempts/{id}/reveal` | Reveal the answer to the task of a submitted attempt, for good; returns the attempt in full | owner |
| `GET /attempts/{id}` | An attempt, its outcome, and once the answer is revealed its result | owner |
| `GET /attempts` | The caller's attempts, optionally for one task | signed in |
| `GET /me`, `GET /me/progress` | The caller, and their progress | signed in |
| `POST /signals` | Send a signal from an attempt | owner of the attempt |
| `POST /feedback` | A message from the feedback widget becomes a GitHub issue (§9.6) | public |
| `GET /admin/signals`, `POST /admin/signals/{id}/resolve` | Signals | admin |
| `GET /admin/users`, `GET /admin/attempts` | Users and attempts | admin |

Sign-in lives outside the versioned API: `GET /auth/login/{provider}?returnUrl=`, the providers'
callbacks, and `POST /auth/logout`.

The answer key never leaves the server before the learner has submitted an attempt at the task and
revealed its answer, and afterwards only inside their attempts' results and their per-card progress.

Submission of attempts and signals is rate-limited per user, in the manner of the existing
submission limit.

### 9.4 Frontend

React, Vite and TypeScript, as today. The API client, error handling, layout and loading, error and
empty states from the existing shell survive. New: a translation catalogue for every string; the
build-time prerender of `/` and `/problems` (§4.1); a read-only code viewer with Python highlighting;
a layout that works at phone width.

### 9.5 Deployment

- **One VPS in Russia** (Timeweb Cloud, Selectel or similar). Russia's personal data law (152-FZ)
  requires personal data of Russian citizens — an e-mail from sign-in is one — to be stored on
  servers in Russia. Hosting there removes that risk.
- **Docker Compose** on that server: a reverse proxy with automatic TLS (Caddy), the API, the static
  frontend, PostgreSQL, Umami.
- **The project's own domain.**
- **A release** builds images in CI, then on the server applies migrations, runs content ingest, and
  restarts the API — one scripted command.
- **Backups:** a daily database dump, seven days kept, one copy off the server.

### 9.6 Feedback

A widget in the bottom corner of every page ([#243](https://github.com/shoraLBRT/ritocode/issues/243),
[ADR 0019](adr/0019-feedback-as-github-issues.md); its look in DESIGN.md › Feedback): a heading, a
textarea, *Отправить*. No sign-in. `POST /api/v1/feedback` with `{ message, page, captchaToken }`
opens an issue in this repository with the `feedback` label — the message, the page path, and the
user id when signed in — and answers `201 { number }`. Refusals, each explained in a word on the form
with the text kept:

| Status | `code` | When |
| --- | --- | --- |
| 400 | `feedback_empty` | the message is empty or whitespace |
| 400 | `feedback_too_long` | longer than `Feedback:MaxLength` (4000) |
| 429 | `feedback_rate_limited` | more than `Feedback:MaxPerWindow` (5) from one address in `Feedback:Window` (1 hour) |
| 403 | `feedback_not_verified` | the Turnstile captcha failed, when `Feedback:TurnstileSecretKey` is set |
| 503 | `feedback_not_configured` | no GitHub token or repository on the server |
| 503 | `feedback_unreachable` | GitHub could not be reached |
| 500 | `feedback_failed` | GitHub refused the issue |

---

## 10. Non-functional requirements

### 10.1 Security

User code never runs anywhere: diagnosis executes nothing the learner writes. OAuth state and PKCE
where the provider supports it; the sign-in return address accepted only as a local path; cookies
secure and HTTP-only; CSRF protection on state-changing requests; ownership checked inside every
query that reads an attempt or a signal; admin endpoints behind a policy.

### 10.2 Performance

A task, material included, is a single response of tens of kilobytes. No caching layer in the MVP.

### 10.3 Accessibility

Keyboard use throughout steps 1 and 2; visible focus; colour never the only carrier of meaning in
the review (found, missed and extra each have a mark and a word).

### 10.4 Legal

A privacy policy page, and a sign-in notice linking it. **Not engineering work, and listed so it is
not forgotten:** the text of the policy, and whether the maintainer must notify Roskomnadzor as a
personal data operator, are the maintainer's to check.

---

## 11. What happens to the existing code and documents

The platform was built for the previous product. Before any of it is removed, the last commit of the
previous product is tagged `pre-diagnosis`, so everything below stays recoverable.

**Survives:** the solution and module layout, the architecture tests, the API conventions and error
handling, persistence and migrations, the test harness for PostgreSQL, the identity seam, the
frontend shell and its API client, CI.

**Reshaped:** Problems becomes Content — ingest and the catalogue survive, and what they carry
changes. Submissions becomes Attempts — attempt history survives, and the queue and frozen trees go.
Auth gains real sign-in. Users loses `xp` and `trust_level`.

**Removed:** the Workspaces module; the Evaluations module — validators, the pipeline, the sandbox
runner; the old problem package format and its C# packages in `content/problems`; object storage and
MinIO; `spikes/`; the frontend's old problem pages. Documents: `AGENTS_OVERVIEW.md`, `MVP_SCOPE.md`,
`SLICE_PLAN.md`, `PROBLEM_PACKAGE_SPEC.md`, `EVALUATION_PIPELINE.md`, `STORAGE_LAYOUT.md`,
`SCALING_PLAN.md`, and ADRs 0005, 0006 and 0009. `README.md`, `ARCHITECTURE.md`, `DOMAIN_MODEL.md`,
`DATABASE_SCHEMA.md` and `PROJECT_STATE.md` are rewritten for the new product.

**Backlog:** issues that describe only the previous product are closed as *not planned* with the
label `superseded` and a link to ADR 0010. Issues common to both — sign-in, CI, logging, the security
baseline — stay open and are re-scoped where needed. The operations are listed for the maintainer's
approval before they are carried out.

---

## 12. Later iterations

Recorded so they are not lost and not built early — as issues in the
[*Later* milestone](https://github.com/shoraLBRT/ritocode/milestone/11) since 2026-11-04: what the
learner takes away, locations, equivalent leaves, existing projects, authors' own code, other stacks,
authoring in the admin area, community authorship, English, difficulty beyond the shortlist — and,
since 2026-11-11, the incident: a boss task over a whole production stack, specified in §14
([ADR 0016](adr/0016-incident-boss-task.md), [#227](https://github.com/shoraLBRT/ritocode/issues/227)).

---

## 13. Open questions

What this file decided without the maintainer's answer, or could not decide at all.

1. **Removing object storage** (§9.2) is a default. It simplifies deployment and removes code, and
   nothing in the product needs it. If the maintainer expects large material or uploaded files
   soon, it stays.
2. **The scoring numbers** in §5.2 are starting values. The maintainer's first proposal for a miss
   was −2 or −5; −3 sits between. Tuning waits for real attempts.
3. **The size bands** in §3.4 are estimates of reading speed, not measurements.
4. **The shortlist size** for easy tasks is 15–20 extra cards on the maintainer's figure; with a
   key of zero to three cards, that is 15–23 cards in all.
5. **Personal data:** the policy text and the operator notification (§10.4).

---

## 14. The incident

**A second task type, outside the MVP** (§2.2): a long "boss" task in which the learner investigates
a whole production application — code, history, pipeline, infrastructure, database, metrics, logs
and frontend — and answers with the same cards and the same treatment tree as a task. The decision
and the alternatives weighed are [ADR 0016](adr/0016-incident-boss-task.md); the work is
[#227](https://github.com/shoraLBRT/ritocode/issues/227), in the *Later* milestone.

**Everything §3–§10 says about tasks holds for incidents unless this section says otherwise.** The
catalogue, the treatment tree, signals, sign-in, attempts and their history are shared. Where this
section had to choose without the maintainer's answer, it marks the choice as a **default** (§14.13).

### 14.1 Terms

| Term | Russian | Meaning |
| --- | --- | --- |
| **World** | мир | An application — backend, frontend, database, infrastructure, pipeline, history — authored once and shared by several incidents. |
| **Incident** | инцидент | One thing that went wrong in a world: a variant, a scenario, a recording, an alarm, an answer key, hints and a postmortem. The unit of practice, like a task. |
| **Variant** | изменение | What the incident changes against the world's base revision: lines of code, a database setting, an index, a YAML file. Never shown as such; it is in the recording. |
| **Scenario** | сценарий | The load and events the variant is run under when it is recorded. Not shown. |
| **Recording** | запись | What every system of the pool produced in one run of the variant under its scenario. Everything the learner investigates. |
| **System** | система | One part of the stack the learner can inspect — code, history, CI/CD, infrastructure, database, metrics, logs, frontend. |
| **Pool** | набор систем | The fixed list of systems every incident shows (§14.3). |
| **Alarm** | тревога | What reached the team and opened the incident — an alert, a support ticket, a message from the business. Not §4.8's signal, which stays the learner's word to the author. |
| **Evidence** | доказательство | A pointer into the recording that shows a problem: lines, a commit, a log entry, a time range, a query, a setting. |
| **Help** | помощник | Prepared questions with authored answers: free **reference** and paid **hints** (§14.7). |
| **Postmortem** | разбор инцидента | The author's account of the incident, shown in the review. |

In code an incident is `Incident`; the shared parent of a task and an incident, where one is needed,
is an *exercise*.

### 14.2 Worlds

A world is an application a junior could join as a new team member. It is authored once, with an AI
assistant, and runs on the author's machine for recording (§14.10); the learner never runs it.

**The first stage is three worlds with three or four incidents each**, in popular stacks and
ordinary domains of Ritocode's own. *Default:*

| World | Domain | Backend | Shared by all three |
| --- | --- | --- | --- |
| **A marketplace** | sellers, a catalogue, a cart, orders, payments | Java, Spring Boot | PostgreSQL; a React and TypeScript frontend; Docker Compose behind nginx; GitHub Actions |
| **A delivery service** | orders, couriers, dispatching, tracking | Python, FastAPI; Redis and a background worker | |
| **A places map** | places, search near a point, reviews, routes | Python, Django | |

A world carries:

- **The base revision** — its source tree, infrastructure and pipeline files, its database schema,
  settings and seed data, and its commit history (§14.3).
- **The world's context** — a team's onboarding page: what the product does, for whom, at what
  scale (users, requests, data), who maintains what, and the team's conventions. It plays the part
  of §3.5's context and is visible from the start of every incident in the world. Scale is stated
  here, because disproportion is judged against it.
- **Its dashboards and saved queries** — the same set in every incident of the world (§14.3).

Rules:

- **A world is an existing project.** It has conventions, utilities and history, so class 2 — no
  knowledge of the project — can be in an incident's key (§3.1's exclusion does not apply).
- **Incidents in a world are independent variants of the base revision**, not a chain. They can be
  solved in any order; the task list orders them by difficulty.
- **Written in English** — code, logs, commit messages, configuration — as §3.4. The world's context,
  the alarm, help and the postmortem are localised.
- **No hint comments**, as §3.4. **The 79-character limit does not apply**: a world follows its
  stack's usual formatter, and an incident is read on a computer.
- **Size is not banded.** A world is meant to be searched, not read through; *default* order of
  size: 2 000–6 000 lines of backend in 30–100 files, 1 000–3 000 lines of frontend.

### 14.3 The pool of systems

Every incident of every world shows **every system of the pool**, healthy or not, in the same order.
A system that appeared only when it mattered would tell the learner where to look. A system is added
to or removed from the pool for every world at once, never for one incident.

| System | What the learner sees | Where it comes from |
| --- | --- | --- |
| **Code** | Backend and frontend sources at the incident's revision: the file tree, a viewer with highlighting for each stack, and search across all files | the world plus the variant |
| **History** | The last 20–40 commits before the incident: author, date, message, diff. Commits made by an agent link the brief it was given, as the description of its pull request. The team's commit rules | the world plus the variant |
| **CI/CD** | Pipeline definitions; the recent runs with their steps, status and logs; deploys with their times | the recording |
| **Infrastructure** | Compose files or manifests, the proxy configuration, resource limits, environment variables with secret values masked | the world plus the variant |
| **Database** | Schema, indexes, row counts and sizes, selected server settings with their values; the slow-query statistics of the window; the queries the application ran, with recorded results and plans; the world's saved queries — connections, locks, table sizes — with their results | the recording |
| **Metrics** | The world's dashboards over the recording window: traffic, latency percentiles, errors, resources per service, database connections, queues, cache, one business metric | the recording |
| **Logs** | Every service's log lines over the window, filtered by service, level, text and time | the recording |
| **Frontend at run time** | One or more recorded browser sessions: network requests with status, timing and size; console messages | the recording |

**The database is shown, not run** (ADR 0016): nothing executes a query in the page. The learner
reads what the application ran and what the team's saved queries returned. The saved queries are
the same in every incident of a world, so their presence says nothing; their results differ.

**Noise belongs to the world.** A recording carries what a healthy run of the world produces —
routine warnings, a retried request, a flaky test in an old run. A coincidence in the scenario — an
unrelated deploy in the same minute — is allowed, because it happens in production, and it must be
real in the recording rather than painted on afterwards.

### 14.4 The incident

An incident carries:

- **Title** and **difficulty** — easy, medium or hard, on a scale of its own (§14.6).
- **World** — a reference to one world.
- **Variant** and **scenario** — for recording only; never sent to the learner.
- **Alarm** — what reached the team, in a few lines, with its time. Visible from the start.
- **Recording** — the data of every system of the pool.
- **Answer key** — zero to about five findings. Each finding is a card, **one or more pieces of
  evidence**, **one or more leaves to stop it now** and **one or more leaves to prevent it**, plus an
  optional note.
- **Hints** — one ladder of one to five rungs (§14.7).
- **Postmortem** — what happened, minute by minute; the cause; what stopped it; what prevents it;
  and what the brief or the world's rules did not say. Shown in the review, in place of §3.5's
  lesson.

Rules:

- **A finding may be a contributing cause.** A missing timeout that turned a slow query into an
  outage is a finding beside the slow query.
- **A false alarm is a valid incident.** An alert raised by load the context announced, where
  everything is as it should be, has an empty key or only `accept` leaves — the incident's clean
  task (§3.5).
- **Verdict cards** — over-engineering, under-engineering — follow §3.5: in the key only when the
  world's context makes them a problem.

### 14.5 Evidence

Evidence is what proves a finding. Each system has its kinds of pointer:

| System | A piece of evidence is |
| --- | --- |
| Code, infrastructure | a file and a range of lines |
| History | a commit, optionally a file in it |
| CI/CD | a run, optionally a step |
| Database | a table, an index, a setting, or one of the listed queries |
| Metrics | a dashboard panel and a time range |
| Logs | one or more log entries |
| Frontend at run time | a request or a console message |

- **The learner pins evidence while investigating**: every panel has a *pin* action, and pins gather
  in a tray. In step 1 each picked card is given one or more pins.
- **Every picked card needs at least one piece of evidence** before the answer can be checked — the
  incident's equivalent of §4.4's "at least one leaf". *Default.*
- **The key lists one or more accepted pieces of evidence per finding, and any one of them is
  right** — §5.1's tolerance, carried over: the slow query can be shown in its statistics, in its
  plan or in the lines that build it.
- **Matching is computed, not judged.** Identifiers match exactly; a range of lines matches if it
  overlaps the key's range; a time range matches if it overlaps the key's window; a set of log
  entries matches if it contains one the key lists.
- Evidence generalises #207's locations: when locations reach ordinary tasks, they are evidence of
  the kind *a file and a range of lines*.

### 14.6 Solving an incident

**Where.** An incident is listed in `/tasks` with an *incident* mark and its world, and opens at
`/incidents/{slug}`. Its review is `/incidents/{slug}/attempts/{id}`. Access is as for tasks: public
to investigate, sign-in to check.

**An incident is for a computer.** On a narrow screen the page shows the alarm, the world's context
and a note that the incident is meant for a wider screen; the panels are not laid out for a phone.

**The workspace.** A sidebar with the alarm, the world's context and the answer — the step at hand —
as in §4.4; beside it the **panels**, one per system of the pool, always present and always in the
same order; the pin tray; and the help button.

**Steps:**

0. **Investigation** — free. The learner opens panels, searches, filters and pins. Nothing is asked.
1. **Diagnosis** — cards from the **whole catalogue**, as §4.4's step 1 for medium and hard tasks
   (no shortlist), each given its evidence.
2. **Treatment, in two layers** — for each picked card, the whole tree twice: **what stops it now**
   and **what keeps it from happening again**. Each layer needs at least one leaf; `accept` is a
   valid answer in either. *Default.*
3. **Check** — scored (§14.8); the review opens.

**An attempt survives a closed tab.** For a signed-in learner the answer in progress — pins, picks,
leaves, hints taken — is saved on the server as the attempt's draft; for a signed-out one, in the
browser, then carried through sign-in as §4.6. This is new to the Attempts module, which records
only submitted answers today.

**Time.** *Default* estimates, to be calibrated from attempts (§8): easy 20–30 minutes, medium
30–45, hard 45–60.

### 14.7 Help

The help button opens a panel styled as a conversation with an assistant. The learner does not type:
they pick one of the questions offered, and the answer appears. **Every answer is authored. No model
is called** (ADR 0016): nothing is sent to a third party, a question costs nothing to serve, and the
same question always gets the same answer. The interface calls it *помощник* and does not claim the
answers are generated. *Default.*

Two kinds of question:

- **Reference — free.** How to read a latency chart, what p95 means, what an index does, what
  `max_connections` controls, how this world deploys. Written once per system of the pool, per
  concept, or per world — **never per incident**, and never naming a card, so a reference answer
  cannot point at the cause. Content validation checks that no reference answer contains a card's
  name. Reference is open to everyone.
- **Hints — each costs points.** One ladder per incident, from vague to specific: "start from the
  database", then "look at the slowest query of the minute the alert fired", then "count how many
  times it runs per request". A rung opens only after the one before it; each shows its cost before
  it is taken; the last rung never names the card. Taking a hint needs a signed-in attempt, because
  its cost is recorded there.

### 14.8 Scoring

§5's principles hold: deterministic, no traps, tolerant of alternatives, numbers as parameters. The
rules of §5.2 are extended; with *w* the card's weight:

| Event | Points |
| --- | --- |
| A card in the key is picked | **+10 · w** |
| A card in the key is not picked | **−3 · w** |
| A card not in the key is picked | **−3**, flat |
| For a found card, at least one piece of evidence matches the key's | **+5 · w** |
| For a found card, each piece of evidence that matches none of the key's | **−1** |
| For a found card, in each layer, at least one picked leaf is among the key's leaves for that layer | **+5 · w** per layer |
| For a found card, each picked leaf that is not among the key's leaves for its layer | **−2** |
| Each hint taken | **−h** for its rung |

- The maximum is the sum of **25 · w** over the findings in the key.
- The total is floored at zero and shown in points, as §5.2.
- **The economy is balanced last** (ADR 0016): every number here, and the cost *h* of each rung, is
  a starting value, tuned once incidents and attempts exist.

**Worked example.** A delivery-world incident: at lunchtime the checkout slows down until it fails.
The key has two findings: a query in a loop (`query-in-loop`, weight 2; evidence: its line in the
slow-query statistics or the lines that build it; now `ops.rollback`; prevent `auto.load-test` or
`brief.scale`) and a missing timeout on the call to the couriers' service (`missing-timeout`,
weight 2; evidence: the hanging requests in the logs; now `ops.config`; prevent `rule.conventions`).
Maximum: 50 + 50 = **100**. Starting hint costs: 2 for the first rung, 4 for the second.

The learner takes two hints, picks the query in a loop with two pins — the statistics line and an
unrelated log entry — chooses `ops.rollback` now and `manual.tests` to prevent it; picks
`unbounded-load` as well, which the key does not list; and misses the timeout.

- Query in a loop: +20 found, +10 evidence matched, −1 a pin that matched nothing, +10 the "now"
  layer matched, the "prevent" layer unmatched, −2 an extra leaf → **+37**
- Missing timeout: missed → **−6**
- Unbounded load: extra → **−3**
- Hints: **−6**

Total **22 of 100**. The review shows that the rollback was right, that a test of the query would not
have caught a load problem, and that the timeout was what turned slowness into an outage.

### 14.9 The treatment tree gains operational leaves

The tree of §3.3 gains the measures that live in the **running system** rather than in the code. It
stays **one tree for every task and every incident**: ordinary tasks may use the new leaves, and the
tree is still shown whole in step 2. *Default* leaves:

| Branch | New leaves |
| --- | --- |
| **`ops` — Change the running system** (Изменить работающую систему) — a new branch | `rollback` — roll back the release or the change · `config` — change a setting: a pool, a timeout, a limit · `index` — add or change an index · `capacity` — add resources or instances · `switch-off` — turn the feature off · `data-fix` — repair the data the incident damaged |
| **`auto`** | `alert` — an alert on a metric · `load-test` — a load test in the pipeline |

No leaf is removed, so every existing answer key stays valid. The new branch and leaves go into
`taxonomy/treatments.yaml` and its translations, and into §3.3's table, in the change that builds the
incident.

### 14.10 The boss on the arena

- **First stage:** incidents are listed in `/tasks` beside tasks, with their mark, open to everyone.
  A filter shows tasks, incidents or both.
- **Target model: incidents are opened with points earned on tasks.** Decided by the maintainer on
  2026-11-12:
  - **Every attempt counts, first or practice.** Ritocode does not police retries: a learner who
    games the balance only cheats themselves, as §3.2 says of the open catalogue.
  - **A task counts once, at the best score among its attempts.** Coming back to a task can raise
    what it brings; repeating it cannot multiply it.
  - **Each incident has a price, and opening it spends that price.** With 10 points and incidents
    at 5, a learner opens two. An opened incident stays open for good.
  - **The balance is computed, not stored:** what the learner's tasks brought, minus the prices of
    the incidents they opened. The one new stored fact is which incidents a learner opened, and at
    what price.
  - A locked incident is listed with its price and the learner's balance; a signed-out visitor sees
    the price and a sign-in prompt.
- **Progress is unchanged.** §4.7 still counts first attempts only; the balance is a separate count
  and does not touch it.
- **This departs from §4.7's "no XP, no levels, no leaderboard"** on the maintainer's decision of
  2026-11-11: the balance exists only to open incidents, is shown only beside them, and there is
  still no leaderboard.

### 14.11 Review

The review opens in the incident's workspace, with:

- **The score**, with its composition: findings found of present, evidence matched, each layer's
  treatments matched, extra picks, hints taken and their cost.
- **Every finding in the key**, found or missed: the learner's evidence beside the key's — each opens
  the panel at that place — the learner's leaves beside the author's in both layers, the note, and
  the full card.
- **Every extra pick**, with the signal button of §4.8.
- **The postmortem.**
- **Other incidents in this world.**
- **Try again** — a new attempt, marked as practice. Hints taken in a practice attempt cost the same.

### 14.12 Authoring and engineering

**Authoring** — `content/` carries worlds and incidents beside cards and tasks; the file format goes
into [`CONTENT_FORMAT.md`](CONTENT_FORMAT.md) with the change that builds them. In outline:

- **A world**: its base revision as a source tree, its context, its dashboards and saved queries, and
  the scripts that run it locally.
- **An incident**: the variant as a patch to the base revision, the scenario, the recording, the
  alarm, the key, the hint ladder and the postmortem.
- **Reference**: questions and answers per system, per concept and per world.

**Recording** is a command on the author's machine: it applies the variant, starts the world, runs
the scenario with a load generator and a scripted browser, and exports every system of the pool —
logs, metric series, database statistics, plans and saved-query results, pipeline runs, browser
sessions — into the incident's recording. Recording again gives a new recording, reviewed in a pull
request like any content.

**A skill, `author-incident`**, does for incidents what `author-task` does for tasks (§7.1): drafts
the variant, scenario, alarm, key, hints and postmortem from the maintainer's idea; records; runs
content validation; and runs the **blind smoke test** — a separate session that receives exactly what
a learner would, without hints, and investigates the recording. It reports what it found, which
systems it opened, and every difference from the key: the check that an incident is neither
unsolvable nor obvious. Writing a world is larger work, done with the maintainer before its first
incident.

**Content validation** adds: every system of the pool has data in every recording; every piece of
evidence in a key points at something that exists in the recording; the ladder has one to five
rungs; no reference answer names a card.

**Engineering**, within §9's rules:

- **Content** owns worlds, incidents, recordings, hints and reference; ingest loads them.
- **Attempts** owns incident attempts: their drafts, the hints taken, scoring per §14.8 as a pure
  function beside §5's, the incidents a learner opened, and the balance of §14.10, computed from
  attempts and openings rather than stored.
- **API**: the incident to investigate (alarm, world context, the panel index), each system's
  recording, loaded per panel; taking a hint, which returns its text and records it in the attempt;
  saving the draft. **A hint's text, like the answer key, never leaves the server before it is
  taken.**
- **Recordings** are megabytes, not the tens of kilobytes of §10.2. Each panel's data is loaded when
  the panel is first opened; *default* budget: at most 5 MB compressed per incident.
- **Frontend**: highlighting for every world's languages and for YAML, SQL and logs; a virtualised
  log view; charts with a selectable time range; the pin tray.
- **Security**: nothing the learner writes runs anywhere, as §10.1. The world runs only on the
  author's machine, during recording.
- **Measurement** (§8): the internal journal adds the panels opened and the time in each; Umami adds
  *incident opened*, *hint taken* and *evidence pinned*.

### 14.13 Decisions and defaults

**Decided by the maintainer on 2026-11-11** (ADR 0016): the incident keeps the answer model of
tasks; a fixed pool of systems in every incident; worlds with several incidents each, three worlds
of three or four incidents to start, in Python and Java, in ordinary domains; recorded, not live;
the database shown, not run; evidence in the answer; treatment in two layers; operational leaves in
the one tree; help as prepared questions with authored answers, reference free and hints paid; the
boss first in the task list, later unlocked by points; the economy balanced last.

**Confirmed by the maintainer on 2026-11-12** — the defaults this section first chose, with one
changed:

1. The worlds' domains and stacks of §14.2.
2. Evidence required on every picked card, and −1 for a pin that matches nothing (§14.5, §14.8).
3. Both treatment layers required on every card (§14.6).
4. The interface word *помощник*, not *ИИ*, and no claim that answers are generated (§14.7).
5. Hints only in a signed-in attempt (§14.7).
6. The `ops` branch and its leaves, `auto.alert` and `auto.load-test` (§14.9).
7. **Changed:** the balance counts every attempt, each task once at its best score, and opening an
   incident spends its price (§14.10). The first stated default — first attempts only, a threshold,
   nothing spent — is withdrawn.
8. An incident not laid out for a phone (§14.6).
9. The size of a world and the recording budget (§14.2, §14.12), to be revisited once the first
   world and a few of its incidents exist.

The section has no open defaults left; the economy's numbers remain the open part (§14.8).

---

## Appendix A — Decision log

All decided by the maintainer on 2026-09-30, in answer to questions put while preparing this file.

| # | Decision |
| --- | --- |
| 1 | Audience: vibe coders with little architecture knowledge; content starts from the basics. Superseded on 2026-10-28 by [ADR 0015](adr/0015-arena-home-and-junior-audience.md): junior developers who build with AI agents. |
| 2 | Step 1 asks what the learner sees; the context moves the treatment. The context is visible from the start. |
| 3 | No traps. Found +10, missed −2 to −5; the economics are tuned separately. The whole catalogue is not labelled per material — too costly for authors. |
| 4 | Locations of findings wait for a later version; the MVP keeps the simple card. |
| 5 | No accepted-alternative cards, no four-valued treatment scale, no justification links: too costly for authors. MVP: the author lists several leaves; later: equivalence at the tree level. |
| 6 | Weight belongs to the card, not the finding. |
| 7 | Material is generated to order; everything is simulated by default. Authors' own code and real-project labels later; open source undecided. |
| 8 | New projects only in the MVP; class 2 tasks later. |
| 9 | A task takes 5–25 minutes; some take five. Adaptive layout for phones. |
| 10 | One stack in the MVP — Python. Treatments are stack-independent. AI translation between stacks later. |
| 11 | Every problem goes into the catalogue, including agent-specific ones and ones no task uses. Terms: task catalogue, problem catalogue, problem cards. |
| 12 | No relations between cards. |
| 13 | Localisation from the start; content in Russian; English must not require rewriting content. |
| 14 | The treatment tree is shown whole. |
| 15 | During a task, only a card's name and summary are visible. |
| 16 | Contexts are separate tasks over a shared material, one to four of them. |
| 17 | Progress by classes and cards; no XP, no leaderboard. |
| 18 | Tasks have zero to about ten findings; clean tasks exist. |
| 19 | Signed-out visitors can work through a task; checking requires sign-in; after sign-in they return to where they were. |
| 20 | 20 tasks for the MVP, written by the maintainer alone, with AI assistance and an AI smoke test. |
| 21 | Content lives in `content/` in this repository. |
| 22 | Outdated issues closed as not planned; outdated documents, ADRs and code removed. |
| 23 | Takeaway in the MVP: score and review. The "tell the agent" block and the personal memo later. |
| 24 | An extra pick costs a small flat penalty; the learner can signal a real finding. |
| 25 | Several leaves in a key mean "any of". |
| 26 | Easy tasks show a shortlist: the key's cards plus 15–20 others. |
| 27 | Material rules: no hint comments; a blind AI smoke test before publishing. |
| 28 | Authoring in the MVP is a Claude Code skill; an admin screen for it later. |
| 29 | Specification first, then the stage plan and backlog operations for approval, then changes on GitHub. Documents in English. |
| 30 | Public address on the project's own domain; open registration; GitHub and Google sign-in. |
| 31 | A public problem catalogue page, one page, no page per card. |
| 32 | Admin: signals, users and attempts. |
| 33 | Measurement: an internal journal and self-hosted Umami. |
| 34 | No deadline. Launched when all tasks work publicly and the maintainer and two acquaintances found them useful. |
| 35 | Free. Hosting on a VPS in Russia. |
