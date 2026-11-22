#!/bin/sh
# The daily backup, on the server, from deploy/ (docs/SPEC.md §9.5, #136): a dump of the product's
# database and of Umami's, the last seven days of them kept in BACKUP_DIR, and each new dump copied
# off the server with rclone to BACKUP_REMOTE when it is set. Run by cron (deploy/README.md).
#
# The heartbeat (#296, ADR 0020): a run in which every dump was made **and** copied off the server
# writes the time, in seconds since the epoch, to BACKUP_DIR/heartbeat/last-good; a run that fails
# writes it to BACKUP_DIR/heartbeat/last-failed. check.sh alerts on a failed last run and on a last
# good one older than 26 hours; the admin system page shows last-good. With BACKUP_REMOTE empty no
# copy leaves the server, so the run counts as failed once the dumps are made.
set -eu

cd "$(dirname "$0")"

# BACKUP_DIR and BACKUP_REMOTE may come from production.env, as everything else on the server does.
if [ -f production.env ]; then
	BACKUP_DIR="${BACKUP_DIR:-$(sed -n 's/^BACKUP_DIR=//p' production.env)}"
	BACKUP_REMOTE="${BACKUP_REMOTE:-$(sed -n 's/^BACKUP_REMOTE=//p' production.env)}"
fi
BACKUP_DIR="${BACKUP_DIR:-/var/backups/ritocode}"
BACKUP_REMOTE="${BACKUP_REMOTE:-}"

compose="docker compose -f compose.production.yml --env-file production.env"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
heartbeat="$BACKUP_DIR/heartbeat"
mkdir -p "$BACKUP_DIR" "$heartbeat"

# Any failure from here on — a dump, a copy, the pruning — is recorded for check.sh.
trap 'status=$?; if [ "$status" -ne 0 ]; then date -u +%s >"$heartbeat/last-failed" || true; fi' EXIT

for database in ritocode umami; do
	file="$BACKUP_DIR/$database-$stamp.dump"
	# Custom format: compressed, and pg_restore can restore it into an empty database.
	$compose exec -T postgres pg_dump --username ritocode --format custom "$database" >"$file.partial"
	mv "$file.partial" "$file"
	echo "Dumped $database to $file"

	if [ -n "$BACKUP_REMOTE" ]; then
		rclone copy "$file" "$BACKUP_REMOTE/$database/"
		echo "Copied it to $BACKUP_REMOTE/$database/"
	fi
done

# Seven days kept on the server; the remote keeps by its own lifecycle rule, which must also be seven
# days — the privacy policy promises a deleted account leaves the backups within a week (RUNBOOK.md ›
# Personal data).
find "$BACKUP_DIR" -name '*.dump' -type f -mtime +6 -delete

if [ -z "$BACKUP_REMOTE" ]; then
	echo "BACKUP_REMOTE is empty: the dumps stay on the server, so this is no good backup" >&2
	exit 1
fi
date -u +%s >"$heartbeat/last-good.partial"
mv "$heartbeat/last-good.partial" "$heartbeat/last-good"
echo "Heartbeat written to $heartbeat/last-good"
