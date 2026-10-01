#!/bin/sh
# The daily backup, on the server, from deploy/ (docs/SPEC.md §9.5, #136): a dump of the product's
# database and of Umami's, the last seven days of them kept in BACKUP_DIR, and each new dump copied
# off the server with rclone to BACKUP_REMOTE when it is set. Run by cron (deploy/README.md).
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
mkdir -p "$BACKUP_DIR"

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

# Seven days kept on the server; the remote keeps by its own lifecycle rule.
find "$BACKUP_DIR" -name '*.dump' -type f -mtime +6 -delete
