# 0016 — The incident: a boss task over a whole production stack

- Status: Accepted — decided by the maintainer on 2026-11-11; the numbers of the economy are left to
  the end, and the details below to the specification
- Date: 2026-11-11
- Relates to: [#227](https://github.com/shoraLBRT/ritocode/issues/227),
  [#207](https://github.com/shoraLBRT/ritocode/issues/207) (locations),
  [#209](https://github.com/shoraLBRT/ritocode/issues/209) (existing projects),
  [#211](https://github.com/shoraLBRT/ritocode/issues/211) (other stacks)
- Builds on: [ADR 0010](0010-diagnosis-of-ai-written-code.md), [ADR 0015](0015-arena-home-and-junior-audience.md);
  [CONCEPT.md](../CONCEPT.md) §4–6; [SPEC.md](../SPEC.md) §3.3–3.5, §5

## Context

A task today is a small project read as code: context, the agent's brief, up to twelve files, and a
diagnosis picked from the catalogue. That trains the regular work — reading what an agent wrote.

It does not train what a junior meets in their first weeks in a team: a production application
with a database, a pipeline, infrastructure, dashboards and logs, where something is wrong and
nobody says where. The maintainer saw a paid course built on that idea — an application with
incidents laid in, investigated under load in Grafana — and asked what of it belongs in Ritocode.

Most of such incidents are already in the catalogue's terms. A sale nobody mentioned in the brief,
and the query that was fine on a hundred rows, is **disproportion**; the defect was in the brief as
often as in the code (ADR 0010). What Ritocode lacks is not the answer model but the material: the
evidence of a running system, and the search for it.

## Decision

**The incident is a second task type: a boss.** Long and hard, it keeps the answer model of
ADR 0010 whole — problems picked from the same catalogue, treatments from the same tree, a review,
and a score that is a pure function of the answer and an authored key. Nothing the learner writes
is executed, and nothing runs in the learner's page either.

**What differs is the scope of the search: the whole technical stack, not the code alone.**

1. **A fixed pool of systems.** Every incident of every world shows every system of the pool,
   whether it is involved or not. If an incident showed only the code and the database, the learner
   would know where to look and that everything shown matters; with the whole stack always present,
   they have to search. The starting pool, to be refined in the specification:
   - **code** — backend and frontend sources;
   - **repository history** — commits with their diffs, and the team's commit rules;
   - **CI/CD** — pipeline definitions and the logs of their runs;
   - **infrastructure** — compose files or manifests, proxy configuration, environment (secrets
     masked);
   - **database** — schema, indexes, settings, the slow-query log, and queries with their results;
   - **metrics** — dashboards;
   - **logs** — searchable and filterable;
   - **frontend at run time** — a recorded network log and console of a browser session.

   A system is added to or removed from the pool for every world and incident at once, never for one.

2. **Worlds, and incidents in them.** A *world* is an application, authored once. Each incident is
   a small variant of it — a changed line of code, a database setting, a dropped index, an edited
   YAML file — plus a scenario: the load and events that make it fail. The learner returns to a
   world they already know and learns its configuration, its queries and its conventions as a
   junior learns a team's application. **The first stage is three worlds with three or four
   incidents each**, in different popular stacks and ordinary domains — for example a marketplace,
   a delivery service and a maps service, two in Python and one in Java. Which world gets which
   stack and domain is the specification's choice; the domains are Ritocode's own, not copies of
   the course that prompted this.

3. **Recorded, not live.** The author runs each variant under its scenario once and records what
   every system of the pool produced — healthy systems included, over the same time window, with
   their ordinary noise. The learner reads that recording. **The database is shown, not run:** its
   schema, indexes and settings, the queries the application executed, and their results and plans
   as recorded tables. No database engine runs in the page — it would cost much to build and add
   little to what is taught. No environment exists per learner, so there is nothing to host,
   isolate or reset, and the same answer always meets the same evidence.

4. **The answer carries evidence.** For each root cause the learner picks a card and points at what
   proves it — lines of a file, a commit, a log entry, a time range on a chart, a query, a table, an
   index or a setting. Evidence generalises the locations of #207 beyond code. Treatment is chosen
   in two layers: what stops the incident now, and what prevents it from happening again — a better
   brief, a rule, a check — both from the treatment tree.

5. **The treatment tree gains operational treatments** — a rollback, a configuration change, an
   index, among others — in the one tree every task uses, not in a tree of the incident's own, so
   ordinary tasks may use them too. Whether they form a branch of their own (*where the measure
   lives*: in the running system rather than in the code) or join `manual`, and the exact leaves, is
   the specification's choice.

6. **Help is prepared questions, answered by the author.** The learner opens the help panel and
   sees questions as if they could ask an assistant — "how do I read this dashboard?", "what does
   this setting do?", "where would you look first?" — and picks one; the answer is authored. No
   model is called: nothing is sent to a third party, nothing costs money per question, and the same
   question always gets the same answer. Questions are of two kinds:
   - **reference** — free; written once per system of the pool or per concept, never per incident,
     so they can never point at the cause;
   - **hints** — each costs *N* points; written per incident as a ladder from vague to specific,
     each more detailed than a reference answer. Their cost is part of the score, which stays
     deterministic.

7. **Scoring** is SPEC §5 extended: the same points for found and missed problems and for
   treatments, points for evidence, minus the costs of the hints taken; floored at zero; every
   number a parameter. **The economy — the cost of a hint, the points for evidence, the price of a
   boss — is balanced at the end**, once incidents exist to try it on.

8. **The boss on the arena.** At first an incident simply appears in the task list beside ordinary
   tasks. The target model: an incident is **unlocked by points earned on other tasks** — solve
   enough of them and a boss opens. The threshold belongs to the economy of point 7.

## Alternatives

- **A live environment per learner** — containers, a real Grafana, a load generator, a shell or a
  database connection. Closest to production, and the course that prompted this works that way.
  Rejected for now: hosting and isolating it costs money and security work per learner, it brings
  back execution as the source of truth that ADR 0010 withdrew, and an incident reproduced live can
  differ from the one the key describes. A downloadable lab — the learner runs the world on their
  own machine and answers on the site — stays possible later.
- **A database engine in the page** (PostgreSQL compiled to WebAssembly, or SQLite) with a query
  console over a snapshot. Rejected by the maintainer: much to build, little gained over recorded
  queries and results.
- **Only the systems an incident needs.** Cheaper to author and to read. Rejected by the
  maintainer: the set of panels is itself the hint.
- **A separate answer model** — a free-text root cause judged by a model. Rejected: it replaces a
  defensible key with an opinion, as ADR 0010 found for AI review.
- **A real model answering the learner's questions.** Rejected by the maintainer: it costs money
  per question, sends the learner's text to a third party, answers differently each time, and with
  the incident's data it would solve the incident.
- **One world for every incident, or a new application per incident.** One world teaches one
  stack; a world per incident makes authoring its most expensive part every time and loses the
  return to a familiar application. Several worlds with several incidents each sit between.

## Consequences

- **Authoring changes shape.** A world is a large piece of work done once; an incident is a
  variant, a scenario, a recording, a key and a hint ladder; reference questions are written once
  per system. It needs its own skill, `author-incident`, with a blind smoke test in which a session
  that sees only the panels has to find the cause — the check that an incident is neither unsolvable
  nor obvious.
- **SPEC §3.4's material rules do not fit a world.** Python only, new projects only, 79-character
  lines read on a phone, and the size bands were written for small projects. Java arrives with the
  incident before ordinary tasks get other stacks (#211). The incident is specified in SPEC §14,
  with the defaults it chose listed in §14.13; CONCEPT §6 names it.
- **The treatment tree changes for every task.** New leaves are a content change to the tree's one
  file; existing keys stay valid, since no leaf is removed.
- **Class 2 becomes possible.** A world has conventions, a history and existing utilities, so "no
  knowledge of the project" can be taught inside it — the gap #209 names.
- **The catalogue grows** with operational problems — an exhausted connection pool, a missing
  index, an N+1 query, a misconfigured pipeline — in the existing card format.
- **An incident is desktop-first.** Eight panels do not read on a phone.
- **An attempt is long**, so it has to survive a closed tab; the Attempts module records only
  finished attempts today.
- **Unlocking needs a balance of points per learner** — derived from attempts the Attempts module
  already keeps, or stored; the specification decides.
- The work is [#227](https://github.com/shoraLBRT/ritocode/issues/227), in the *Later* milestone;
  its steps become issues when the maintainer plans it.
