# Backlog rewrite

- **Status:** For the maintainer's approval. **Nothing below has been done yet.**
- **Date:** 2026-09-30
- **Lifetime:** this file exists only to be approved. Once the operations are carried out, the
  `NEW-n` placeholders in [`ROADMAP.md`](ROADMAP.md) are replaced with real issue numbers, and this
  file is deleted in the same pull request, before it merges.

It lists every change to the 71 open issues and the project board that the new plan needs. The
rules come from [`SPEC.md`](SPEC.md) §11 and the maintainer's answers:

- an issue that belongs only to the previous product is **closed as not planned**, labelled
  `superseded`, with a comment linking ADR 0010 — closed, not deleted, because GitHub deletion
  cannot be undone;
- an issue whose subject exists in both products — sign-in, CI, logging, security, catalogue
  screens — **keeps its number** and gets a new title and body;
- everything else the MVP needs is a **new issue**.

**Totals:** 54 closed (53 as not planned, 1 as completed), 17 rescoped, 19 created. Open afterwards:
36, all labelled `phase:mvp`, every one in a stage.

---

## 1. Labels and milestones

**New labels:** `phase:mvp`, `superseded`, `type:content`, `epic:cleanup`, `epic:content`,
`epic:authoring`, `epic:trainer`, `epic:admin`, `epic:public-site`, `epic:measurement`,
`epic:deploy`.

Old labels are **kept**: the closed issues carry them, and deleting a label strips it from its
history. They are simply no longer applied.

**New milestones:** `S0 · Clear the ground`, `S1 · Content foundation`, `S2 · Authoring`,
`S3 · Trainer`, `S4 · Accounts`, `S5 · Progress, signals, admin`, `S6 · Public site and measurement`,
`S7 · Production`, `S8 · Launch`.

---

## 2. Close as not planned — 53 issues

Each gets the label `superseded` and this comment:

