# 0011 — Release images on GitHub Container Registry

- Status: Accepted (by the maintainer, 2026-10-01)
- Date: 2026-10-01
- Relates to: [#31](https://github.com/shoraLBRT/ritocode/issues/31), [#135](https://github.com/shoraLBRT/ritocode/issues/135), [#136](https://github.com/shoraLBRT/ritocode/issues/136)
- Builds on: [0001](0001-technology-stack.md), [SPEC.md](../SPEC.md) §9.5

## Context

A release builds images in CI, then on the server applies migrations, runs content ingest and
restarts the API ([SPEC.md](../SPEC.md) §9.5). The server is one VPS in Russia. #31 asks for images
built and tagged on every merge to `main`, published where that VPS can pull them reliably, and
leaves the registry to be chosen with that reachability as the deciding factor.

Two things decide what "reliably" can mean before the VPS exists:

- **What a pull needs.** Every credential a pull needs is one more secret on the server and one more
  account the maintainer keeps. The repository is public.
- **What is known about reachability.** Docker Hub has restricted access from Russian addresses
  before. GitHub, and with it `ghcr.io`, is used from Russian hosting every day, but is a foreign
  service like Docker Hub. Russian registries (Yandex Container Registry, Selectel) are the most
  reachable from a Russian VPS, and each needs an account, a paid project and a key held in CI.

## Decision

**Images go to GitHub Container Registry**, as public packages of this public repository:

| Image | Holds |
| --- | --- |
| `ghcr.io/shoralbrt/ritocode-api` | the API, and the migrator beside it (`migrator/Ritocode.DbMigrator.dll`) |
| `ghcr.io/shoralbrt/ritocode-web` | the frontend with `/` and `/problems` prerendered, served by Caddy on port 80 |

- **Tags:** the full commit — what a release names — and `main`, the newest.
- **Built on every push to `main`** by `.github/workflows/release-images.yml`, and **built without
  pushing on every pull request**, so a broken Dockerfile fails before it merges.
- **Publishing needs no secret beyond the workflow's own token**, and pulling needs none at all.

## Alternatives

- **Docker Hub.** It needs an account and a token, and it is the registry whose access from Russia
  has been restricted before. Rejected on the deciding factor.
- **Yandex Container Registry or Selectel.** These are the most reachable from a Russian VPS. They
  need a cloud account with billing, a service-account key in CI, and a credential on the server for
  every pull. That is the maintainer's to open, and nothing yet shows it is needed. It remains the
  fallback below.
- **Building on the server.** No registry at all, but the server needs the SDKs and the source, and
  a release takes minutes of the VPS's CPU. It also loses "the image that was tested is the image
  that runs".

## Consequences

- A merge to `main` publishes images anyone can pull. Nothing in them is secret: configuration and
  credentials come from the server's environment (#135).
- The prerendered pages need the site's address at build time. The workflow reads the repository
  variable `SITE_ORIGIN`, and until the domain exists (#134) it builds with a placeholder and says so
  in a warning.
- **Revisit when** a pull from the VPS in #135 is slow or fails. Then add a mirror in a Russian
  registry: a second login and push in the same workflow, keyed by secrets the maintainer adds. The
  images and tags stay as they are.
