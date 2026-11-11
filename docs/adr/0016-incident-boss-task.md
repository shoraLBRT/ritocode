# 0016 — The incident: a boss task over a whole production stack

- Status: Accepted — the shape decided by the maintainer on 2026-11-11; the open questions below
  are not
- Date: 2026-11-11
- Relates to: [#227](https://github.com/shoraLBRT/ritocode/issues/227),
  [#207](https://github.com/shoraLBRT/ritocode/issues/207) (locations),
  [#209](https://github.com/shoraLBRT/ritocode/issues/209) (existing projects)
- Builds on: [ADR 0010](0010-diagnosis-of-ai-written-code.md), [ADR 0015](0015-arena-home-and-junior-audience.md);
  [CONCEPT.md](../CONCEPT.md) §4–6; [SPEC.md](../SPEC.md) §3.4–3.5, §5

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

**The incident is a second task type: a boss.** Rare, long and hard, placed on the arena apart from
ordinary tasks. It keeps the answer model of ADR 0010 whole — problems picked from the same
catalogue, treatments from the same tree, a review, and a score that is a pure function of the
answer and an authored key. Nothing the learner writes is executed.

**What differs is the scope of the search: the whole technical stack, not the code alone.**

1. **A fixed pool of systems.** Every incident shows every system of the pool, whether it is
   involved or not. If an incident showed only the code and the database, the learner would know
   where to look and that everything shown matters; with the whole stack always present, they have
   to search. The starting pool, to be refined in the specification:
   - **code** — backend and frontend sources;
   - **repository history** — commits with their diffs, and the team's commit rules;
   - **CI/CD** — pipeline definitions and the logs of their runs;
   - **infrastructure** — compose files or manifests, proxy configuration, environment (secrets
     masked);
   - **database** — schema, indexes, settings, the slow-query log, and a query console;
   - **metrics** — dashboards;
   - **logs** — searchable and filterable;
   - **frontend at run time** — a recorded network log and console of a browser session.

   A system is added to or removed from the pool for every incident at once, never for one.

2. **A world, many incidents.** An application — a *world* — is authored once. Each incident is a
   small variant of it: a changed line of code, a database setting, a dropped index, an edited
   YAML file — plus a scenario: the load and events that make it fail. The learner returns to a
   world they already know and learns its configuration, its queries and its conventions as a
   junior learns a team's application. This is SPEC §3.4's "one material, several tasks" at a
   larger scale.

3. **Recorded, not live.** The author runs each variant under its scenario once and records what
   every system of the pool produced — healthy systems included, over the same time window, with
   their ordinary noise. The learner investigates that recording in the browser: the query console
   runs against a snapshot of the database inside the page. No environment exists per learner, so
   there is nothing to host, isolate or reset, and the same answer always meets the same evidence.

4. **The answer carries evidence.** For each root cause the learner picks a card and points at what
   proves it — lines of a file, a commit, a log entry, a time range on a chart, a table, an index or
   a setting. Evidence generalises the locations of #207 beyond code. Treatment is chosen in two
   layers: what stops the incident now, and what prevents it from happening again — a better brief,
   a rule, a check — both from the treatment tree.

5. **Help, free and paid.**
   - **Reference** is free. It explains the systems and concepts of the pool — how to read a
     dashboard, what a database setting does, what an index is — and is written once per system or
     concept, never per incident, so it can never point at the cause.
   - **Hints** cost points. They are authored per incident as a ladder from vague to specific, each
     with a fixed cost; the costs are part of the score, which stays deterministic.
   - **Ask AI** is a question box. Its answers differ between attempts, so its content never
     changes the score. How much of the incident it sees, and what a question costs, is open.

6. **Scoring** is SPEC §5 extended: the same points for found and missed problems and for
   treatments, points for evidence, minus the costs of the hints taken; floored at zero; every
   number a parameter.

## Alternatives

- **A live environment per learner** — containers, a real Grafana, a load generator, a shell or a
  database connection. Closest to production, and the course that prompted this works that way.
  Rejected for now: hosting and isolating it costs money and security work per learner, it brings
  back execution as the source of truth that ADR 0010 withdrew, and an incident reproduced live can
  differ from the one the key describes. A downloadable lab — the learner runs the world on their
  own machine and answers on the site — stays possible later.
- **Only the systems an incident needs.** Cheaper to author and to read. Rejected by the
  maintainer: the set of panels is itself the hint.
- **A separate answer model** — a free-text root cause judged by a model. Rejected: it replaces a
  defensible key with an opinion, as ADR 0010 found for AI review.
- **A new application per incident.** Rejected: the world is the expensive part of authoring, and
  coming back to a familiar application is part of what is taught.
- **Ask AI with the incident's data, free.** Rejected: it would solve the incident.

## Consequences

- **Authoring changes shape.** The world is a large piece of work done once; an incident is a
  variant, a scenario, a recording, a key and a hint ladder. It needs its own skill,
  `author-incident`, with a blind smoke test in which a session that sees only the panels has to
  find the cause — the check that an incident is neither unsolvable nor obvious.
- **SPEC §3.4's material rules do not fit a world.** Python only, new projects only, 79-character
  lines read on a phone, and the size bands were written for small projects. The incident gets its
  own section in SPEC before anything is built; until then this ADR is the record. CONCEPT §6 gains
  the incident at the same time.
- **Class 2 becomes possible.** A world has conventions, a history and existing utilities, so "no
  knowledge of the project" can be taught inside it — the gap #209 names.
- **The catalogue grows** with operational problems — an exhausted connection pool, a missing
  index, an N+1 query, a misconfigured pipeline — in the existing card format.
- **An incident is desktop-first.** Eight panels and a query console do not read on a phone.
- **An attempt is long**, so it has to survive a closed tab; the Attempts module records only
  finished attempts today.
- The work is [#227](https://github.com/shoraLBRT/ritocode/issues/227), in the *Later* milestone;
  its steps become issues when the maintainer plans it.

## Open questions

1. **The world's stack and domain.** Python on the backend continues the tasks; the database is
   most likely PostgreSQL, since its settings and indexes are what is taught. The domain should be
   Ritocode's own, not a copy of the course that prompted this.
2. **The in-browser database.** PGlite (PostgreSQL compiled to WebAssembly) would keep real
   settings, `EXPLAIN` and indexes; SQLite is the fallback. Needs a spike: snapshot size, load time,
   how faithfully plans and settings show.
3. **Ask AI's scope and cost.** Either it sees only the reference scope — no incident data, no key —
   and is free; or it sees the incident's panels and every question costs a flat number of points.
   Either way it runs on a paid API and sends the learner's question to a third party (SPEC §10.4).
4. **Treatment leaves for operations.** Whether the tree needs leaves such as a rollback, a
   configuration change or an index, or whether its existing leaves cover them.
5. **The boss on the arena.** How often an incident appears, and whether it is unlocked by solved
   tasks.
