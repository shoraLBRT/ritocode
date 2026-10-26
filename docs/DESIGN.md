# Design

The face of Ritocode: what it looks like and how the task screen is laid out. The decision and
what was turned down are in [ADR 0014](adr/0014-visual-identity-and-task-workspace.md); the concepts
it was chosen from are on the maintainer's design canvas (artboard «Ж»). This document is the
reference for building any page — read it before adding a colour, a font size or a component.

## Look

- **Palette: Indigo.** An indigo accent on a cool grey ground; white cards on a grey page.
- **Type: IBM Plex Sans** for text, **IBM Plex Mono** for code, paths and numbers in columns.
  Served with the site (`@fontsource`), weights 400–700 for Sans, 400–600 for Mono.
- **Shape:** rounded cards (14 px) with a soft shadow, controls rounded 10 px, chips and the steps
  fully round.
- **Theme:** light by default for everyone; dark is a choice made with the toggle in the header and
  kept in the browser (`src/theme`). The root carries `data-theme="dark"`; nothing else changes.

## Tokens

Every colour is a CSS custom property in `frontend/src/styles.css`, redefined for the dark theme.
A component uses tokens only — never a raw colour.

| Token | Light | Dark | Used for |
| --- | --- | --- | --- |
| `--page` | `#f1f1f4` | `#0f0f14` | the page behind the cards |
| `--surface` | `#ffffff` | `#16161d` | cards, panels, fields |
| `--surface-muted` | `#f7f7fa` | `#13131a` | a field at rest |
| `--surface-sunken` | `#e9e9ef` | `#1f1f29` | hover, segmented controls, tags |
| `--text` / `--text-muted` / `--text-faint` | `#18181f` / `#4c4c5a` / `#6c6c80` | `#e7e7ef` / `#a7a7b8` / `#858598` | text, secondary, hints |
| `--border` / `--border-strong` | `#e0e0e8` / `#c3c3d0` | `#282834` / `#393948` | lines, control outlines |
| `--accent` / `--accent-ink` / `--accent-soft` | `#4a4fb0` / `#fff` / `#e6e7f7` | `#9ea3f5` / `#11122c` / `#232448` | primary actions, the current step, the brief |
| `--good` / `--warn` / `--danger` (+ `-surface`) | green / amber / clay | lighter versions | found / missed / extra, errors |
| `--hit` | `#fbe3a0` | `#4a3d14` | the words a search matched |
| `--code-*` | — | — | the code panel and its highlighting |

**The six problem classes** each have a colour and a tint (`--class-{id}`, `--class-{id}-tint`). An
element of a class carries `k-{class}` and reads them as `--k` and `--ks`: the filter chips, the
chips of picked cards, the option tags, the class badge on a card in the review.

| Class | Light | Dark |
| --- | --- | --- |
| disproportion | `#a0661a` | `#deaa5c` |
| project-knowledge | `#3f62a8` | `#8fadea` |
| hygiene | `#b4483a` | `#ec8f7e` |
| growth | `#7650a8` | `#b9a0e6` |
| false-confidence | `#a34877` | `#e693bd` |
| domain | `#2f7d5a` | `#77c9a0` |

Colour never carries meaning alone: an outcome has a mark and a word, a class its name.

## The task workspace

`/tasks/{slug}` and its review `/tasks/{slug}/attempts/{id}` (SPEC §4.4–4.5) fill the window:

- **Header**: the brand (a link home) on the left; **the steps** *Диагноз → Лечение → Разбор* at
  the centre, with what is done in each (cards picked, findings treated); the theme toggle and the
  session on the right. The site navigation is not shown here.
- **Sidebar, 40%**: the **context** card — the title, the agent's brief always, and on expanding the
  path, difficulty and the context itself (open on step 1, folded from step 2 on) — and below it
  **the step at hand**.
- **Code, 60%**: the open file's path as a button that opens the project's tree (folders, files, a
  line count each, the project's size); the previous and next files as buttons named by them; a
  larger-code toggle; the code with line numbers.
- **Phone width** (≤ 64 rem): the context, the code and the answer become three tabs.

### A step

1. A heading and a count.
2. **The search row**: a field, and beside it the step's action — *К лечению* (or *Здесь чисто*
   with nothing picked) at step 1, *Проверить* at step 2.
3. What is picked, as removable chips under the field.
4. Filter chips — the classes at step 1, the treatment branches at step 2 — any number on.
5. The options: ranked matches with the matched words marked, or — with no query and no filter —
   the catalogue folded by group, muted. Searching is the way in; browsing is the way round.

Step 2 adds above the field the list of picked findings, each with its count of measures or
*нужна мера*; the current one shows its summary. Each leaf says where its measure lives.

Search (`src/pages/task/search.ts`) cuts each word of the query to a crude stem, so Russian word
forms match, and ranks by how many stems an option shares, then by where — name, keywords, summary.

### The review

The same frame with *Разбор* current. The sidebar holds the score, its composition, the verdicts —
found, missed, extra — as a list, the lesson, the same code in another task and *Try again*. Beside
it, the chosen verdict in full — the learner's leaves against the author's (the shared ones green,
the learner's wrong ones red), the author's note, the whole card, and for an extra pick the
signal — or the code again.
