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
- [ ] The off-server bucket still expires objects after 7 days, and nothing there is older —
      `rclone lsl "$BACKUP_REMOTE"` ([Personal data](#personal-data)).

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
   with a lifecycle rule deleting objects older than 7 days ([Personal data](#personal-data)),
   run `rclone config` to add a remote for it, and set `BACKUP_REMOTE` (e.g. `backups:ritocode-backups`).
5. **local** Replace the placeholder in `frontend/src/site/privacy.ru.md` with the privacy policy's
   text (#134) and merge it: `/privacy` renders that file, and every sign-in offer links to it.
6. **local** Release `main` — the [release](#release) below. The first one creates the volumes,
   Umami's database, the schemas and the content.
7. Open `https://UMAMI_DOMAIN`, sign in with Umami's default account (`admin` / `umami`) and **change
   its password at once**. Add the site; set the repository variables `UMAMI_SCRIPT_URL`
   (`https://UMAMI_DOMAIN/script.js`) and `UMAMI_WEBSITE_ID` (GitHub → Settings → Variables), and
   `SITE_ORIGIN` (`https://SITE_DOMAIN`). The web image takes them at build time, so release once more
   after the next push to `main`.
   **server** For the traffic panel on `/admin` (#306): put the same site id in `production.env` as
   `UMAMI_WEBSITE_ID`, and a token as `UMAMI_API_TOKEN` — [get an Umami token](#get-an-umami-token)
   below, after the password is changed (changing it ends the token) — then `dc up -d api`. Without them the panel says Umami is not configured; nothing else
   depends on it.
8. **server** Schedule the backup: `crontab -e`, then
   `15 3 * * * cd ~/ritocode && ./backup.sh >> ~/ritocode-backup.log 2>&1`.
   And the server's own checks ([Monitoring](#monitoring)), every five minutes, in the same crontab:
   `*/5 * * * * cd ~/ritocode && ./check.sh >> ~/ritocode-check.log 2>&1`. Make their Telegram bot
   first ([The server's checks](#the-servers-checks)).
9. Sign in with GitHub and with Google; open `/admin` — it is there only for `ADMIN_EMAIL`. Each
   sign-in offer carries the notice linking `/privacy`.
10. Turn on [monitoring](#monitoring): set the repository variable `MONITOR_ORIGINS` to
    `https://SITE_DOMAIN https://UMAMI_DOMAIN`.
11. Register the site in the [search consoles](#search-consoles) and set the IndexNow key.

## Release

**local**

```bash
RITOCODE_SERVER=deploy@server ./scripts/release.sh
```

It releases `origin/main`; give a commit to release another. It prints each step and ends with
`Released <commit>`. From the migrations until the API is healthy the site is in
[maintenance](#maintenance): every address answers 503 with the maintenance page. If the API does not
become healthy it prints the API's last log lines and stops with maintenance still on: the new images
are already running, so [roll back](#roll-back) — the rollback's release takes maintenance off.

After maintenance is off it tells [IndexNow](#indexnow) which public pages changed — a line starting
`IndexNow:`. A failure there is reported and does not fail the release.

Check: `https://SITE_DOMAIN/health/ready` answers `Healthy`, and the site opens.

## Roll back

Release the commit that ran before:

```bash
RITOCODE_SERVER=deploy@server ./scripts/release.sh <previous commit>
```

Migrations are not undone. The previous code runs on the newer schema only if the migration added
rather than removed or renamed — which is why the checklist reads it first. If it did not, restore the
backup taken before the release instead ([Restore a backup](#restore-a-backup)).

## Maintenance

While the file `maintenance/on` exists, Caddy answers every request to `SITE_DOMAIN` — the pages, the
API, `/health/ready` — with 503, `Retry-After: 60` and the page `maintenance/index.html` (Russian, no
script). It reads the file on every request: no restart. The same page is what visitors get whenever
the API or the web image does not answer. A release switches it on and off by itself; by hand, for
work of your own on the server:

**server**, in `~/ritocode`:

```bash
touch maintenance/on    # on
rm maintenance/on       # off
```

Check: `curl -sI https://SITE_DOMAIN/ | head -1` says `503` while it is on and `200` once it is off.
For planned work longer than a few minutes, clear `MONITOR_ORIGINS` too ([Monitoring](#monitoring)),
or the monitor opens an alert.

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

**A restore brings back accounts deleted after the dump.** Delete them again before anything else:
every deletion logs `Deleted account <id> on its owner's request` (event 1210) with the id only —
[Read logs](#read-logs) from the dump's time on, then for each id run the
[deletion by hand](#delete-an-account-by-hand).

## Personal data

The privacy policy promises what this section keeps (#299, `frontend/src/site/privacy.ru.md`).

- **Export and delete are the learner's own:** both are on `/profile` (`GET /api/v1/me/export`,
  `DELETE /api/v1/me`). Deleting erases every module's rows at once; nothing is left in the live
  database.
- **Backups forget a deleted account within seven days.** `backup.sh` keeps seven daily dumps on the
  server (`-mtime +6`), so the last dump that holds an account is gone a week after its deletion. The
  **off-server copy must expire as fast**: give the bucket a lifecycle rule that deletes objects older
  than **7 days** when you create it ([First setup](#first-setup-of-a-server), step 4), and check it
  is still there at each release. A longer rule breaks the promise.
- **A request by e-mail** from someone who cannot sign in any more: confirm the address is theirs,
  then delete by hand.

### Delete an account by hand

**server**, in `~/ritocode`. Find the id, then erase in the order the API does — every other schema
first, the account last:

```bash
dc exec postgres psql -U ritocode ritocode -c "SELECT id FROM users.users WHERE email = lower('<address>')"
dc exec postgres psql -U ritocode ritocode -c "
  BEGIN;
  DELETE FROM attempts.signals     WHERE user_id = '<id>';
  DELETE FROM attempts.reveals     WHERE user_id = '<id>';
  DELETE FROM attempts.study_marks WHERE user_id = '<id>';
  DELETE FROM attempts.attempts    WHERE user_id = '<id>';
  DELETE FROM auth.sessions        WHERE user_id = '<id>';
  DELETE FROM auth.linked_accounts WHERE user_id = '<id>';
  DELETE FROM users.users          WHERE id = '<id>';
  COMMIT;"
```

A table added since names a `user_id` too; `docs/DATABASE_SCHEMA.md` lists every one.

## Rotate a secret

Every secret is in `~/ritocode/production.env` on the server; nothing else holds one.

| Secret | How |
| --- | --- |
| `GITHUB_CLIENT_SECRET`, `GOOGLE_CLIENT_SECRET` | Make a new secret at the provider, put it in `production.env`, `dc up -d api`, then delete the old one at the provider. |
| `POSTGRES_PASSWORD` | `dc exec postgres psql -U ritocode -c "ALTER ROLE ritocode PASSWORD '<new>'"`, put it in `production.env`, `dc up -d api`. |
| `UMAMI_DB_PASSWORD` | `dc exec postgres psql -U ritocode -c "ALTER ROLE umami PASSWORD '<new>'"`, put it in `production.env`, `dc up -d umami`. |
| `UMAMI_APP_SECRET` | Put a new one in `production.env`, `dc up -d umami`; everyone signed in to Umami signs in again, and `UMAMI_API_TOKEN` stops working — get a new one (below). |
| `UMAMI_API_TOKEN` | [Get an Umami token](#get-an-umami-token), put it in `production.env`, `dc up -d api`. A token outlives Umami's restarts; changing that Umami account's password or `UMAMI_APP_SECRET` ends it, so get a new one after either. |
| The rclone key | Make a new key at the provider, `rclone config` to update the remote, delete the old key. |
| `TELEGRAM_BOT_TOKEN` | @BotFather → `/revoke` for the monitoring bot, put the new token in `production.env` (`check.sh` reads it on its next run; nothing to restart) **and** in the repository secret of the same name, which the *Monitor* workflow sends with; then `dc up -d api`, as the API sends the error alerts with it (#294). |
| The data-protection key ring | Not a secret anyone types. To discard it: `dc stop api`, `docker volume rm ritocode-production_api-keys`, `dc up -d api`; sign-ins in flight fail once. |

Signed-in learners stay signed in through all of these: sessions live in the database.

### Get an Umami token

The traffic panel on `/admin` (#306) reads Umami's API as an Umami account that can see the site. Self-hosted
Umami has no API keys; its sign-in answers with a token, and that token is what the API sends. On the
server, through the edge (put the account's password in the prompt, not in the shell's history):

```bash
read -rsp 'Umami password: ' p; echo
curl -s https://UMAMI_DOMAIN/api/auth/login -H 'Content-Type: application/json' \
  -d "{\"username\":\"admin\",\"password\":\"$p\"}" | sed 's/.*"token":"\([^"]*\)".*/\1/'; unset p
```

The printed value is `UMAMI_API_TOKEN`. It outlives Umami's restarts, and ends when that account's
password or `UMAMI_APP_SECRET` changes. The panel shows **refused** when Umami no longer takes it, and
**failing** when Umami does not answer (`dc ps umami`, `dc logs --since 15m umami`).

## Read logs

**browser** — `https://SITE_DOMAIN/admin/logs/`, signed in as an admin (Dozzle, #295). Signed out it
answers 401, signed in as anyone else 404. Pick `api` (or `caddy`, `umami`, `postgres`…) in the list;
the page tails live, and the search box filters the lines shown.

- A request a user reports by its id (the `X-Request-Id` every error shows): search for the id. Every
  line of that request carries it, the summary line (method, path, status, duration) included.
- By level: search for `"LogLevel":"Error"` (or `Warning`, `Critical`) — the API's lines are JSON with
  the level in `LogLevel`.
- Further back than the page has loaded: scroll up, or widen the time range at the top.

**server** — the same files over SSH, when the site or the viewer is down:

```bash
dc logs --since 1h api
dc logs --no-log-prefix api | jq -c 'select(any(.Scopes[]?; .RequestId == "<id>"))'
dc logs --no-log-prefix api | jq -c 'select(.LogLevel == "Error")'
```

The other services: `dc logs caddy`, `umami`, `postgres`, `dozzle`.

How long they are kept: Docker's driver, 10 × 20 MB compressed for the API and Caddy, 5 × 10 MB for
the rest — about 14 days, or less if the cap comes first; a release starts the API's log anew. The
server's checks report how many days the API's log actually reaches back ([The server's
checks](#the-servers-checks)). How the API's lines are shaped and the settings:
[deploy/README.md](../deploy/README.md#logs).

**The viewer is down** (`FAIL container dozzle` or `docker-proxy`, or `/admin/logs/` answers 502/503):
`dc ps dozzle docker-proxy`, `dc logs --since 15m dozzle docker-proxy`, `dc up -d dozzle docker-proxy`.

## An error was reported

Every unhandled server exception and every error a visitor's browser sends is kept in the `errors`
schema, grouped by fingerprint ([ADR 0020](adr/0020-observability.md) decision 1, #294). A **new**
group, or a resolved one that happens again (**reopened**), sends one Telegram message: the source, the
type, where (the route template or the page's path), the count and a link to `/admin/errors`. It
carries no message, no stack and no request id — Telegram is outside Russia.

1. Open **`/admin/errors`** (Админка › Ошибки). A row is a group: the type and the latest message, where,
   how many times, first and last seen, the latest request ids. *Стек последнего случая* opens the latest
   occurrence's stack and commit.
2. Find the request's log lines by one of its request ids ([Read logs](#read-logs)). A browser report's
   request id is the report's own `POST /api/v1/client-errors`, so its lines say only when it came;
   the stack and the page are in the record. The error page a visitor saw shows the same id as *Номер
   ошибки*, so an id quoted in feedback finds its group.
3. Fix it; after the release, press **Разобрано**. If it happens again the group reopens, and a message
   says so.

What is kept: occurrences for 30 days and at most the latest 50 per group; the group (its count and
times) until it is resolved, and after. No request body, address or user id is recorded.

**The bot** is the server checks' own ([Monitoring › The server's checks](#the-servers-checks)): the
same `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` in `production.env`; after setting them, `dc up -d api`
as well. Either empty: errors are still recorded, nothing is sent. At most 20
messages an hour are sent; past that the API logs `Error alert not sent, past the hour's limit`.

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
| `FAIL health`, `returned error: 503` | Three causes, in this order. [Maintenance](#maintenance) is on: `ls maintenance/on` — a release that failed leaves it on. The API is down behind Caddy, which answers with its maintenance page instead of a 502: `dc ps api`, `dc logs --since 15m api`, `dc up -d api`. The API is up and a database check fails: `dc ps postgres`, `dc logs --since 15m postgres`, disk space (`df -h`). |
| `FAIL certificate`, `expires within 14 days` or `expired` | Caddy has failed to renew for two weeks or more: `dc logs caddy \| grep -i -E "acme\|certificate"`. Usually DNS or port 80 closed. |
| `FAIL reachable` (Umami's address) | `dc ps umami`, `dc logs --since 15m umami`. |

**Try it** without waiting for a failure: *Actions* → *Monitor* → *Run workflow*, with
`https://expired.badssl.com` as the addresses. It opens an alert; the next scheduled run, on the real
addresses, closes it.

Two limits of a scheduled workflow: GitHub can start a run several minutes late when it is busy, and
it **disables the schedule after 60 days without a commit** to the repository — it e-mails a warning
first; re-enable it under *Actions* → *Monitor*.

The workflow also sends each of its notifications — opened, changed, closed — to Telegram when the
repository secrets `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` are set and passed to its *Alert* step
(`scripts/monitor-alert.sh` reads them; until the workflow passes them, only the issue alerts).

### The server's checks

What the workflow cannot see from outside, `deploy/check.sh` checks on the server every five minutes
from cron ([ADR 0020](adr/0020-observability.md), #296): free disk, memory, load, the containers,
the database and the backup. `deploy/alert.sh` sends a Telegram message when what fails changes and
one when everything passes again, nothing in between; what it last sent is in `~/ritocode/monitor-state`.
A message names the checks and their numbers, never a user, an e-mail or a request. Each run's report
is in `~/ritocode-check.log`; `./check.sh --no-alert` prints one now without sending anything.

**The bot**, once: in Telegram, @BotFather → `/newbot` — a bot for this alone, not bellboy's — and its
token into `production.env` as `TELEGRAM_BOT_TOKEN`. Send the bot any message, open
`https://api.telegram.org/bot<token>/getUpdates`, and put `message.chat.id` in `TELEGRAM_CHAT_ID`.
Either empty: the checks run and say nothing. **Try it**: `MONITOR_DISK_FREE_PERCENT=101 ./check.sh`
sends the disk alert; `./check.sh` right after sends the recovery.

While [maintenance](#maintenance) is on, the containers and the database are not checked. The
thresholds are variables in `production.env` ([production.env.example](../deploy/production.env.example)).

| It says | Means | Look at |
| --- | --- | --- |
| `FAIL disk /` | Less than 15 % of the disk is free. | `df -h`, `du -sh /var/lib/docker /var/backups/ritocode`; `docker system prune` removes old images. Logs are capped by Docker ([Read logs](#read-logs)). |
| `FAIL memory` | Less than 10 % of memory is available. | `docker stats --no-stream`; the API or Umami growing — `dc restart <service>` frees it, and is worth an issue. |
| `FAIL load` | The five-minute load is above two per CPU. | `top`; `docker stats --no-stream`. |
| `FAIL container <service>` | The service is not running, is unhealthy, is restarting, or restarted in the last 15 minutes. | `dc ps`, `dc logs --since 30m <service>`. A loop of restarts is usually a bad setting in `production.env` or the database. |
| `FAIL database` | PostgreSQL did not answer `psql`. | `dc ps postgres`, `dc logs --since 15m postgres`, disk space. |
| `FAIL database size` | The product's database is above 5 GB. | `dc exec postgres psql -U ritocode -c "\dt+ *.*"` for the largest tables; raise `MONITOR_DATABASE_MAX_MB` if the growth is expected. |
| `FAIL database connections` | 80 % or more of `max_connections` are taken. | `dc exec postgres psql -U ritocode -c "select application_name, state, count(*) from pg_stat_activity group by 1, 2"`. |
| `FAIL backup last run` | The last `backup.sh` failed: a dump, or the copy off the server (`BACKUP_REMOTE` empty counts as failed). | `~/ritocode-backup.log`; `rclone lsd <remote>` for the remote; run `./backup.sh` by hand once it is fixed. |
| `FAIL backup age` | No backup both dumped and copied off the server for 26 hours (or ever). | As above; also that cron runs (`crontab -l`). |

`ok   logs api (the oldest line …)` is not an alert: it says how many days of the API's log Docker
keeps, against the 14 that ADR 0020 aims for.

The heartbeat behind the two backup checks is `BACKUP_DIR/heartbeat/last-good` (seconds since the
epoch; `date -d @$(cat …)` reads it), written only when every dump was made and copied off; a failed
run writes `last-failed`. The admin system page shows `last-good` as *Последняя резервная копия*.

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

## Search consoles

Yandex Webmaster and Google Search Console show what is indexed, for which queries, and what is
broken (#303). Russian traffic comes largely from Yandex, so look there first.

### Register the site

1. **Yandex Webmaster** (<https://webmaster.yandex.ru>): add `https://SITE_DOMAIN`; choose the
   *meta tag* check and copy the tag's `content` value only.
2. **Google Search Console** (<https://search.google.com/search-console>): add a *URL prefix*
   property `https://SITE_DOMAIN/`; choose *HTML tag* and copy its `content` value only.
3. **local** Set the repository variables `YANDEX_VERIFICATION` and `GOOGLE_SITE_VERIFICATION`
   (GitHub → Settings → Variables) to those values. The web image is built with them (as
   `VITE_YANDEX_VERIFICATION`, `VITE_GOOGLE_SITE_VERIFICATION`): each puts its meta tag in the head
   of every page; unset, no tag. A value that is not letters, digits, `-` and `_` fails the build.
   The release-images workflow must pass them as build arguments — see deploy/README.md › The search
   consoles.
4. Push to `main` (or re-run the release-images workflow), [release](#release), check that
   `curl -s https://SITE_DOMAIN/ | grep verification` shows both tags, and press *Verify* in each
   console. Keep the tags: the consoles re-check them and drop a site whose tag is gone.
5. In each console submit the sitemap `https://SITE_DOMAIN/sitemap.xml` (Yandex: *Indexing →
   Sitemap files*; Google: *Sitemaps*). `robots.txt` names it too.

### IndexNow

After each release `deploy/release.sh` compares the running web image's `page-hashes.txt` with the
new one's and sends the pages that are new, changed or gone to IndexNow (`deploy/indexnow.sh`), which
shares them with Yandex and Bing. A release of code alone sends nothing but `/`, whose arena turns
daily.

1. **server** Make a key, `openssl rand -hex 16`, put it in `production.env` as `INDEXNOW_KEY`,
   then `dc up -d caddy`. The edge serves it at `https://SITE_DOMAIN/<key>.txt`: check that
   `curl -s https://SITE_DOMAIN/<key>.txt` prints the key.
2. The next release sends every public page once (the old image had no list), then only the changed.
   In Yandex Webmaster, *Indexing → IndexNow* (or *Crawl statistics*) shows the addresses received.

Rotating the key: a new value in `production.env`, `dc up -d caddy`; nothing else keeps it.

### Weekly

Ten minutes, once a week, in both consoles:

- **Coverage** (Yandex: *Indexing → Pages in search*, *Excluded pages*; Google: *Pages*): every card
  and task should be indexed. A page *excluded* as a duplicate, *noindex* or *not found* is a bug —
  check its canonical link and that the sitemap lists it.
- **Queries** (Yandex: *Search queries*; Google: *Performance*): which queries bring people, and
  which pages they land on. Cards with impressions and no clicks want a better title or summary.
- **Errors** (Yandex: *Diagnostics* / *Site quality*; Google: *Pages* errors, *Core Web Vitals*): server
  errors, unreachable robots.txt or sitemap, slow pages. A 5xx burst lines up with
  [an error report](#an-error-was-reported) or a release's maintenance window.
