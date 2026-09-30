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
docker compose up -d --wait postgres

export Database__ConnectionString="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

echo "Applying migrations..."
dotnet run --project src/Ritocode.DbMigrator

cat <<SUMMARY

Ready.

  PostgreSQL     localhost:${POSTGRES_PORT}  (db ${POSTGRES_DB}, user ${POSTGRES_USER})

Run the API:   dotnet run --project src/Ritocode.Api
Run the tests: dotnet test Ritocode.slnx
Stop:          docker compose down          (add -v to also discard the data)
SUMMARY