> Closed as not planned. This issue belongs to the product Ritocode was before 2026-09-30.
> [ADR 0010](https://github.com/shoraLBRT/ritocode/blob/main/docs/adr/0010-diagnosis-of-ai-written-code.md)
> redefined it as a trainer for diagnosing AI-written code — see `docs/SPEC.md` and
> `docs/ROADMAP.md`. Work already merged for this issue is removed in stage S0 or S1; the last commit
> of the previous product is tagged `pre-diagnosis`.

### Phase 1 — 15 issues whose subject does not exist in the new product

| # | Title | Why it has no place |
| --- | --- | --- |
| 5 | Define object storage layout for artifacts and bundles | Object storage is removed (SPEC §9.2) |
| 13 | Implement workspace reset and snapshot creation | No workspaces: nothing is edited |
| 15 | Dispatch evaluation jobs asynchronously from submissions | No evaluation queue: scoring is synchronous |
| 16 | Implement submission result and report retrieval API | Replaced by the Attempts module (NEW-7) |
| 17 | Build evaluation orchestrator worker pipeline | No validator pipeline |
| 18 | Implement validator plugin interface | No validators |
| 19 | Implement baseline validators: compile, tests, lint, patch scope | No validators |
| 21 | Provision isolated runner environment for evaluation jobs | Nothing the learner writes is executed |
| 22 | Build runner images for supported languages | No runner |
| 23 | Collect and store runner logs and evaluation artifacts | No runner |
| 25 | Expose profile, progress and leaderboard APIs | No leaderboard; progress API is #24 |
| 28 | Build workspace editor UI with file tree | Replaced by a read-only viewer in NEW-8 |
| 36 | Harden workspace file handling and sandbox boundaries | No workspaces, no sandbox |
| 38 | Create validator and runner test suite | No validators, no runner |
| 43 | Implement workspace and temporary artifact cleanup job | No workspaces, no artifacts |

### Phase 2 — all 22 issues: repository integration

#44 GitHub App installation · #45 webhook ingestion · #46 repository and snapshot schema · #47
repository sync worker · #48 pinned snapshot pipeline · #49 repository-backed package format · #50
problems from snapshots · #51 workspace materialisation from snapshots · #52 snapshot access
controls · #53 patch-scope validator extensions · #54 performance benchmark validator · #55
test-quality validator · #56 repository-backed catalogue APIs · #57 frontend for repository-backed
problems · #58 AI feedback summarisation · #59 hint ladder and coaching · #60 search index · #61
recommendation service · #62 maintainer authoring CLI · #63 repository sync observability · #64
snapshot retention · #65 repository-backed QA fixtures.

### Phase 3 — all 16 issues: contributions to real repositories

#66 trust level model · #67 trust score service · #68 PR attempts schema · #69 contribution policy
enforcement · #70 external branch and patch workflow · #71 provider PR lifecycle · #72 contribution
rate limiting · #73 suspicious patch detection · #74 maintainer policy API · #75 maintainer review
queue · #76 portfolio model · #77 portfolio screens · #78 moderator tooling · #79 production scaling
for runners · #80 disaster recovery · #81 Phase 3 rollout plan.

Two of these touch subjects the new product has — #58 AI feedback and #80 backups. Neither carries
over: ADR 0010 rejected AI commentary on the score for the core loop, and backups for the MVP are
part of NEW-18.

---

## 3. Close as completed — 1 issue

| # | Title | Comment |
| --- | --- | --- |
| 37 | Create backend integration test harness | The harness it asked for exists: a PostgreSQL container per test assembly, a migrated database per test class. Its MinIO half is removed with object storage in NEW-3. From here each feature ships its own integration tests. |

---

## 4. Rescope — 17 issues keep their number

Each gets a new title and body (context, scope, acceptance criteria, links to the spec), `phase:1`
replaced by `phase:mvp`, the new `epic:*` label, and its milestone. Each also gets a comment:

> Rescoped for the new product (ADR 0010, `docs/SPEC.md`). Earlier comments describe work for the
> previous product; what survives of it is named in the new description.

| # | New title | Stage | Priority | Size | What changes |
| --- | --- | --- | --- | --- | --- |
| 40 | Retire the previous product's documents and rewrite the entry documents | S0 | P0 | M | Was "write architecture docs". Now the S0 documentation clean-up (ROADMAP, S0). |
| 9 | Content read APIs: problem catalogue, treatment tree, task catalogue, task to solve | S1 | P0 | M | Was the problem catalogue service. The existing catalogue endpoints are replaced by SPEC §9.3's four reads. |
| 42 | Write the 20 MVP tasks | S2 | P0 | XL | Was three C# refactoring packages. Now 20 diagnosis tasks in Python per `CONTENT_FORMAT.md`, each passing the smoke test. Stays open across stages. |
| 20 | Diagnosis scoring | S3 | P0 | S | Was verdict aggregation over validators. Now SPEC §5 as a pure function, parameters in configuration. |
| 26 | Frontend shell: translation catalogue, signed-in state, phone-width layout | S3 | P0 | M | The shell exists. Adds i18n, auth-aware routing, a layout that works at phone width. |
| 27 | Task catalogue and problem catalogue pages | S3 | P0 | M | Was the problem catalogue and detail screens. Now SPEC §4.2–4.3. |
| 29 | Review screen | S3 | P0 | M | Was submission status and result. Now SPEC §4.5. |
| 6 | Sessions: cookie sign-in state, `/me`, sign-out, CSRF protection | S4 | P0 | M | The identity seam exists. Adds the real session behind it. |
| 7 | Sign-in with GitHub and Google, one account per verified e-mail | S4 | P0 | M | Was GitHub account linking. Adds Google and the verified-e-mail rule of SPEC §6.1. |
| 24 | Progress by classes and cards | S5 | P1 | M | Was progress and XP. No XP; SPEC §4.7 from first attempts. |
| 30 | Progress page | S5 | P1 | S | Was profile and progress screens. Only the progress page remains. |
| 35 | Security baseline for the new product | S5 | P0 | M | Ownership rule and rate limit exist. Adds CSRF checks, return-address validation, rate limits for attempts and signals, admin policy tests, security headers. |
| 31 | CI: release images | S7 | P0 | M | Build and test jobs exist; content validation arrives with NEW-2. Adds building and publishing the release images. |
| 33 | Structured logging and request correlation | S7 | P1 | S | Request correlation exists. Structured logs for the API only; there are no workers. |
| 34 | Minimal monitoring: health checks and an uptime alert | S7 | P2 | S | Was metrics dashboards for evaluations. Now an external uptime check on the health endpoints. |
| 41 | Operational runbook and release checklist | S7 | P1 | S | Now for the single-VPS deployment of NEW-17 and NEW-18. |
| 39 | End-to-end test of the learning flow | S8 | P1 | M | Now: signed out solve, sign in, review, progress. |

---

## 5. Create — 19 new issues

All labelled `phase:mvp`, added to the board with Status *Todo*. Bodies follow the same shape as the
rescoped ones: context, scope, out of scope, acceptance criteria, links to the spec.

| Id | Title | Stage | Labels | Priority | Size | Acceptance in one line |
| --- | --- | --- | --- | --- | --- | --- |
| NEW-1 | Remove the previous product's code | S0 | `type:tech-debt` `epic:cleanup` | P0 | L | Tag `pre-diagnosis`; Workspaces, Evaluations, Submissions, Progress, `spikes/`, `xp` and `trust_level` gone; build and tests green |
| NEW-2 | Content format, taxonomy and `content validate` | S1 | `type:feature` `epic:content` | P0 | L | Problems renamed Content; every rule of `CONTENT_FORMAT.md` §7 tested; reference content loads; CI runs validation |
| NEW-3 | Ingest content into PostgreSQL and remove object storage | S1 | `type:feature` `epic:content` | P0 | L | Ingest upserts, unpublishes and retires; overview and shortlist derived; MinIO and the storage client gone |
| NEW-4 | `author-card` skill | S2 | `type:feature` `epic:authoring` | P0 | S | Drafts a valid card from a name, reading the live catalogue |
| NEW-5 | `author-task` skill with the blind smoke test | S2 | `type:feature` `epic:authoring` | P0 | M | Produces a valid material and task; the smoke test's differences from the key are reported |
| NEW-6 | Write the problem catalogue: 55–60 cards | S2 | `type:content` `epic:content` | P0 | XL | 55–60 cards, 8–10 per class, the agent-specific list of SPEC §3.2 included |
| NEW-7 | Attempts module: start, record step, submit, read, history | S3 | `type:feature` `epic:trainer` | P0 | L | SPEC §5.4 and §9.3 attempt endpoints; key from Content by contract; first-attempt rule; ownership in every query |
| NEW-8 | Task screen: material viewer, diagnosis and treatment | S3 | `type:feature` `epic:trainer` `epic:frontend` | P0 | L | SPEC §4.4 on desktop and at phone width; easy tasks show the shortlist |
| NEW-9 | Signed-out solving: sign in on *Check* and return to the task | S4 | `type:feature` `epic:auth` `epic:frontend` | P0 | M | SPEC §4.6: answer kept, same task after sign-in, submitted automatically |
| NEW-10 | Privacy policy page and sign-in notice | S4 | `type:feature` `epic:frontend` | P0 | XS | `/privacy` renders the maintainer's text; the sign-in prompt links it |
| NEW-11 | Signals: "I'm sure it is here" | S5 | `type:feature` `epic:trainer` | P1 | S | SPEC §4.8 from the review, rate-limited, owner only |
| NEW-12 | Admin area: signals, users and attempts | S5 | `type:feature` `epic:admin` | P1 | M | SPEC §6.2; admins from configuration; non-admins get 404 |
| NEW-13 | Landing page | S6 | `type:feature` `epic:public-site` | P1 | S | `/` explains the product and links a demo task and the catalogue |
| NEW-14 | Prerender `/` and `/problems` for search engines | S6 | `type:feature` `epic:public-site` | P1 | M | Both pages' content present in the HTML with JavaScript disabled |
| NEW-15 | Umami analytics and product events | S6 | `type:feature` `epic:measurement` | P1 | S | SPEC §8's events arrive in Umami; no cookies set |
| NEW-16 | Launch prerequisites (maintainer): domain, VPS in Russia, OAuth apps, privacy text | S7 | `type:infra` `epic:deploy` | P0 | S | Domain and VPS exist; GitHub and Google OAuth apps registered; policy text written; operator notification checked |
| NEW-17 | Production deployment on a VPS in Russia | S7 | `type:infra` `epic:deploy` | P0 | L | Caddy with TLS, API, static frontend, PostgreSQL, Umami under Compose on the domain |
| NEW-18 | Release command and backups | S7 | `type:infra` `epic:deploy` | P0 | M | One command migrates, ingests and restarts; daily dump kept seven days, one copy off the server, restore tested |
| NEW-19 | Trial with three people, and calibration | S8 | `type:qa` `epic:qa` | P0 | M | Three people solved the 20 tasks; size bands and scoring parameters updated in SPEC from their attempts |

NEW-16 is assigned to the maintainer.

---

## 6. The board

1. Add the 19 new issues to project 3 with Status *Todo*.
2. For the 36 open issues, set the board's **Priority** and **Size** fields to the values above.
3. **Archive** the 54 closed items from the board, so its Done column shows only work of the new
   product. Archived items stay searchable and can be restored.
4. The 10 issues closed before today stay on the board as they are.

---

## 7. Order of execution

Labels and milestones first; then the 19 new issues, so their numbers exist; then the rescopes,
whose bodies reference the new numbers; then the closures; then the board. Finally, the `NEW-n`
placeholders in `ROADMAP.md` are replaced, this file is deleted, and both land in this pull request.
