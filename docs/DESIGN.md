# Design

The face of Ritocode: what it looks like, how the task screen is laid out, and the arena the other
pages are built from. The decisions and what was turned down are in
[ADR 0014](adr/0014-visual-identity-and-task-workspace.md) (look, task screen) and
[ADR 0015](adr/0015-arena-home-and-junior-audience.md) (the arena, the audience); the concepts they
were chosen from are on the maintainer's design canvases (artboards «Ж» and «D · Арена»). This
document is the reference for building any page — read it before adding a colour, a font size or a
component.

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
| `--spotlight` / `--spotlight-ink` / `--spotlight-muted` | `#25275e` / `#fff` / `#c9cbf5` | `#2b2d70` / `#fff` / `#c9cbf5` | the one spotlight card on a page (the daily task) |
| `--heat-1` … `--heat-4` | `#cfd1f2` … `#4a4fb0` | `#2a2c5a` … `#9ea3f5` | the activity map, above `--surface-sunken` for none |

Radii: `--radius` 10 px for controls, `--radius-card` 14 px for cards, `--radius-panel` 18 px for a
page's large panels (the task table, the daily task).

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

## The arena

The home page and, after it, the catalogue, the task list and progress
([ADR 0015](adr/0015-arena-home-and-junior-audience.md),
[#190](https://github.com/shoraLBRT/ritocode/issues/190)). It looks and works like LeetCode: the
audience — junior developers who build with AI agents — recognises a trainer at first sight. The
components can be seen live in the design system <https://claude.ai/artifact/R4Btj6eR7gBo1MENTdJHyf>;
its token names are its own, and where it differs from this document, this document wins.

### Voice

- The learner is «ты», as a colleague on a review: «Начать диагноз», «Подобрать задачи».
- No gendered past tense: «Найдено 2 из 3», «Задача решена» — never «ты нашёл».
- A button says what happens. Problem and class names are the catalogue's, word for word; slugs show
  in Plex Mono where a developer benefits from seeing them.
- Real numbers only — 56 problems, 6 classes, 20 tasks from the content; never an invented crowd.

### The home page

- The header: the brand, the sections (Задачи, Каталог проблем, Прогресс), the search, then the
  streak and the avatar — or *Войти* and *Начать* for a guest.
- A row of two: a card with the lead and the real counts (`rc-stat`), and the **daily task**
  (`rc-daily`) — the page's only `--spotlight`.
- Below, the **task table** (`rc-panel` › `rc-task-table`) with difficulty tabs, and beside it an
  aside of 300 px — **progress by class** (`rc-ring`, `rc-bar`) and **activity** (`rc-heat`) — which
  drops under the table at phone width.
- Signed out, the same page: the personal parts give way to a sign-in prompt and the demo task.

### Components

Everything the arena adds is a class prefixed `rc-` in `frontend/src/styles.css`, on the tokens
above. What the site already had stays as it is.

| Component | Classes | Notes |
| --- | --- | --- |
| Button | `.button`, `.button--primary`, `.button--small`; `.rc-daily__action` on the spotlight | shared, not `rc-` |
| Code | `.code`, `.code__line`, `.code__token--*` | shared |
| Header, brand, nav | `.app__header`, `.app__mark`, `.app__nav-link` | shared |
| Panel | `.rc-panel`, `.rc-panel__bar` | a page's large card: the table with its toolbar |
| Segmented tabs | `.rc-tabs` › `button.rc-tab[aria-pressed]`, `.rc-tab__count` | a filter of 2–5 options, such as difficulty |
| Toggle | `button.rc-toggle[aria-pressed]` › `.rc-toggle__track` | a display setting that applies at once |
| Search | `label.rc-search` › `input`, `.rc-kbd` | the header's search over tasks and problems |
| Difficulty | `.rc-difficulty--easy` / `--medium` / `--hard` | always a word, on the state colours |
| Problem class | `.rc-class.k-{class}` | the class colour is for classes only; never on a task |
| Status | `.rc-status--done` / `--tried` / `--todo` | with an `aria-label` in words |
| Stat | `.rc-stat` › `.rc-stat__value`, `.rc-stat__label` | real counts only |
| Task table | `table.rc-task-table`, `__title`, `__time`, `__number` | status (signed in) · title · time · difficulty; the time column goes on a phone; scrolls in `.table-scroll` |
| Daily task | `.rc-daily` › `__top`, `__title`, `__meta`, `__chip`, `__code`, `__action` | one per page |
| Progress | `.rc-ring` (`--p`), `.rc-ring__hole`, `__value`, `__of`; `.rc-bar.k-{class}` (`--p`) | `--p` is a percentage, 0–100 |
| Activity | `.rc-heat` › `.rc-heat__cell[data-level=0–4]` | a column per week; `aria-hidden`, the streak says it in words |
| Streak, avatar | `.rc-streak`, `.rc-avatar` | the header's right end |

The list shows no number of findings: a clean task would read as «0» and give itself away. Classes
never appear on the task list (SPEC §4.3) — the concept's opt-in toggle for them was turned down in #190.
