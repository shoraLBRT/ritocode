# Deploy

What runs in production and how it is built ([SPEC.md](../docs/SPEC.md) §9.5): the images, the
production Compose file of [#135](https://github.com/shoraLBRT/ritocode/issues/135), the release,
backups and restore of [#136](https://github.com/shoraLBRT/ritocode/issues/136), and the logs.

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
- **When nothing answers** (#298): if the API or the web image cannot be reached (Caddy's 502 or
  504) the edge serves `maintenance/index.html` itself, mounted read-only at `/srv/maintenance`, with
  503 and `Retry-After: 60` — the site's look, Russian, no script. While `maintenance/on` exists the
  edge answers every request on `SITE_DOMAIN` that way; a release creates and removes it, and
  [docs/RUNBOOK.md](../docs/RUNBOOK.md#maintenance) says how by hand. A status the API answered
  itself, its own 503 included, passes through. The pages show one banner while the API does not
  answer (`frontend/src/components/ApiUnavailableBanner.tsx`).
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
- **The feedback issues** are opened as a GitHub App (ADR 0019): the Transliterator's feedback App,
  which is installable on this account only. Install it on this repository too — GitHub → Settings →
  Applications → the App → Configure → add the repository; it needs *Issues: read & write* — and put
  its App ID (`FEEDBACK_GITHUB_APP_ID`) and private key (`FEEDBACK_GITHUB_APP_PRIVATE_KEY`, the PEM on
  one line with `\n` for its line breaks) into `production.env`. The API finds the installation by
  the repository and renews its installation token itself. Renaming the App to a neutral name changes
  only the bot's display name on the issues. `FEEDBACK_GITHUB_TOKEN`, a fine-grained personal token,
  is the alternative when no App is set.
- **The feedback captcha**: the Turnstile site key is the repository variable `TURNSTILE_SITE_KEY`,
  which the web image is built with; its secret is `TURNSTILE_SECRET_KEY` in `production.env`. Set
  both or neither — a secret without the key in the page refuses every message (ADR 0019).
- **Migrations and content** run from the API image as the one-off `migrate` service: `migrate`
  applies migrations, `migrate ingest content <commit>` ingests the `content/` the image was built
  with, stamped with the commit. A release runs both.
- **Images** come from `RITOCODE_REGISTRY` (GHCR by default; a mirror if GHCR is slow from the server,
  ADR 0011) at `RITOCODE_VERSION`.

## Release

From a clean checkout, one command (the server needs Docker, the files of `deploy/` it is sent, and
its own `production.env`):

```bash
RITOCODE_SERVER=deploy@server ./scripts/release.sh
```

It releases `origin/main` (or the commit given as its argument), whose images the release-images
workflow has built: it copies that commit's `deploy/` to `~/ritocode` on the server and runs
`deploy/release.sh <commit>` there — pull the images, switch maintenance on, apply migrations,
ingest the content with the commit, start the services on the new images, wait for `/health/ready`,
switch maintenance off (a failed release leaves it on). **Rolling back is releasing
the previous commit.** A migration is never undone by a rollback; one that must be reversed needs a
new migration.

On the server itself the same release is `./release.sh <full commit>` from `~/ritocode`.

## Backups

`backup.sh` dumps both databases — the product's and Umami's — in PostgreSQL's custom format to
`BACKUP_DIR` (`/var/backups/ritocode`), keeps seven days of them, and copies each new dump off the
server with `rclone` to `BACKUP_REMOTE`. Run it daily from cron on the server:

```
15 3 * * * cd /home/deploy/ritocode && ./backup.sh >> /var/log/ritocode-backup.log 2>&1
```

**Where the off-server copy goes** (decided in #136): an S3-compatible bucket at a Russian provider
— Selectel or Timeweb Cloud object storage — in another region from the server, through an `rclone`
remote configured on the server (`rclone config`), with the bucket's own lifecycle rule keeping 30
days. It keeps the data in Russia (SPEC §9.5, 152-FZ), costs a few roubles a month at this size, and
is not lost with the server. `BACKUP_REMOTE` is the remote and bucket, e.g. `backups:ritocode-backups`;
left empty, dumps stay on the server only. Creating the bucket and its key is the maintainer's
(#134).

## Restore

```bash
./restore.sh ritocode /var/backups/ritocode/ritocode-<stamp>.dump
```

It stops the API, drops the database and makes it again empty, restores the dump into it and starts
the API — everything written since the dump is lost. `./restore.sh umami <dump>` does the same for
Umami. A dump from the remote is fetched first with `rclone copy BACKUP_REMOTE/ritocode/<file> .`.

**Performed once** (#136), on a local run of this stack: a release ingested the content; `backup.sh`
dumped both databases; `docker compose down -v` removed every volume; PostgreSQL started on an empty
volume (no `content` schema); `restore.sh ritocode` restored the dump; the site served the catalogue
(the same three cards), the task, the landing page and a healthy `/health/ready`. Repeat it on the
server once it exists, and after any change to these scripts.

A run is good only when every dump was made **and** copied to `BACKUP_REMOTE`: then it writes the
time to `BACKUP_DIR/heartbeat/last-good`; any failure writes `last-failed`. With `BACKUP_REMOTE`
empty the dumps are still made, but the run counts as failed. `check.sh` alerts on both (below), and
the admin system page shows `last-good` — the API mounts `BACKUP_DIR/heartbeat` read-only, and
`release.sh` creates that directory so Docker does not create it as root.

## Checks

`check.sh` checks the server every five minutes — disk, memory, load, containers, the database,
the backup's heartbeat — and `alert.sh` sends Telegram when what fails changes and on recovery
([ADR 0020](../docs/adr/0020-observability.md), #296):

```
*/5 * * * * cd /home/deploy/ritocode && ./check.sh >> /var/log/ritocode-check.log 2>&1
```

The bot's token and chat id and every threshold are in `production.env`; what each alert means is in
[RUNBOOK.md › The server's checks](../docs/RUNBOOK.md#the-servers-checks). `check-test.sh` tests both
scripts against fakes (`DeployScriptTests` runs it in the backend's tests).

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

## Monitoring

Nothing on the server: the uptime check runs from GitHub every five minutes
(`.github/workflows/monitor.yml`, [ADR 0013](../docs/adr/0013-uptime-monitoring.md)) against
`/health/ready` through Caddy, and against the certificates of the addresses in the repository
variable `MONITOR_ORIGINS`. An alert is an issue labelled `monitoring-alert`. What to look at when one
opens: [RUNBOOK.md](../docs/RUNBOOK.md#monitoring).
