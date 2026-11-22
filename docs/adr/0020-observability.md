# 0020 — Observability on one VPS: errors, logs, metrics, alerts

- Status: Accepted (2026-11-22, by the maintainer, as written)
- Date: 2026-11-19
- Relates to: [#293](https://github.com/shoraLBRT/ritocode/issues/293); blocks
  [#294](https://github.com/shoraLBRT/ritocode/issues/294) (errors),
  [#295](https://github.com/shoraLBRT/ritocode/issues/295) (logs),
  [#296](https://github.com/shoraLBRT/ritocode/issues/296) (monitoring)
- Builds on: [0013](0013-uptime-monitoring.md), [SPEC.md](../SPEC.md) §6.2, §9.5

## Context

What production support exists today:

- **From outside** — the *Monitor* workflow ([ADR 0013](0013-uptime-monitoring.md)) checks
  `/health/ready` and the certificates every five minutes from GitHub; the alert is one open issue
  labelled `monitoring-alert` ([scripts/monitor-alert.sh](../../scripts/monitor-alert.sh)).
- **Logs** — the API writes JSON lines to the console with the request id and the user id (#33).
  Docker's `json-file` driver keeps 5 × 10 MB per service
  ([deploy/compose.production.yml](../../deploy/compose.production.yml)), read with
  `docker compose logs` and `jq` over SSH ([RUNBOOK › Read logs](../RUNBOOK.md#read-logs)). On a busy
  day that may be hours of history, not days.
- **The admin system page** (`/admin/system`, #297, SPEC §6.2) reports the versions, the migrations,
  the database's size and the health of Umami and the feedback App. Its `backup` part is always
  absent: nothing records a backup.
- **Backups** — [deploy/backup.sh](../../deploy/backup.sh) runs from cron with `set -eu`. A failure
  stops the script and leaves nothing anyone reads.

What nobody sees: a server exception (a 500 is a log line nobody reads), an error in the browser, a
disk filling up, memory pressure, a container restarting in a loop, a failed backup.

What decides the choice:

- **One small VPS in Russia** (SPEC §9.5) runs Caddy, the API, the static web, PostgreSQL and
  Umami. Its size is not chosen yet (#134). **This ADR assumes 2 vCPU, 2 GB RAM and a 30 GB disk**
  — the cheap end of what Timeweb or Selectel rent — and every figure below is weighed against that.
  If the maintainer rents more, the heavier options get cheaper, not different.
- **152-FZ.** Logs and error records carry user ids, which are personal data; they stay on servers in
  Russia. Anything that leaves the country — an alert, a hosted service — carries no user id, no
  e-mail, no request body: only a check's name and its numbers.
- **One maintainer.** Every service is one more thing to update, back up and log in to. The cheapest
  option that answers "is it broken, and why" wins over the most capable one.
- **No new account unless it buys something needed** (as in ADR 0013).

### What the stack costs today

These are **estimates**, from the usual footprint of each image at low traffic, not measurements on
the server (none exists yet): Caddy ×2 ~40–80 MB, the API ~150–300 MB, PostgreSQL ~100–300 MB,
Umami (Node) ~150–300 MB — **about 0.5–1 GB of the 2 GB**, leaving roughly 1 GB for the operating
system's cache, a release (two API containers briefly) and anything this ADR adds. Measuring it on
the real server is the first thing #296's checks will do.

## Options

The RAM and disk figures are **estimates** from the projects' documented or commonly reported
footprints at low traffic; none was measured here.

### Errors — server exceptions and browser errors

| | What it is | RAM | Disk | Data leaves Russia | Cost to the maintainer |
| --- | --- | --- | --- | --- | --- |
| **E1. In the product's PostgreSQL** | A table of error groups and occurrences; a middleware records unhandled exceptions, `POST /api/v1/client-errors` records the browser's; `/admin/errors` shows them | ~0 (inside the API) | Small; capped by a retention rule | No | Code to write (#294) and keep; no new service |
| **E2. GlitchTip, self-hosted** | A Sentry-compatible service: the .NET and browser Sentry SDKs send to it, it groups, alerts, has its own UI | ~300–600 MB: web, worker, Valkey/Redis; its own database (can share the PostgreSQL) | ~1 GB+ with its event retention | No | A service to update and back up, its own login, its own address behind Caddy |
| **E3. Hosted (Sentry, Bugsnag…)** | The same, run by someone else | 0 | 0 | **Yes** — stack traces, routes, request ids, and user ids unless scrubbed perfectly | An account; 152-FZ risk |

E2 and E3 group better (source maps, release tracking, breadcrumbs) than anything #294 will build.
At Ritocode's traffic — few users, a handful of distinct errors — grouping by type and top frames is
enough, and E2's ~0.5 GB is a quarter of the assumed server.

### Logs — retention and reading without SSH

| | What it is | RAM | Disk | Reads without SSH |
| --- | --- | --- | --- | --- |
| **L1. Dozzle** | One container reading Docker's own log files through the Docker socket; live tail and search in a browser | ~20–50 MB | None of its own — shows what Docker keeps | Yes, behind Caddy |
| **L2. Loki + Grafana** (+ Promtail/Alloy) | Logs shipped into Loki, queried in Grafana | ~300–600 MB for the three | Loki's chunks, own retention | Yes, with LogQL |
| **L3. Recent errors only, in the admin area** | No log viewer; the error groups of E1 carry request ids, the rest stays SSH | 0 | 0 | Only errors |

Retention is a separate question from the viewer: whatever reads them, Docker's files are the logs.
Raising the cap (L1, L3) keeps everything as it is; L2 adds a second copy with its own retention.

### Metrics — server and application

| | What it is | RAM | Disk |
| --- | --- | --- | --- |
| **M1. Prometheus + Grafana + node-exporter + cAdvisor** | Time series of everything, dashboards, Alertmanager rules | ~0.4–0.9 GB (cAdvisor and Prometheus are the heavy ones); with L2's Loki the whole Grafana stack is **~0.7–1.5 GB** | 1–3 GB for a few weeks of series |
| **M2. Threshold checks in a script** | A shell script on the VPS, every five minutes from cron: free disk, memory, load, containers restarting or unhealthy, the database's size and connections, the backup's heartbeat; alerts on a change, as #34 does | ~0 (runs for a second) | A state file |
| **M3. Application metrics at `/metrics`** | OpenTelemetry meters in the API (request rate, latency, 5xx), exported for Prometheus, internal only | ~10–30 MB in the API | Only if something scrapes them |

M1 answers questions M2 cannot ("how did latency change over the week"), but nobody is asking them
yet, and on the assumed server it does not fit beside the stack with any margin.

### Alerts — where they reach the maintainer

| | What it is | Secret | Leaves Russia |
| --- | --- | --- | --- |
| **A1. The `monitoring-alert` issue** (today) | GitHub e-mail and app notifications; keeps the history | None | Only check names |
| **A2. Telegram** | A bot message; the maintainer reads Telegram most, and already runs a Telegram assistant (bellboy) | A bot token and chat id, in `production.env` and/or a repository secret | Only check names and numbers — by rule, never a user id |
| **A3. E-mail from the server** | SMTP from the VPS | SMTP credentials | Depends on the provider |

## Decision

**The light option: everything inside the stack that already runs, one small viewer, a script,
and Telegram beside the issue.** The whole addition is estimated at **under 100 MB of RAM** and a
few hundred MB of disk for logs, against ~0.7–1.5 GB for the Grafana stack.

1. **Errors — E1, in the product's PostgreSQL, shown in the admin area.** Unhandled server
   exceptions and browser errors are grouped by a fingerprint and listed on `/admin/errors`. No
   request body, no address, no user id in an error record — the request id leads to the log lines,
   which have the rest. Occurrences are pruned (kept 30 days, and at most the latest 50 per group);
   groups stay until resolved. GlitchTip (E2) is the upgrade if grouping by type and frames proves
   too coarse; the browser's endpoint and the request ids stay.
2. **Logs — L1, Dozzle, behind Caddy, for admins only; retention by Docker's driver.**
   - **Retention**: `json-file` with `max-size: 20m`, `max-file: 10` and `compress: "true"` for the
     API and Caddy (200 MB each before compression), the current 5 × 10 MB for the rest. That is a
     cap, not a number of days: **14 days, or the cap if it comes first**. #296's checks report the
     age of the oldest API line, so the real number of days is known rather than guessed.
   - **Reading**: Dozzle at `/admin/logs/` on the site's own address (`DOZZLE_BASE`), with Caddy's
     `forward_auth` asking the API whether the session cookie belongs to an admin — the same sign-in
     as the rest of the admin area, no second password. Dozzle reads the Docker socket only through a
     read-only socket proxy that allows container listing and logs, nothing else, because the raw
     socket is root on the host. Caddy `basic_auth` is the fallback if `forward_auth` proves awkward.
   - Loki + Grafana (L2) is not built; revisit with M1.
3. **Metrics — M2, threshold checks in a script; M3 later.** `deploy/check.sh` runs from cron every
   five minutes on the VPS. It checks free disk (alert below 15 %), available memory, load,
   containers restarting or unhealthy (`docker ps`/`inspect`), the database's size and connection
   count, and the backup heartbeat. `backup.sh` writes the heartbeat only when the local dump **and**
   the off-site copy succeeded; the check alerts when the last good backup is older than 26 hours.
   The API reads the same heartbeat for `/admin/system`'s `backup` part. Request rate, latency and
   5xx as metrics (M3) are not built now: the 5xx reach the maintainer as errors (decision 1). When
   they are wanted, the API gets OpenTelemetry meters exported at a `/metrics` that Caddy does not
   route, which Prometheus (M1) can scrape later without changing the API again.
4. **Alerts — A1 as today, plus A2, Telegram.**
   - The *Monitor* workflow keeps its issue and also sends Telegram (the bot token and chat id as
     repository secrets).
   - The server's own checks (decision 3) and new or reopened error groups (decision 1) send
     Telegram from the VPS (the bot token and chat id in `production.env`). They follow #34's rule:
     one message when what fails changes, one on recovery, state kept in a file.
   - **A message names the check and its numbers, never a user id, a request body or an e-mail** —
     Telegram's servers are outside Russia.
   - A dedicated bot, not bellboy's: one token per purpose, revoked alone.

## Consequences

- **What each blocked issue builds**, once this is accepted:
  - **#294 (errors)**: the error tables and their migration, the exception middleware with
    fingerprints, `POST /api/v1/client-errors` (anonymous, rate-limited, size-capped), the browser's
    handlers and error boundary, `/admin/errors` with resolve and reopen, the pruning rule above, and
    the Telegram message on a new or reopened group.
  - **#295 (logs)**: the logging caps above in `deploy/compose.production.yml`, Dozzle and a
    read-only socket proxy in the production Compose file, the `/admin/logs/` route and its
    `forward_auth` check, RUNBOOK › Read logs rewritten around the viewer, deploy/README.md's
    retention, and the privacy text (#128) saying logs are kept up to 14 days.
  - **#296 (monitoring)**: `deploy/check.sh` and its cron line, the backup heartbeat in
    `backup.sh`, `backup` on `/admin/system`, the Telegram step in the *Monitor* workflow, the
    bot's variables in `production.env.example`, tests of the alert rule against fakes as
    `monitor-test.sh` does, and RUNBOOK › Monitoring's table extended with each new alert.
- **Fits the assumed server** with margin: under 100 MB of RAM added, and a disk cap of about 0.5 GB
  for logs before compression. The Grafana stack would have taken half the server.
- **No new account**, one new secret (the bot token), kept in two places — the repository for the
  workflow and `production.env` for the server — and rotated together (RUNBOOK › Rotate a secret).
- **No history of metrics.** The checks say what is wrong now; nothing shows a trend. A slow leak is
  seen when it crosses a threshold, not before.
- **The server's alerts depend on the server.** If the VPS is down, `check.sh` is silent — the
  *Monitor* workflow from outside still sees it, which is why both stay.
- **Telegram from a Russian VPS** works today; if it is ever blocked or throttled, the issue (A1)
  still carries the external alerts, and the server's checks would need another channel. Russian
  logs and error records are unaffected either way.
- **Dozzle adds a dependency on the Docker socket**, narrowed by the proxy. Its retention is
  Docker's: a container removed by a release keeps its logs only until it is recreated — the same as
  `docker compose logs` today.
- **Revisit when** the site has enough traffic that trends matter (M1 + L2, on a larger server), or
  when grouping errors in the product's own tables stops being enough (E2).

## Open questions for the maintainer

1. **The server size.** Is 2 GB RAM / 30 GB disk the right assumption for #134? Above ~4 GB the
   Grafana stack (M1 + L2) becomes affordable, though not needed.
2. **Server alerts as issues too?** This proposes Telegram only for the server's own checks. Opening
   the `monitoring-alert` issue from the VPS as well would need a GitHub token on the server (or the
   feedback App's issue permission reused); proposed: no.
3. **Retention** of 14 days for logs and 30 days for error occurrences — or other numbers for the
   privacy text.
