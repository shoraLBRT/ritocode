#!/usr/bin/env bash
# Fails when a module's EF model no longer matches its last migration.
# The check CI runs; also useful before opening a pull request.
set -uo pipefail

cd "$(dirname "$0")/.."

modules=(Users Auth Problems Workspaces Submissions)
drifted=()

dotnet tool restore >/dev/null

for module in "${modules[@]}"; do
  if dotnet ef migrations has-pending-model-changes \
      --project "src/Modules/Ritocode.Modules.${module}" \
      --startup-project src/Ritocode.DbMigrator \
      --context "${module}DbContext" >/dev/null 2>&1; then
    echo "ok      ${module}"
  else
    echo "DRIFTED ${module}"
    drifted+=("$module")
  fi
done

if [ ${#drifted[@]} -gt 0 ]; then
  echo
  echo "Model and migrations disagree for: ${drifted[*]}"
  echo "Add a migration with ./scripts/db-migrations-add.ps1 -Module <name> -Name <name>"
  exit 1
fi
