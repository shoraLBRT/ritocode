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

In C#, a task is `DiagnosisTask`, so that it never collides with `System.Threading.Tasks.Task`.

---

## 2. Audience and what "launched" means

### 2.1 Who it is for

**Vibe coders** — people who build software by instructing agents and know little about
architecture. The content therefore starts from the basics: problem cards are written for someone
who has never heard the word "coupling", and easy tasks are short enough to finish in five minutes.

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

**Size follows difficulty.** Estimates assume a vibe coder reads simple Python for understanding at
20–30 lines a minute — slower than skimming, faster than line-by-line review (7–8 lines a minute).
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
| `/` | Landing: what Ritocode is, a link to a demo task and to the problem catalogue | public |
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

### 4.2 Problem catalogue

One page. Cards are grouped by the six classes, each shown in full. A search box filters by name,
summary and keywords. Every card has an anchor, so a card can be linked to without a page of its
own.

### 4.3 Task catalogue

A list of tasks: title, difficulty, an expected time derived from difficulty, and — for a signed-in
learner — whether they have solved it. Filters: difficulty, solved or not. **No class tags** on
tasks: they would give the answer away. Ordered by difficulty, then by publication.

### 4.4 Solving a task

The screen has three areas: **context and brief**, **material**, and **the answer**. On a phone they
become three tabs — Context, Code, Answer.

**Material viewer.** A file tree with a line count per file, then the selected file with syntax
highlighting and line numbers. Above the tree, an overview generated at ingest: total lines, file
count, and the dependencies the project declares. Read-only.

**Step 1 — Diagnosis: what do you see?** The learner picks cards from the catalogue — only name and
summary are shown — by searching or by browsing the six groups. The question is "what do you see
here that should be named", not "what is wrong": a god class that is fine in this context is still
a god class, and whether to leave it is step 2's decision.

- **Medium and hard tasks** offer the entire catalogue.
- **Easy tasks** offer a shortlist: the cards in the answer key plus 15–20 others drawn from the
  whole catalogue. The shortlist is computed at ingest, deterministically from the task's slug, so
  every learner sees the same list. The author does nothing to produce it.
- Picking nothing is a valid answer. On a clean task it is the right one.

**Step 2 — Treatment: what do you do with it here?** For each picked card, the whole treatment tree.
The learner opens one or more branches and ticks one or more leaves in each. Every picked card needs
at least one leaf before the answer can be checked. The learner can go back to step 1 at any time.

**Check.** The answer is submitted and scored (§5), and the review opens.

### 4.5 Review

- **Score** in points, with its composition beneath: findings found out of findings present, extra
  picks, treatments that matched.
- **Every finding in the key**, marked found or missed. For each: the learner's leaves beside the
  author's, the author's note if there is one, and the full card, expandable.
- **Every extra pick**, marked "not in this task's answer", with the full card and a button —
  *"I'm sure it is here"* — that opens a one-line comment and sends a signal (§4.8).
- **The lesson**, if the task has one.
- **The same code in another context** — links to the other tasks over this material.
- **Try again** — a new attempt, marked as practice (§5.4).

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
- **Per card** — met, found, missed, picked when absent, treated right.

No XP, no levels, no leaderboard.

### 4.8 Signals

A signal is a learner saying "this card is really here, the answer key missed it". It records the
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
| `GET /tasks` | The task catalogue; solved flags when signed in | public |
| `GET /tasks/{slug}` | A task to solve: context, brief, material, the card list to pick from with name and summary only | public |
| `POST /attempts` | Start an attempt at a task | signed in |
| `PATCH /attempts/{id}` | Record the step reached | owner |
| `POST /attempts/{id}/submit` | Submit the answer; returns the result and reveals the key | owner |
| `GET /attempts/{id}` | An attempt and its result | owner |
| `GET /attempts` | The caller's attempts, optionally for one task | signed in |
| `GET /me`, `GET /me/progress` | The caller, and their progress | signed in |
| `POST /signals` | Send a signal from an attempt | owner of the attempt |
| `GET /admin/signals`, `POST /admin/signals/{id}/resolve` | Signals | admin |
| `GET /admin/users`, `GET /admin/attempts` | Users and attempts | admin |

Sign-in lives outside the versioned API: `GET /auth/login/{provider}?returnUrl=`, the providers'
callbacks, and `POST /auth/logout`.

The answer key never leaves the server before an attempt at the task is submitted, and afterwards
only inside that attempt's result.

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

Recorded so they are not lost and not built early.

- **What the learner takes away:** "what to tell the agent next time" on the review, from a new card
  field; then a personal memo that accumulates across tasks and copies in one go.
- **Locations:** a finding's file and lines in the key; the learner pointing at them; scoring per
  instance.
- **Equivalent leaves** declared once in the treatment tree, so an author need not list both.
- **Existing projects** as material, and with them class 2 tasks.
- **Authors' own code** as material, and a label for material taken from a real project.
- **Other stacks**, and AI translation of a task into the learner's stack.
- **Adding and editing tasks in the admin area**, which also opens authoring to users.
- **Community authorship** of cards, with counter-arguments rather than votes.
- **English**, then other locales.
- **Difficulty beyond the shortlist** — for example, contexts that must be inferred rather than read.

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

## Appendix A — Decision log

All decided by the maintainer on 2026-09-30, in answer to questions put while preparing this file.

| # | Decision |
| --- | --- |
| 1 | Audience: vibe coders with little architecture knowledge; content starts from the basics. |
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
