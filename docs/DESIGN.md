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
| `--page` | `#e6e7ee` | `#0b0b10` | the page behind the cards |
| `--surface` | `#ffffff` | `#1d1d28` | a card or panel raised on the page; the header |
| `--surface-inset` | `#f1f2f6` | `#14141c` | an area set into a card: fields, the search, table heads, notices |
| `--surface-pop` | `#ffffff` | `#252531` | a popup over everything: menus, the search list, the file tree |
| `--surface-sunken` | `#e2e3eb` | `#2b2b38` | the fill of a track, a tag or a hovered row; segmented controls |
| `--text` / `--text-muted` / `--text-faint` | `#18181f` / `#4c4c5a` / `#5e5e70` | `#e7e7ef` / `#a9a9ba` / `#9393a6` | text, secondary, hints |
| `--border` / `--border-strong` | `#dcdce5` / `#c3c3d0` | `#2e2e3b` / `#45455a` | lines, control outlines |
| `--accent` / `--accent-ink` / `--accent-soft` | `#4a4fb0` / `#fff` / `#e6e7f7` | `#9ea3f5` / `#11122c` / `#232448` | primary actions, the current step, the brief |
| `--good` / `--warn` / `--danger` (+ `-surface`) | green / amber / clay | lighter versions | found / missed / extra, errors |
| `--hit` | `#fbe3a0` | `#4a3d14` | the words a search matched |
| `--code-*` | — | — | the code panel (on `--code-surface`, an inset level) and its highlighting |
| `--spotlight` / `--spotlight-ink` / `--spotlight-muted` | `#25275e` / `#fff` / `#c9cbf5` | `#2b2d70` / `#fff` / `#c9cbf5` | the one spotlight card on a page (the daily task) |
| `--spotlight-raised` / `--spotlight-inset` | white 12 % / black 28 % | the same | a chip and the code on the spotlight |
| `--shadow` / `--shadow-pop` / `--shadow-control` | soft grey | black, with a faint light top edge | a card, a popup, a pressed tab or a toggle's knob |
| `--heat-1` … `--heat-4` | `#cfd1f2` … `#4a4fb0` | `#2a2c5a` … `#9ea3f5` | the activity map, above `--surface-sunken` for none |

