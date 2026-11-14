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

`/tasks/{slug}` and its review `/tasks/{slug}/attempts/{id}` (SPEC §4.4–4.5) fill the window. The
layout is **A** of [#198](https://github.com/shoraLBRT/ritocode/issues/198), chosen by the maintainer
from mock-ups on 2026-11-11 (canvas <https://claude.ai/artifact/TVFVsXkjuRFuo59oaDSqP9>: the top row
for step 1, row A for step 2, the review and the phone). The code is read once and the work happens
in the step, so the step gets the room; it replaced a 40/60 split of a sidebar and the code.

- **Header**: the brand (a link home) and the arena's **sections** (Задачи — current, a task being
  in it — Каталог проблем, Прогресс, Админка, as the arena offers them) on the left; **the steps**
  *Диагноз → Лечение → Разбор* between them and the right end, with what is done in each (cards
  picked, findings treated); the theme toggle and the session on the right. The sections came back
  in [#199](https://github.com/shoraLBRT/ritocode/issues/199), reversing #195, so that a finished
  task is one click from the task list and the catalogue; the search is still not shown here. Each
  part is as wide as it needs, so the steps sit between the sections and the session rather than
  at the window's centre — on one line from 75 rem (1200 px) up, which an admin's four sections and
  the session need. Below that the steps take a line of their own under them, centred (900 px), and
  at phone width the header wraps as the arena's does: brand and sections, then the toggle and the
  session, then the steps (375 px). No menu: a section stays one click away.
- **Three columns** (`pages/task/workspace.css`, `.workspace`): a narrow left column for the
  **context** (`ContextPanel`: the title, the difficulty, the agent's brief always, and *Весь
  контекст* for the context itself — open on step 1, folded from step 2 on); **the code in the
  centre**; a narrow right column (`.workspace__aside`) for what the step has gathered — the picked
  cards (step 1), the found problems (step 2) or the verdicts (review).
- **The code** (`MaterialViewer`): the open file's path as a button that opens the project's tree
  (folders, files, a line count each, the project's size); the previous and next files as buttons
  named by them — arrows only when the centre is narrow, so the bar stays one line; a larger-code
  toggle; the code with line numbers.
- **The dock** (`.dock`): the step's action docks at the bottom of the centre, over the code — the
  search field, the filters and the step's button. With results it **rises with the scroll**
  (`dock.ts`): at rest it shows one row of them and about 5% of the next, so it is plain there is
  more; scrolling down the results first lifts the dock, in step with the wheel, trackpad or finger
  — never in one jump — until every result is in sight or about 10% of the code is left (the file
  bar and the first line), then scrolls them; scrolling up, the results go back to their top first
  and then the dock comes down the same way. The lift is `--dock-lift` on the results, added to
  their height; the dock's `max-height` is the bound, so the CSS keeps it. A grip on top raises or
  lowers it all the way by a click; a mouse wheel's tick and the grip are eased over a few frames,
  and snap under `prefers-reduced-motion`. At rest the code scrolls clear of the dock.
  **Every dock can be hidden** (#240): its bar on top holds the grip in the middle and, on the
  right, *Скрыть* (named *Скрыть панель, чтобы читать код*); hidden, the dock is that bar alone —
  the step's question, what the dock holds (the search text and the count, or the finding being
  treated) and *Показать* — so the whole file can be read, with a filter on or not. The controls
  and results stay mounted while hidden: the search, the filters, the picks, the lift and the
  results' scroll come back as they were.
- **Phone width** (≤ 64 rem): the context or the code is behind, by two tabs, and the step is a
  **bottom sheet** (`.workspace__sheet`, fixed): the picks or the findings as a row of chips on top,
  then the dock, rising the same way up to just under the header. The review keeps three tabs: the
  context, the verdicts (before the reveal: the score and the counts), the chosen card (before the
  reveal: the code).

### Step 1 — the matches are cards

- While the field is empty and no class is pressed, the dock is the field, the class filters and
  *К лечению* (or *Здесь чисто* with nothing picked). Text or a class raises it, and the matching
  cards appear **under the field** — the catalogue's card (`article.rc-pcard.rc-pcard--dock`),
  as many 11.25 rem columns as fit (three at 1280 px, four at 1440, two on a phone): the class on
  the band, the name (two lines at most), the rule, the summary (four lines; three on a phone), the
  matched words marked; *Подробнее*, which opens it (it stretches over the card), and its own
  *Отметить* (a real checkbox named by the card). A picked card has an accent outline and is listed
  under *Отмечено* on the right, each with ×. The number of matches, or of cards in the pressed
  classes, ends the filter row.
- **An opened card** (`.dock-open` › `.rc-pcard--peek`) shows what a task may show of a card —
  SPEC §3.2, **no signs, no treatment**: the class, the full summary and the class's description,
  with ‹ n из N › through the matches (← → too) and *Отметить: вижу это в коде*. It opens over the
  centre column only, at the middle, the code dimmed; the side columns stay as they are. ×, Esc or a
  click on the dimmed code closes it; Tab stays inside it; focus returns to the card.
- Arrow keys walk the matches from the field and Enter ticks the one walked to.

### Step 2 — the same dock

- The found problems are compact rows on the right (`.findings`), each with its count of measures
  or *нужна мера*; the current one shows its summary. *Следующая находка →* and *← К диагнозу* are
  under them.
- The dock holds the heading and, under it, the current finding as its subject (`.dock__subject`):
  a card in the finding's class colour (`.k-{class}`, border and tint) with the name large and a line
  saying what the step asks ([#241](https://github.com/shoraLBRT/ritocode/issues/241)); then the treatment search with *Проверить*
  beside it, the hints, the measures ticked for the current finding as chips, the branch filters,
  and the leaves: every branch in columns, each headed by where its measure lives (`.leaf-columns`),
  or — with text or a branch pressed — the matches, each with its branch. It rises the same way.

Search (`src/pages/task/search.ts`) cuts each word of the query to a crude stem, so Russian word
forms match, and ranks by how many stems an option shares, then by where — name, keywords, summary.

### The review

**Before the answer is revealed** ([#229](https://github.com/shoraLBRT/ritocode/issues/229),
[ADR 0017](adr/0017-a-result-without-the-answer.md)) the same frame holds the outcome only: at the
top of the centre the score, a line saying whether the task is solved, and *Попробовать снова* —
still the primary button — then *Следующая задача →* and *Показать ответ* beside it as plain ones
(below); under it the code, to
read again; on the right **the counts drawn small** (`OutcomeTally`, `rc-tally`):

- a **ring** of the findings found out of those present (`--good` on `--surface-sunken`, the number
  in Plex Mono at its centre, «из N» under it) — the progress ring of the arena made smaller;
- a **row** each for found, treated right, not found and extra picks, on the inset surface: the label,
  the number in words on the right, and a **pip** per finding or pick — found filled `--good`, not
  found an outline in `--warn`, treated `--accent`, not treated a hollow grey, extra a square in
  `--danger`. Pips are drawn in one fixed order (found before missed), so they show how many and
  never which; past twelve a row keeps its number only. The drawing is `aria-hidden`; the words
  carry it, and colour never stands alone (a pip's shape or outline tells it apart as well);
- a quiet hint that the answer holds which cards, the mistakes and the author's advice.

*Показать ответ* opens a confirmation inside the score bar, across its width (`.review__reveal`,
focused on opening): «Показать ответ?», what it opens and what it does to the status — «ответ
открыт» from now on, or, for a task solved already, that it stays solved — and *Да, показать* /
*Не сейчас*. No modal: the score stays in sight. *Не сейчас* gives focus back to *Показать ответ*;
*Да, показать* moves it to the score bar of the full review. On a phone the buttons wrap
under each other rather than overflow the bar. The same code in another context, the lesson and
the signal wait for the reveal.

**Once revealed**, the same frame with *Разбор* current: the score with its composition, *Попробовать снова* and
*Следующая задача →* at the top of the centre (`.review__score-bar`); under it the chosen verdict in full — the learner's leaves
against the author's (the shared ones green, the learner's wrong ones red), the author's note, the
whole card, and for an extra pick the signal — or the code again. The verdicts — found, missed,
extra — are listed on the right with the lesson; the same code in another task sits under the
context on the left.

**The next task** ([#199](https://github.com/shoraLBRT/ritocode/issues/199), the maintainer's
decision of 2026-11-12): *Следующая задача →* is a plain button right after *Попробовать снова*, in
the same place before the reveal and after it, so it does not move when *Показать ответ* goes. It
leads to the **next unsolved task in the order of the `/tasks` list** — any status but *solved*, so a
task tried, given up on or never opened (`pages/task/next.ts`). Past the end of the list it carries
on from the start; it never offers the task under review. When every other task is solved there is
**no link**: the header's *Задачи* is the way to the list. A signed-out caller has no statuses, so
for one it is simply the next task in the list. The review reads `GET /tasks` beside its own
requests and does not wait for it — until the list arrives, or if it fails, the link is absent.
From a review, then, one click reaches a new attempt, the next task, and through the header the
task list and the catalogue.

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

- The header, on every page; the task workspace keeps its brand, sections and right end and puts
  its steps where the search would be (above, #199): the brand (the way home), the sections
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
  *Все / Новые / Начатые / Решённые / Ответ открыт* (#229); each tab's count is what it would show
  under the other filter. The status column marks each task (`StatusMark`): new an empty dashed ring,
  tried an amber dot, solved a green check, the answer revealed a quiet grey eye — each named in
  words for a screen reader. The
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
| Status | `.rc-status--done` / `--tried` / `--todo` / `--revealed` | with an `aria-label` in words; the `StatusMark` component |
| Outcome tally | `.rc-tally` › `__ring`, `__hole`, `__rows`, `__row`, `__label`, `__value`, `__pips`, `__pip--found` / `--missed` / `--treated` / `--untreated` / `--extra` | the review's counts before the reveal (above); `pages/task/OutcomeTally.tsx` |
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
| Workspace dock | `.dock` (`--cards`, `--leaves`, `--results`, `--raised`, `--collapsed`) › `__bar` (`__grip`, `__summary`, `__collapse`), `__controls`, `__filters`, `__results`, `__cards`; `.workspace__aside`; `.workspace__sheet` | the task screen's step at the bottom of the code (above); `pages/task/workspace.css` |
| Card in the dock | `article.rc-pcard.rc-pcard--dock.k-{class}` › `__foot`, `__open`, `label.rc-pcard__pick`; opened: `.dock-open` › `.rc-pcard--peek` | the catalogue's card made small; the opened one shows no signs or treatment (SPEC §3.2) |

The list shows no number of findings: a clean task would read as «0» and give itself away. Classes
never appear on the task list (SPEC §4.3) — the concept's opt-in toggle for them was turned down in #190.
