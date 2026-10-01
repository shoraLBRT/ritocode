# Runbook

How to run Ritocode in production: one VPS, Docker Compose ([SPEC.md](SPEC.md) §9.5). What each file
does is in [deploy/README.md](../deploy/README.md); this page is what to type, and in what order.

Commands marked **local** run from a clean checkout of the repository on your machine. Commands
marked **server** run on the VPS as the deploy user, in `~/ritocode` — the directory a release
copies `deploy/` into. Every `docker compose` command on the server is:

```bash
docker compose -f compose.production.yml --env-file production.env …
```

written below as `dc …`. Add `alias dc='docker compose -f compose.production.yml --env-file production.env'`
to the deploy user's shell.

---

## Before each release

- [ ] CI on the commit is green, the *Release images* workflow pushed its images, and it is on `main`.
- [ ] The PR said whether it adds a migration. If it does, read it: a migration is never undone by a
      rollback (see [Roll back](#roll-back)).
- [ ] Content changes passed *Validate content*; a release refuses content with errors and keeps the
      old content, but the release then stops before starting the new images.
- [ ] Today's backup exists — `ls -lt /var/backups/ritocode | head` on the server — or take one:
      `./backup.sh`.
- [ ] You know the commit that is running now, to roll back to: `dc images api` on the server.

## First setup of a server

Once, by the maintainer (#134 provides the server, the domain and the OAuth apps).

1. **server** Install Docker with the Compose plugin and `rclone`. Create the deploy user, add it to
   the `docker` group, and allow your SSH key.
2. Point DNS A records for `SITE_DOMAIN` and `UMAMI_DOMAIN` at the server. Open ports 80 and 443.
3. **server** `mkdir ~/ritocode`, then create `~/ritocode/production.env` from
   [deploy/production.env.example](../deploy/production.env.example): long random passwords, the OAuth
   apps' ids and secrets (their callbacks are `https://SITE_DOMAIN/auth/callback/github` and
   `/google`), your sign-in address as `ADMIN_EMAIL`. `chmod 600 production.env`.
4. **server** For the off-server backup copy: create the bucket and its access key at the provider,
   run `rclone config` to add a remote for it, and set `BACKUP_REMOTE` (e.g. `backups:ritocode-backups`).
5. **local** Release `main` — the [release](#release) below. The first one creates the volumes,
   Umami's database, the schemas and the content.
6. Open `https://UMAMI_DOMAIN`, sign in with Umami's default account (`admin` / `umami`) and **change
   its password at once**. Add the site; set the repository variables `UMAMI_SCRIPT_URL`
   (`https://UMAMI_DOMAIN/script.js`) and `UMAMI_WEBSITE_ID` (GitHub → Settings → Variables), and
   `SITE_ORIGIN` (`https://SITE_DOMAIN`). The web image takes them at build time, so release once more
   after the next push to `main`.
7. **server** Schedule the backup: `crontab -e`, then
   `15 3 * * * cd ~/ritocode && ./backup.sh >> ~/ritocode-backup.log 2>&1`.
8. Sign in with GitHub and with Google; open `/admin` — it is there only for `ADMIN_EMAIL`.
9. Turn on [monitoring](#monitoring): set the repository variable `MONITOR_ORIGINS` to
   `https://SITE_DOMAIN https://UMAMI_DOMAIN`.

## Release

**local**

```bash
RITOCODE_SERVER=deploy@server ./scripts/release.sh
```

It releases `origin/main`; give a commit to release another. It prints each step and ends with
`Released <commit>`. If the API does not become healthy it prints the API's last log lines and stops:
the new images are already running, so [roll back](#roll-back).

Check: `https://SITE_DOMAIN/health/ready` answers `Healthy`, and the site opens.

## Roll back

Release the commit that ran before:

```bash
RITOCODE_SERVER=deploy@server ./scripts/release.sh <previous commit>
```

Migrations are not undone. The previous code runs on the newer schema only if the migration added
rather than removed or renamed — which is why the checklist reads it first. If it did not, restore the
backup taken before the release instead ([Restore a backup](#restore-a-backup)).

## Restore a backup

**server**, in `~/ritocode`:

1. Find the dump: `ls -lt /var/backups/ritocode`. Not on the server any more? Fetch it from the
   remote: `rclone copy "$BACKUP_REMOTE/ritocode/<file>" /var/backups/ritocode/`.
2. Restore it:

   ```bash
   ./restore.sh ritocode /var/backups/ritocode/ritocode-<stamp>.dump
   ```

   It stops the API, recreates the database empty, restores the dump and starts the API. Everything
   written after the dump is gone.
3. Check: `https://SITE_DOMAIN/health/ready`, the catalogue, a signed-in page.

Umami's database the same way: `./restore.sh umami /var/backups/ritocode/umami-<stamp>.dump`.

## Rotate a secret

Every secret is in `~/ritocode/production.env` on the server; nothing else holds one.

| Secret | How |
| --- | --- |
| `GITHUB_CLIENT_SECRET`, `GOOGLE_CLIENT_SECRET` | Make a new secret at the provider, put it in `production.env`, `dc up -d api`, then delete the old one at the provider. |
| `POSTGRES_PASSWORD` | `dc exec postgres psql -U ritocode -c "ALTER ROLE ritocode PASSWORD '<new>'"`, put it in `production.env`, `dc up -d api`. |
| `UMAMI_DB_PASSWORD` | `dc exec postgres psql -U ritocode -c "ALTER ROLE umami PASSWORD '<new>'"`, put it in `production.env`, `dc up -d umami`. |
| `UMAMI_APP_SECRET` | Put a new one in `production.env`, `dc up -d umami`; everyone signed in to Umami signs in again. |
| The rclone key | Make a new key at the provider, `rclone config` to update the remote, delete the old key. |
| The data-protection key ring | Not a secret anyone types. To discard it: `dc stop api`, `docker volume rm ritocode-production_api-keys`, `dc up -d api`; sign-ins in flight fail once. |

Signed-in learners stay signed in through all of these: sessions live in the database.

## Read logs

**server**

```bash
dc logs --since 1h api
```

A request a user reports by its id (the `X-Request-Id` every error shows):

```bash
dc logs --no-log-prefix api | jq -c 'select(any(.Scopes[]?; .RequestId == "<id>"))'
```

The other services: `dc logs caddy`, `umami`, `postgres`. Docker keeps 5 × 10 MB per service. How the
API's lines are shaped: [deploy/README.md](../deploy/README.md#logs).

## Monitoring

The *Monitor* workflow ([ADR 0013](adr/0013-uptime-monitoring.md)) runs every five minutes from
GitHub, outside the server. It checks the addresses in the repository variable `MONITOR_ORIGINS`
(GitHub → Settings → Variables; the site first): the site's `/health/ready` answers `Healthy` —
Caddy, the API and PostgreSQL are up — and every address's certificate is valid and more than 14 days
from expiry. Empty, nothing is checked; clear it to silence monitoring during planned work.

**The alert** is an issue labelled `monitoring-alert` that mentions you, so GitHub notifies you by
e-mail and in the app. It shows the failing checks and links the run. While it is open a comment is
added only when what fails changes. When every check passes again the workflow comments and closes it.

| It says | Look at |
| --- | --- |
| `FAIL health`, `curl: (7)` or a timeout | The server or Caddy: `dc ps`, then `dc logs --since 15m caddy`. The VPS's console if SSH fails too. |
| `FAIL health`, `returned error: 502` | The API is down behind Caddy: `dc ps api`, `dc logs --since 15m api`, `dc up -d api`. |
| `FAIL health`, `returned error: 503` | The API is up and a database check fails: `dc ps postgres`, `dc logs --since 15m postgres`, disk space (`df -h`). |
| `FAIL certificate`, `expires within 14 days` or `expired` | Caddy has failed to renew for two weeks or more: `dc logs caddy \| grep -i -E "acme\|certificate"`. Usually DNS or port 80 closed. |
| `FAIL reachable` (Umami's address) | `dc ps umami`, `dc logs --since 15m umami`. |

**Try it** without waiting for a failure: *Actions* → *Monitor* → *Run workflow*, with
`https://expired.badssl.com` as the addresses. It opens an alert; the next scheduled run, on the real
addresses, closes it.

Two limits of a scheduled workflow: GitHub can start a run several minutes late when it is busy, and
it **disables the schedule after 60 days without a commit** to the repository — it e-mails a warning
first; re-enable it under *Actions* → *Monitor*.

## Add an admin

Admins are the addresses in `Users:Admin:Emails` (SPEC §6.2). The production Compose file passes one:
`ADMIN_EMAIL`.

- **Change the admin:** set `ADMIN_EMAIL` in `production.env`, then `dc up -d api`. It takes effect
  on the next request — no one has to sign in again.
- **A second admin:** add `Users__Admin__Emails__1: ${ADMIN_EMAIL_2:-}` under the API's environment
  in `deploy/compose.production.yml` in a PR, release it, then set `ADMIN_EMAIL_2` on the server.

The address must be the one the person signs in with, verified at the provider.

## Publish new content

Content reaches production only through the repository (SPEC §7).

1. Write it with the `author-card` / `author-task` skills; open a PR. CI runs *Validate content* and
   *Prerender public pages*.
2. Merge. The release-images workflow builds images carrying that `content/`.
3. [Release](#release). The release ingests the image's content stamped with the commit: cards and
   tasks are upserted by slug, a task removed from `content/` is unpublished and a card retired —
   never deleted, because attempts name them. Stored results never change.

The prerendered `/problems` page is rebuilt with the web image, so it shows the new cards after the
same release.
