# Domain Model

The entities that exist in the code, and which module owns each. The physical schema, indexes and
constraints are in [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md); this document is the conceptual view.
The entities still to come — signals — are described in [SPEC.md](SPEC.md) §4.8 and join this file
as they are built.

Every entity is owned by exactly one module. An entity is only ever read or written through its
owning module — see [ADR 0002](adr/0002-modular-monolith-layout.md).

## User

Owned by the **Users** module. A platform account.

Fields:

- id
- email — stored lower-cased, unique
- username — stored lower-cased, unique
- created_at

## LinkedAccount

Owned by the **Auth** module. Links a Ritocode account to an external identity.

Fields:

- id
- user_id — the Ritocode user
- provider — `GitHub` today; Google arrives with sign-in
  ([#7](https://github.com/shoraLBRT/ritocode/issues/7))
- provider_user_id — the provider's immutable identifier, unique per provider
- provider_login — last known login at the provider, for display only, may be stale
- linked_at

The immutable id is what identifies the account; logins get renamed and must not silently detach an
account.

## Content

Owned by the **Content** module. Written only by ingest, from `content/` in the repository
([CONTENT_FORMAT.md](CONTENT_FORMAT.md)); read by everything else. Every row carries the commit it
was loaded from (`content_revision`). Localised text and the lists an item holds are stored as JSON
beside the columns: content is read whole, by slug, and never queried by a field inside it.

- **Taxonomy** — the one set of classes and the treatment tree, with their labels per locale.
- **Card** — a problem card: slug (permanent), class, weight (1–3), text per locale. A card that
  leaves `content/` is **retired**, never deleted, because attempts name it.
- **Material** — slug, language, its files, and an **overview** derived at ingest: files with line
  counts, total lines, declared dependencies.
- **Task** — slug, material, difficulty, **findings** (the answer key: a card and the leaves right
  for it), text per locale, and for an easy task the **shortlist** of cards offered in step 1 —
  its findings plus up to 20 others, chosen deterministically from its slug. A task that leaves
  `content/` is **unpublished**, never deleted. The findings never leave the server except inside a
  submitted attempt.

## Attempt

Owned by the **Attempts** module. One learner's answer to one task, and its result
([SPEC.md](SPEC.md) §5.4): the user, the task's slug, when it started, the furthest **step** reached
(diagnosis or treatment — the journal of §8), and, once **submitted**, the answer, the result with
the key revealed, the author's notes and lesson for the review, the score and its maximum, and the
content revision it was scored against. A
submitted attempt never changes again: a later change to the task, a card or the scoring parameters
does not rewrite it. The **first submitted** attempt at a task counts toward progress; every later
one is **practice**. The task and its key reach Attempts through `ITaskForAttemptLookup`, which
Content answers; the task catalogue's solved flags reach Content through `ISubmittedTaskLookup`,
which Attempts answers.

## Scoring

Owned by the **Attempts** module and stored in an attempt's result. Scoring is a pure function of an
**answer** (the picked cards, each with its leaves), the **answer key** with each card's weight, and
the **scoring parameters** (SPEC §5.2, configured under `Attempts:Scoring`). It returns the total,
floored at zero, the maximum, whether the answer is **correct** — nothing lost, which on a clean task
means nothing picked — and one line per card: **found** with its treatment (the picked leaves that
match the key, the ones that do not, and the key's own), **missed**, or **extra**. The order of
picks and leaves never changes the result.

## Progress

Owned by the **Attempts** module and never stored: computed on read from the scored results of a
user's **first** attempts ([SPEC.md](SPEC.md) §4.7), so practice cannot move it and a result that
never changes gives a progress that never needs rewriting. Per class — findings met, found, and
found ones treated right — and per card — met, found, missed, picked when absent, treated right. The
class of a card, and the names of classes and cards in the default locale, come from Content through
`ICardClassLookup`; a retired card keeps its name, and a card Content does not know is named by its
slug and has no class.
