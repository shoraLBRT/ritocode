#!/bin/sh
# Restores a dump made by backup.sh, on the server, from deploy/ (#136):
#
#   ./restore.sh ritocode /var/backups/ritocode/ritocode-20261001T031500Z.dump
#
# The database is dropped and made again empty, the dump restored into it, and the services that use
# it restarted. Everything written since the dump is lost — which is what a restore is for.
set -eu

database="${1:?usage: ./restore.sh <ritocode|umami> <dump file>}"
dump="${2:?usage: ./restore.sh <ritocode|umami> <dump file>}"
cd "$(dirname "$0")"

case "$database" in
	ritocode) owner=ritocode; users=api ;;
	umami) owner=umami; users=umami ;;
	*) echo "The database is ritocode or umami." >&2; exit 2 ;;
esac

compose="docker compose -f compose.production.yml --env-file production.env"

echo "Stopping $users..."
$compose stop $users

echo "Recreating $database empty..."
$compose exec -T postgres dropdb --username ritocode --if-exists --force "$database"
$compose exec -T postgres createdb --username ritocode --owner "$owner" "$database"

echo "Restoring $dump..."
$compose exec -T postgres pg_restore --username ritocode --dbname "$database" --no-owner --role "$owner" <"$dump"

echo "Starting $users..."
$compose up -d $users

echo "Restored $database from $dump"
