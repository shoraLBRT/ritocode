# Ritocode frontend

React + Vite + TypeScript. This is the application shell and the API client layer from
[#26](https://github.com/shoraLBRT/ritocode/issues/26) — the frame the product screens are built
into, not the product screens themselves: the translation catalogue, who is signed in, and a layout
that works at phone width.

## Running it

Node 22.22 or newer — that floor is `engines.node` in `package.json`, and `.nvmrc` pins the line
CI and `nvm use` actually resolve to. The backend has to be running for anything but the not-found
page to have content; `docs/PROJECT_STATE.md` has the commands, and a Development host is the one
that seeds a problem to browse.

```bash
npm install
npm run dev
```

The dev server binds port 5173 with `strictPort`, because that exact origin is what the API's
`appsettings.Development.json` allows through CORS. Changing the port here without changing it
there makes every request fail in the browser and succeed from `curl`.

`VITE_API_BASE_URL` says where the API lives, and `VITE_DEMO_TASK` which easy task the landing page
offers; see `.env.example` for the defaults. Both are read at build time and inlined, so a change
needs a rebuild.

## Scripts

| Command | What it does |
| --- | --- |
| `npm run dev` | Vite dev server on <http://localhost:5173> |
| `npm run build` | Typecheck, then a production bundle in `dist/` |
| `npm run build:static` | `build`, then `/` and `/problems` prerendered into it — see below |
| `npm run typecheck` | `tsc --build` over the app and the tooling config |
| `npm run lint` | ESLint, type-aware rules included |
| `npm test` | Vitest, once |
| `npm run test:watch` | Vitest, watching |

`.github/workflows/frontend-ci.yml` runs `npm ci` and then `lint`, `build` and `test` from this
directory on every pull request. None of the three needs a backend, a database or Docker, so the
job is the frontend's whole gate and running it here reproduces it — with one caveat worth knowing:
CI resolves the Node line in `.nvmrc`, which may be newer than the machine you are on.

## The prerendered pages

`/` and `/problems` must be readable without JavaScript (docs/SPEC.md §4.1), so a release builds
with `build:static`. It renders `src/prerender/prerender.tsx` with Vite's SSR build and writes into
`dist/`:

| File | Served for |
| --- | --- |
| `index.html` | `/` — the landing page, rendered |
| `problems.html` | `/problems` — every card in full, each with its anchor |
| `spa.html` | every other route — the untouched shell |
| `sitemap.xml`, `robots.txt` | themselves |

It needs the content export, made by the backend's own parser, and the site's address:

```bash
dotnet run --project src/Ritocode.ContentTool -- export content-export.json content
CONTENT_EXPORT=../content-export.json SITE_ORIGIN=https://ritocode.ru npm run build:static
```

The server tries the path, then the path with `.html`, then `spa.html` (Caddy:
`try_files {path} {path}.html /spa.html`). Once the bundle loads, the application renders over the
static markup; `/problems` then reads the catalogue from the API as it always does.

## How it is put together

```
src/
  api/         the only code that knows the backend exists
  i18n/        the translation catalogue (ru.ts), translate() and useT()
  session/     who is signed in, from GET /me, and RequireSignIn for pages that need it
  hooks/       useApiResource — one request, four states, no cache
  components/  the layout, the loading / error / empty panels, and Markdown for card text
  pages/       one component per route: the landing, /tasks, /problems, /progress, not found;
               task/ is the task screen and the review it lands on
  site/        build-time configuration (the demo task), and each page's title and description
  prerender/   / and /problems as static HTML, for build:static
  routes.tsx   the route table, as data
  test/        render helpers and response builders shaped like the real API
```

Four things worth knowing before changing it:

- **No user-visible string is written in a component.** Text lives in `src/i18n/ru.ts` under a key
  saying where it is shown, and a component reads it with `useT()`: `t('session.signedInAs',
  { username })`. ESLint fails on text in JSX and on a literal `aria-label`, `title`, `alt`,
  `placeholder` or `label`. The catalogue is this project's own, not a library: typed keys, `{name}`
  placeholders and Russian plurals are all it needs, and a second locale is an `en.ts` of the same
  shape. Dates and numbers go through `Intl` with `useLocale()`.

- **Nothing outside `src/api` calls `fetch`.** The ADR 0003 error envelope is read in exactly one
  place, `api/errors.ts`, and every failure reaches a screen as an `ApiError` carrying the stable
  `code` a client is allowed to branch on. That is what stops a special case per endpoint, which is
  the failure ADR 0003 was written to prevent.
- **`useApiResource` returns a discriminated union**, not `{ data, loading, error }`. The triple has
  states that cannot happen and a screen written against it has to invent a meaning for them.
- **The client is passed through React context**, never imported as a module singleton, so a test
  mounts the real route table against a stubbed `fetch`.

Tests stub `fetch` rather than run a mock server: what is under test is what this application does
with a response, and a mock server would be a second implementation of the API to keep in step with
the first. The bodies in `src/test/responses.ts` are copied from the verification table in
`docs/PROJECT_STATE.md`.

## What is deliberately not here

- **Signing in.** Who is signed in comes from `GET /me`, which answers for the development identity
  until [#6](https://github.com/shoraLBRT/ritocode/issues/6) and [#7](https://github.com/shoraLBRT/ritocode/issues/7)
  bring sessions and sign-in; `RequireSignIn` tells a signed-out visitor the page is closed, and
  the button to sign in arrives with #7.
- **Signals and signing in to check.** The review's *I'm sure it is here* button is
  [#129](https://github.com/shoraLBRT/ritocode/issues/129); signing in on *Check* and coming back to
  the task is #127.
- **Prerendering.** `/` and `/problems` are prerendered for search engines in #132.
- **A data-fetching library, a state manager and a design system.** Nothing in the slice needs a
  cache, and a design system invented here would be replaced by stage 6.
