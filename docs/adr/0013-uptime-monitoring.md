# 0013 — Uptime monitoring from a scheduled GitHub workflow

- Status: Proposed
- Date: 2026-10-01
- Relates to: [#34](https://github.com/shoraLBRT/ritocode/issues/34), [#135](https://github.com/shoraLBRT/ritocode/issues/135)
- Builds on: [0011](0011-release-images.md), [SPEC.md](../SPEC.md) §9.5

## Context

One VPS runs the whole site (#135). #34 asks for an external check on the readiness endpoint that
alerts the maintainer, an alert before the TLS certificate expires, and an alert within minutes of
the API stopping. The specification names no monitoring service and no alert channel.

What decides the choice:

- **External.** A check on the same server stays silent when the server is what is down.
- **No new account or secret** unless it buys something needed. Every service is one more login the
  maintainer keeps, and its configuration lives outside the repository where no session can read or
  change it.
- **Minutes, not seconds.** The site is free and has few users; an alert five or ten minutes late
  costs nothing that an alert after one minute would save.

## Decision

**A scheduled GitHub Actions workflow is the monitor, and an issue is the alert.**

- `.github/workflows/monitor.yml` runs every five minutes — GitHub's shortest schedule — on GitHub's
  runners, against the addresses in the repository variable `MONITOR_ORIGINS`.
- `scripts/monitor-check.sh` checks that the site's `/health/ready` answers `Healthy` (which reaches
  Caddy, the API and PostgreSQL), and that each address's certificate is valid and more than 14 days
  from expiry. Caddy renews 30 days ahead, so the certificate alert means two weeks of failed renewals.
  A failed request is retried twice, 20 seconds apart, before it counts.
- `scripts/monitor-alert.sh` keeps **one open issue** labelled `monitoring-alert`, mentioning the
  owner so GitHub notifies them by e-mail and in its app. It comments again only when what fails
  changes, and closes the issue when everything passes. The run itself succeeds when it has reported
  a failure, so GitHub's own failed-run e-mails do not add one message every five minutes.
- The alert logic is tested against a fake `gh` on every pull request that touches it.

## Alternatives

- **A hosted uptime service** (UptimeRobot, Better Stack, Healthchecks.io). Checks every minute,
  never late, with certificate checks on some plans, and alerts through Telegram or a phone app. It
  needs the maintainer's account, and its monitors are configured in its dashboard rather than the
  repository. It is the upgrade if GitHub's schedule proves too late or too irregular: the workflow
  can then be deleted, and the readiness endpoint and the runbook's table of what to look at stay.
- **Uptime Kuma or a cron job on the VPS.** No outside service at all, but it is not external: when
  the server is down, so is the monitor.
- **A Telegram message** instead of an issue. The maintainer reads Telegram more than e-mail, but a
  bot token becomes a secret in the repository, and an issue already notifies through GitHub's app and
  keeps the history of every outage. A step sending to Telegram can be added beside the issue later.

## Consequences

- No account, no secret: the workflow's own token opens and closes the issues.
- **Late runs.** GitHub starts scheduled runs late when it is busy, sometimes by ten minutes or
  more, and occasionally skips one. #34's "within minutes" holds most of the time, not always.
- **The schedule is disabled after 60 days without a commit** to a public repository. GitHub warns
  by e-mail first, and the runbook says how to re-enable it.
- Each run costs a minute of runner time; the repository is public, so it is free.
- The check sees what a visitor in GitHub's region sees. If the site is blocked from abroad but
  reachable in Russia, the alert fires although Russian learners are served — and the reverse.
- **Revisit when** an outage goes unnoticed for longer than the late runs explain, or when the site
  has users who would notice first.
