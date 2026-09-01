#!/usr/bin/env bash
# One command to get a working local environment: dependencies up, schema migrated.
# Safe to re-run — compose and the migrator are both idempotent.
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "Creating .env from .env.example"
  cp .env.example .env
fi

# shellcheck disable=SC1091
set -a; . ./.env; set +a

echo "Starting dependencies..."
# --wait is applied only to the long-running services: it treats a container that exits as a
# failure, and minio-init is a one-shot that exits 0 by design. The init runs separately so its
# exit code is still checked.
docker compose up -d --wait postgres minio
docker compose run --rm minio-init

export Database__ConnectionString="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

echo "Applying migrations..."
dotnet run --project src/Ritocode.DbMigrator

cat <<SUMMARY

Ready.

  PostgreSQL     localhost:${POSTGRES_PORT}  (db ${POSTGRES_DB}, user ${POSTGRES_USER})
  MinIO API      localhost:${MINIO_PORT}
  MinIO console  http://localhost:${MINIO_CONSOLE_PORT}

Run the API:   dotnet run --project src/Ritocode.Api
Run the tests: dotnet test Ritocode.slnx
Stop:          docker compose down          (add -v to also discard the data)
SUMMARY
