# 0015 — The home page is an arena, and the audience is junior developers

- Status: Accepted
- Date: 2026-10-28
- Relates to: [#190](https://github.com/shoraLBRT/ritocode/issues/190)
- Builds on: [ADR 0014](0014-visual-identity-and-task-workspace.md); [CONCEPT.md](../CONCEPT.md) §2;
  [SPEC.md](../SPEC.md) §2.1, §4.1; [DESIGN.md](../DESIGN.md)

## Context

`/` was the text landing of #131: a heading, a lead, three buttons and five paragraphs taken from
CONCEPT.md. The maintainer found it plain and asked for a page that would make a developer stop.

Over 2026-10-27 and 2026-10-28 the maintainer reviewed six directions on a design canvas
(<https://claude.ai/artifact/QmurqqRgKHmYEiNMYEcyuy>). While choosing, the audience itself changed.
The concept named **vibe coders** — people who build by instructing agents and know little about
architecture. But a task is reading code: the learner gets a small Python project and says what is
wrong with it. Someone who cannot read code cannot solve a task at all. The people who can — and
who need the product — are junior developers who already learn and work through AI agents: they can
read what an agent wrote, they rarely read all of it, and they have no background yet to see what
it will cost.

## Decision

**The audience is junior developers who build with AI agents.** They read code; they do not yet
read architecture. The content still starts from the basics, but the basics of design, not of
programming: a card is written for someone who has never heard the word "coupling", not for
someone who has never seen a function. CONCEPT.md §2 and SPEC.md §2.1 say so from this change on.

**The home page is an arena** — artboard «D · Арена». It looks and works like LeetCode, because
that is the trainer this audience already knows: a daily task, the task table with difficulty and
status, progress by problem class, activity. The page is the product, not a description of it.
What exactly the daily task, the streak and the activity are is left to
[#190](https://github.com/shoraLBRT/ritocode/issues/190), which updates SPEC §4.1 before the page
is built.

**The arena is the design code for every page.** It extends ADR 0014 rather than replacing it: the
same Indigo tokens, IBM Plex, light by default. It adds a few tokens (the `spotlight` card, the
activity scale, difficulty as aliases of the state colours) and a set of component classes prefixed
`rc-`, written down in DESIGN.md › The arena. The catalogue, the task list and the progress page are
to be rebuilt from them, each in its own issue. The design-system artifact
<https://claude.ai/artifact/R4Btj6eR7gBo1MENTdJHyf> shows the components live; where it and
DESIGN.md differ — token names, the card shadow — DESIGN.md wins.

**Voice: «ты».** The interface addresses the learner informally, as a colleague on a review, and
avoids gendered past-tense forms («найдено 2 из 3», not «ты нашёл»).

## Alternatives

- **A · Live review.** A dark editor with the demo code in the hero; the visitor clicks the lines
  where they see a problem. Strong first second, but a landing, not a product; its interaction may
  return inside the arena's hero.
- **B · Manifesto.** An editorial page in huge capitals on a pale ground. Liked for its look; B2
  rebuilt it as a sales page around a pain («Агент говорит "готово"…»), which was too salesy and
  not technical enough for developers; B3 made it technical («Тесты зелёные. PR вернули.»). D was
  preferred over both: a developer trusts a working trainer more than a pitch. B3's lines are kept
  as candidates for the arena's hero copy.
- **C · Periodic table.** The 56 cards as a periodic table coloured by class. Too complex for the
  home page; kept as an idea for the problem catalogue.
- **Keeping vibe coders as the audience.** Rejected: the product's only activity is reading code.

## Consequences

- CONCEPT.md §2, SPEC.md §2.1, the reading-speed note of SPEC §3.4 and decision 1 of SPEC's
  decision log now name junior developers. The cards and tasks are not rewritten: they already start
  from design basics, and the reading-speed estimates were to be calibrated anyway.
- The concept drew an opt-in "show classes" toggle in the task table. SPEC §4.3 forbids class tags
  on tasks, and the maintainer kept that rule (#190): the arena's task list has no classes.
- A daily task, a streak and activity are new features, specified in SPEC §4.1 and §4.7 (#190):
  the daily task changes at midnight Moscow time and prefers unsolved tasks; a day without an
  attempt breaks the streak. They read attempt dates the Attempts module already keeps; no schema
  change is foreseen.
- `/` must stay readable without JavaScript (SPEC §4.1): the prerendered arena shows the lead, the
  catalogue's counts and how a task goes; the tasks and the personal parts are filled in by the
  application, since the content export carries no tasks.
- The existing interface text says «вы». It moves to «ты» page by page, as each page is rebuilt.
