# Deploy

What runs in production and how it is built ([SPEC.md](../docs/SPEC.md) §9.5): the images, the
production Compose file of [#135](https://github.com/shoraLBRT/ritocode/issues/135), and the logs.
The release command and backups arrive with [#136](https://github.com/shoraLBRT/ritocode/issues/136).

## Images

Built by `.github/workflows/release-images.yml` on every push to `main`, tagged with the full commit
and `main`, public on GitHub Container Registry ([ADR 0011](../docs/adr/0011-release-images.md)).

| Image | Dockerfile | Runs |
| --- | --- | --- |
| `ghcr.io/shoralbrt/ritocode-api` | `api.Dockerfile` | the API on port 8080, as a non-root user; the migrator with `--entrypoint dotnet … migrator/Ritocode.DbMigrator.dll` |
| `ghcr.io/shoralbrt/ritocode-web` | `web.Dockerfile` | the static frontend, `/` and `/problems` prerendered, on port 80 (`web.Caddyfile`) |

Both read their configuration from the environment: `Database__ConnectionString` for the API and the
migrator. The web image takes the site's address at build time from the repository variable
`SITE_ORIGIN`.

## Production

`compose.production.yml` runs everything on the one server: **Caddy** at the edge (`Caddyfile`),
the **API** and the **web** image behind it, **PostgreSQL** on a volume, and **Umami** with a database
of its own in that PostgreSQL. Only Caddy publishes ports (80 and 443).

| Path | Goes to |
| --- | --- |
| `https://SITE_DOMAIN/api/*`, `/auth/*`, `/health/*` | the API, port 8080 |
| anything else on `SITE_DOMAIN` | the web image, port 80 |
| `https://UMAMI_DOMAIN` | Umami, port 3000 |

- **TLS and HTTP → HTTPS** are Caddy's own: it gets both certificates from Let's Encrypt once the DNS
  records point at the server, and keeps them in the `caddy-data` volume.
- **Headers.** The edge adds HSTS, `nosniff`, frame denial, a referrer policy and a permissions policy
  to everything, and a content policy to the pages that allows Umami's script and its `/api/send`.
  The API sets its own stricter ones on its responses (#35).
- **Secrets** live in `production.env` beside the Compose file on the server, never in the repository;
  `production.env.example` lists them. The API gets the database, the OAuth apps of #134 and the
  admin's address from it.
- **Behind the proxy.** The API runs with `Api__BehindProxy=true`, so it takes the edge's forwarded
  scheme: the sign-in callback is `https://SITE_DOMAIN/auth/callback/{github,google}`. Its
  data-protection key ring is in the `api-keys` volume (`Api__DataProtectionKeysDirectory`), so a
  release does not fail a sign-in in flight.
- **Umami's database** is made by `postgres-init/10-umami.sh` when the PostgreSQL volume is first
  created. On a volume that already exists, make it by hand: `CREATE ROLE umami LOGIN PASSWORD '…';
  CREATE DATABASE umami OWNER umami;`. Register the site in Umami's own pages, then set the
  repository variables `UMAMI_SCRIPT_URL` (`https://UMAMI_DOMAIN/script.js`) and `UMAMI_WEBSITE_ID`,
  which the web image is built with (#133).
- **Migrations** run from the API image as the one-off `migrate` service:
  `docker compose -f compose.production.yml --env-file production.env run --rm migrate`. The release
  command of #136 runs it, then content ingest, then restarts the API.

First start on a new server, from `deploy/`:

```bash
docker compose -f compose.production.yml --env-file production.env pull
```

```bash
docker compose -f compose.production.yml --env-file production.env run --rm migrate
```

```bash
docker compose -f compose.production.yml --env-file production.env up -d
```

## Logs

The API writes **one JSON object per line** to standard output (the console's `json` formatter,
`src/Ritocode.Api/appsettings.json`), in UTC:

- every request has one summary line — method, path, status, duration — from
  `Microsoft.AspNetCore.HttpLogging`;
- every line of a request carries its **request id** in `Scopes` — the `X-Request-Id` the response
  and every error body carry — and, for a signed-in caller, the **user id**. Nothing else about the
  person is logged: no e-mail, no name, no headers, no query, no body;
- levels come from `Logging:LogLevel`, overridable by environment variable, such as
  `Logging__LogLevel__Default=Warning`.

To find a request's lines by the id a user reports:

```bash
docker compose logs --no-log-prefix api | jq -c 'select(any(.Scopes[]?; .RequestId == "<id>"))'
```

ASP.NET adds a scope of its own that also has a `RequestId` key: the server's connection trace id.
It has a different format and never equals a client's id, so the query above finds exactly the
request.

**Rotation is Docker's.** The production Compose file (#135) gives the API, and every other
service, the `json-file` driver with a cap, so the disk never fills:

```yaml
logging:
  driver: json-file
  options:
    max-size: "10m"
    max-file: "5"
```