**Surfaces step apart without borders** ([#197](https://github.com/shoraLBRT/ritocode/issues/197)).
In light the page is a visible grey under white cards; in dark the levels rise from the page through
the inset, the card and the popup to the sunken fill, each a step lighter, and a raised card or popup
carries a faint light top edge. Every text colour meets WCAG AA (4.5:1) on every surface, and each
surface is apart from the one it sits on — `src/theme/contrast.test.ts` reads the tokens from the
stylesheet and checks both. A new surface or text token goes into that test.

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
  session on the right. The sections and the search are not shown here.
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

### Chosen next: layout A ([#198](https://github.com/shoraLBRT/ritocode/issues/198))

The maintainer chose this layout from mock-ups on 2026-11-11; it replaces the 40/60 layout above
once #198 is built (canvas <https://claude.ai/artifact/TVFVsXkjuRFuo59oaDSqP9>: the top row for step
1, row A for step 2, the review and the phone). The code is read once and the work happens in the
step, so the step gets the room.

- **Three columns.** A narrow left column for the context (the title, the difficulty, the brief,
  *Весь контекст*); **the code in the centre**; a narrow right column for what is picked (step 1)
  or the found problems, a row each with the current one's summary (step 2), or the verdicts
  (review).
- **The step's action docks at the bottom of the centre**, over the code: the search field, the
  filters (classes at step 1, treatment branches at step 2) and the step's button.
- **Step 1 — the matches are cards** (the catalogue's card, #202), never a list over the code.
  While the field is empty and no filter is on, the dock is the field and the filters. Text or a
  filter raises it, and the matching cards appear **under the field**, four to a row, each with
  *Подробнее* and its own *Отметить*. At rest the dock is the field + the filters + one row of cards
  + about 5% of the next row, so it is plain there is more. **Scrolling down the cards first lifts
  the dock** until about 10% of the code is left (the file bar and the first line), then scrolls the
  cards; scrolling up from the top lowers it again; a grip on top does the same by a click.
- **An opened card** shows what a task may show of a card (SPEC §3.2 — no signs, no treatment):
  the class, the full summary and the class's description, with ‹ n из N › and *Отметить: вижу это в
  коде*. It opens over the centre column only, at the middle, and closes like the catalogue's.
- **Step 2** keeps the same frame: the dock holds the treatment search, the branch filters and the
  leaves in columns, rising the same way; the found problems are compact rows on the right.
- **The review** keeps the frame: the score with *Попробовать снова* and *Следующая задача* at the
  top of the centre (#199), the verdict in full under it, the verdicts listed on the right.
- **Phone**: the code behind, the step as a bottom sheet.

## The arena

The home page and, after it, the catalogue, the task list and progress
([ADR 0015](adr/0015-arena-home-and-junior-audience.md),
[#190](https://github.com/shoraLBRT/ritocode/issues/190)). It looks and works like LeetCode: the
audience — junior developers who build with AI agents — recognises a trainer at first sight. The
components can be seen live in the design system <https://claude.ai/artifact/R4Btj6eR7gBo1MENTdJHyf>;
its token names are its own, and where it differs from this document, this document wins.

### Voice

- The learner is «ты», as a colleague on a review: «Решить задачу», «Что ты видишь в коде?». The
  whole interface speaks this way since #217; only the privacy policy's own text keeps its register.
- No gendered past tense: «Найдено 2 из 3», «Задача решена» — never «ты нашёл».
- A button says what happens. Problem and class names are the catalogue's, word for word; slugs show
  in Plex Mono where a developer benefits from seeing them.
- Real numbers only — 56 problems, 6 classes, 20 tasks from the content; never an invented crowd.

### The home page

- The header, on every page but the task workspace
  ([#195](https://github.com/shoraLBRT/ritocode/issues/195)): the brand (the way home), the sections
  (Задачи, Каталог проблем; Прогресс for a learner, Админка for an admin), the search, the theme
  toggle, then the streak and the avatar — or *Войти* and *Начать* (the demo task) for a guest.
  - **The search** («Задачи и проблемы», `Ctrl K` / `⌘K` from anywhere) is a combobox: as the
    learner types, up to five tasks by title and five problems by name, keywords and summary, ranked
    by the task screen's search, in two groups. Arrow keys move, Enter opens the task or the
    problem's anchor in `/problems`, Escape closes. The lists are read on the first focus.
  - **The streak** is «N дн.», named in full for a screen reader; read again on each page change.
  - **The avatar** is the username's first letter; it opens a menu with the username and *Выйти*.
    *Войти* opens the providers with the sign-in notice, as before.
  - At phone width the header wraps: brand and sections, then the toggle and the session, then the
    search on a line of its own, without the shortcut. The design system's menu for the sections
    and the search folded to an icon were not needed at 375 px.
- A row of two: a card with the lead and the real counts (`rc-stat`), and the **daily task**
  (`rc-daily`) — the page's only `--spotlight`. Its top line is the label and a countdown to Moscow
  midnight (`rc-daily__countdown`, a `<time>` ticking each second, not a live region); at zero the
  next day's task takes the card. The countdown comes with the task, so the prerendered page, which
  has no tasks, never shows a time fixed at build.
- Below, two panels: **tasks for you** (`rc-panel` › `rc-task-table`, six tasks, no tabs, *Все
  задачи →*) and **problems to read** (`rc-arena__problems` › `rc-arena__problem.k-{class}`: four
  cards in a grid, the class colour as a top edge and in the class chip, the name and the summary,
  *Все проблемы →*). Beside them an
  aside of 300 px — **progress by class** (`rc-ring`, `rc-bar`) and **activity** (`rc-heat`) — which
  drops under the table at phone width.
- Signed out, the same page: the personal parts give way to a sign-in prompt. The hero's first
  button opens the easy task the page offers: «Начать с лёгкой задачи», not «демо» — any task can
  be worked signed out.

### The task list

`/tasks` ([#193](https://github.com/shoraLBRT/ritocode/issues/193)) is the home page's task table at
full length:

- A heading and the lead above the panel (`rc-page-head`), no hero card and no aside.
- The panel's bar holds the filters (`rc-list-filters`): difficulty tabs, and for a signed-in learner
  *Все / Нерешённые / Решённые*; each tab's count is what it would show under the other filter. The
  number of tasks shown sits at the bar's right end.
- Every task, in the API's order, in the same table as the home page — one component,
  `components/TaskTable.tsx`. Classes and the number of findings never appear, as on the home page.
- The filters live in the address (`?difficulty=hard&status=unsolved`), so the way back from a task
  keeps them; the home page's link to the list carries its difficulty tab.
- On a phone the tab groups wrap, and the table loses its time column like the home page's.

### The problem catalogue

`/problems` ([#202](https://github.com/shoraLBRT/ritocode/issues/202)): problems **as cards**, the
maintainer's direction of 2026-11-10 (artboards «#202 C» on the canvas
<https://claude.ai/artifact/HLpnMneZaDeENR3kSFyPup>).

- The page head (`rc-page-head`), then a panel (`rc-catalogue__bar`) with the search, the number of
  cards shown, and the **class filter** — *Все* and the six classes, each with its count; one class
  at a time, pressed again it shows all.
- Each class: its name with a dot in its colour and its description, then its cards in a grid
  (`rc-catalogue__grid`, as many 15.5 rem columns as fit; two on a phone).
- **A folded card** (`article.rc-pcard.k-{class}`, at least 360 px tall) looks like a card: the
  class's tint is its frame; the band on it carries the class name and a round seal in the class's
  colour, which is the card's anchor (`#slug`); on the white face, the name, a short rule in the class
  colour and the summary (Markdown, clamped to six lines), and *Открыть*. The whole card is one target:
  the `<summary>` of the `<details>` that holds the sections stretches over it. On hover it lifts and
  tilts a little.
- **Open**: the page dims (`--overlay`), and the card grows out of its place to the centre — a FLIP
  animation with the Web Animations API, none under `prefers-reduced-motion` — while its place stays
  as an outline (`rc-pcard--lifted`). The open card (`rc-pcard--open`, up to 860 px wide, as tall as
  the window allows and scrolling inside) is a modal dialog in the same frame: the class and
  `#slug` on the band, the name, the summary and every section in two columns. ×, Esc and a click
  outside close it; Tab stays inside it; focus returns to the card. The address follows it:
  `#slug` while it is open, none after.
- **Without JavaScript** the `<details>` unfolds in place, so the prerendered page reads in full.

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
| Search | `label.rc-search` › `input`, `.rc-kbd` | the header's search over tasks and problems (`HeaderSearch`), its list in `.app__search-popup` |
| Difficulty | `.rc-difficulty--easy` / `--medium` / `--hard` | always a word, on the state colours |
| Problem class | `.rc-class.k-{class}` | the class colour is for classes only; never on a task |
| Status | `.rc-status--done` / `--tried` / `--todo` | with an `aria-label` in words |
| Stat | `.rc-stat` › `.rc-stat__value`, `.rc-stat__label` | real counts only |
| Task table | `table.rc-task-table`, `__title`, `__time`, `__number` | status (signed in) · title · time · difficulty; the time column goes on a phone; scrolls in `.table-scroll`. The `TaskTable` component |
| List page head | `.rc-page-head` › `h1`, `p` | a list page's heading and lead, above its panel |
| List filters | `.rc-list-filters` › `.rc-tabs` | the groups of tabs in a list panel's bar, wrapping on a phone |
| Daily task | `.rc-daily` › `__top`, `__title`, `__meta`, `__chip`, `__code`, `__action` | one per page |
| Progress | `.rc-ring` (`--p`), `.rc-ring__hole`, `__value`, `__of`; `.rc-bar.k-{class}` (`--p`) | `--p` is a percentage, 0–100 |
| Activity | `.rc-heat` › `.rc-heat__cell[data-level=0–4]` | a column per week; `aria-hidden`, the streak says it in words |
| Streak, avatar | `.rc-streak`, `.rc-avatar` | the header's right end (`HeaderStreak`; the avatar opens `.app__menu`) |
| Problem card | `article.rc-pcard.k-{class}` › `__band`, `__seal`, `__face`, `__name`, `__rule`, `__summary`, `details.rc-pcard__more`; open: `.rc-pcard--open` in `.rc-pcard-overlay` | the catalogue's card, folded and open (above) |
| Class filter | `.rc-catalogue__filter` › `button.rc-catalogue__chip.k-{class}[aria-pressed]` | one class or all, with counts |

The list shows no number of findings: a clean task would read as «0» and give itself away. Classes
never appear on the task list (SPEC §4.3) — the concept's opt-in toggle for them was turned down in #190.
