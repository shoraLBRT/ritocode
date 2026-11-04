# 0014 — Visual identity and the task workspace

- Status: Accepted
- Date: 2026-10-27
- Relates to: [#188](https://github.com/shoraLBRT/ritocode/issues/188)
- Builds on: [SPEC.md](../SPEC.md) §4.4–4.5, §9.4; [DESIGN.md](../DESIGN.md)

## Context

Until now the frontend had no design system: `styles.css` said so itself and kept to "enough style
to read the shell". The task screen was three equal columns — context, code, answer — and step 1 was
the whole catalogue as a list of checkboxes, which taught recognition by scrolling rather than
describing what one sees.

The maintainer chose the face of the product and the flow of the task screen over three rounds of
clickable concepts on a design canvas (2026-10-18 to 2026-10-27): first the layout and the flow,
then three unlike styles, then palettes over the chosen shape. This record keeps what was chosen
and what was turned down, so a later session does not reopen it by accident.

## Decision

**Look.** The *Indigo* palette — an indigo accent on a cool grey ground — with **IBM Plex Sans and
IBM Plex Mono**, rounded cards with a soft shadow. **The light theme is the default for everyone**;
a dark theme exists and is the learner's choice, remembered by the browser. Nothing follows the
system's preference on its own. Each of the six problem classes keeps its own muted colour, used
wherever a class is shown. The tokens are in [DESIGN.md](../DESIGN.md).

**Fonts are served with the site** (`@fontsource/ibm-plex-sans`, `@fontsource/ibm-plex-mono`), not
from a font CDN: no third-party request carrying the visitor's address, nothing to add to the
content policy, and the Cyrillic subsets are included.

**The task screen is a workspace.** The header holds the brand and, at its centre, the steps
*Диагноз → Лечение → Разбор*; the navigation steps aside. A sidebar of 40% holds the context —
the agent's brief always, the rest on expanding — and the step at hand; the code takes the other
60%, with a file picker that opens the project's tree and buttons to the neighbouring files, named
by them. The review is the same workspace with its third step current.

**Search first.** Step 1 starts from a field where the learner describes what they see in their
own words; the cards that share a word come up as they type, ranked, the matched words marked.
Picks show as chips in the class's colour; class chips narrow the options. Browsing the whole
catalogue stays possible but is folded and muted — reachable, not the way in. Step 2 works the same
way over the treatment leaves, for one finding at a time, with the list of findings and their
state above it. The button beside each field is the step's own action: on to treatment, or check.

## Alternatives

- **Styles turned down:** a monospaced, brutalist "terminal" look; a Swiss editorial look with a
  red-orange accent; the original teal-green; a clinical blue; ink-and-brass colour blocking; a
  pastel look with a raspberry accent. Palettes tried on the chosen shape and not taken: sage,
  plum, petrol, amber, monochrome.
- **Picks shown inside the search field as tokens**, or in a tray at the bottom of the sidebar:
  chips under the field read better and keep the field short.
- **Treatment as an accordion of all findings at once**: too cramped at 40% with a search in each.
- **Following the system's colour scheme by default**: the light theme is the one the design is
  made for; dark is offered, not imposed.
- **A font CDN**: a request to a third party on every page, for no gain over serving the files.

## Consequences

- The dependencies shown on the old material overview are gone from the screen; the overview
  (files and lines) lives in the file tree. The API still serves them.
- A treatment leaf says where its measure lives through the five branch descriptions, which are
  interface text (the tree is fixed product structure, SPEC §3.3). Search over the leaves matches
  their label, their branch and that description; it has no synonyms of its own, so "права" finds
  less than it could — keywords on the leaves would be a content change (CONTENT_FORMAT) and are
  left for later.
- A learner who chose the dark theme sees the prerendered pages light until the script runs.
- Other pages took the new tokens and fonts as they are; their own layouts are not redesigned here.
