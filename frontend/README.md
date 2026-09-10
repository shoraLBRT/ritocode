# Ritocode frontend

React + Vite + TypeScript. This is the application shell and the API client layer from
[#26](https://github.com/shoraLBRT/ritocode/issues/26) — the frame the product screens are built
into, not the product screens themselves.

## Running it

Node 22.22 or newer. The backend has to be running for anything but the not-found page to have
content; `docs/PROJECT_STATE.md` has the commands, and a Development host is the one that seeds a
problem to browse.

```bash
npm install
npm run dev
```

The dev server binds port 5173 with `strictPort`, because that exact origin is what the API's
`appsettings.Development.json` allows through CORS. Changing the port here without changing it
there makes every request fail in the browser and succeed from `curl`.

`VITE_API_BASE_URL` says where the API lives; see `.env.example` for the default. It is read at
build time and inlined, so a change needs a rebuild.

## Scripts

| Command | What it does |
| --- | --- |
| `npm run dev` | Vite dev server on <http://localhost:5173> |
| `npm run build` | Typecheck, then a production bundle in `dist/` |
| `npm run typecheck` | `tsc --build` over the app and the tooling config |
| `npm run lint` | ESLint, type-aware rules included |
| `npm test` | Vitest, once |
| `npm run test:watch` | Vitest, watching |

## How it is put together

```
src/
  api/         the only code that knows the backend exists
  hooks/       useApiResource — one request, four states, no cache
  components/  the layout and the loading / error / empty panels
  pages/       one component per route
  routes.tsx   the route table, as data
  test/        render helpers and response builders shaped like the real API
```

Three things worth knowing before changing it:

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

- **Protected routes and anything about a signed-in user.** There is no authentication yet — the
  identity seam is [#6](https://github.com/shoraLBRT/ritocode/issues/6) in slice stage 3 — so a
  route guard written now would be guarding against a session nothing issues, and would be replaced
  rather than wired up. `ApiError.isUnauthenticated` exists so the branch is not forgotten, and
  `routes.tsx` says where the guard goes. #26 stays open for it.
- **The catalog and problem screens.** `ProblemsPage` and `ProblemDetailPage` are wiring, not
  design: they exist to prove the page envelope, the query parameters and the error body all
  survive the trip. [#27](https://github.com/shoraLBRT/ritocode/issues/27) in stage 6 replaces both.
- **A data-fetching library, a state manager and a design system.** Nothing in the slice needs a
  cache, and a design system invented here would be replaced by stage 6.
