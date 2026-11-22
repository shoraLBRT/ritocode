#!/bin/sh
# A release, on the server, from deploy/ (docs/SPEC.md §9.5, #136):
#
#   ./release.sh <full commit>
#
# pulls the images built for that commit, switches maintenance on (#298), applies migrations, ingests the content shipped in the API
# image stamped with the commit, restarts the services on the new images and waits for the API's
# health. Rolling back is releasing the previous commit. scripts/release.sh runs this from a checkout.
set -eu

version="${1:?usage: ./release.sh <full commit sha>}"
cd "$(dirname "$0")"

# The shell's value wins over production.env, so every step below runs the same images.
export RITOCODE_VERSION="$version"
compose="docker compose -f compose.production.yml --env-file production.env"

# The API mounts the backup's heartbeat directory (#296); made here, by this user, so Docker does not
# make it as root, where backup.sh could not write.
backup_dir="$(sed -n 's/^BACKUP_DIR=//p' production.env)"
mkdir -p "${backup_dir:-/var/backups/ritocode}/heartbeat"

echo "Releasing $version"
$compose pull --quiet api migrate web

# Maintenance (#298): the edge answers every request with its 503 page while the file exists, so
# nobody uses the old API against a migrated database. A release that fails leaves it on — the site
# is broken either way — and the next release, a rollback included, takes it off.
echo "Maintenance on"
touch maintenance/on

echo "Applying migrations..."
$compose run --rm migrate

echo "Ingesting content..."
$compose run --rm migrate ingest content "$version"

echo "Starting..."
$compose up -d --remove-orphans

echo "Waiting for the API..."
attempt=0
until $compose exec -T caddy wget -qO- http://api:8080/health/ready >/dev/null 2>&1; do
	attempt=$((attempt + 1))
	if [ "$attempt" -ge 30 ]; then
		echo "The API did not become healthy; the previous release is gone and maintenance stays on. Release the previous commit to roll back." >&2
		$compose logs --tail 50 api >&2
		exit 1
	fi
	sleep 2
done

rm -f maintenance/on
echo "Maintenance off"

echo "Released $version"
