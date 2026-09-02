#Requires -Version 7
<#
.SYNOPSIS
  One command to get a working local environment: dependencies up, schema migrated.
.DESCRIPTION
  Safe to re-run — compose and the migrator are both idempotent.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Test-Path .env)) {
    Write-Host 'Creating .env from .env.example'
    Copy-Item .env.example .env
}

$settings = @{}
foreach ($line in Get-Content .env) {
    if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*)$') {
        $settings[$Matches[1]] = $Matches[2]
    }
}

Write-Host 'Starting dependencies...'
# --wait is applied only to the long-running services: it treats a container that exits as a
# failure, and minio-init is a one-shot that exits 0 by design. The init runs separately so its
# exit code is still checked.
docker compose up -d --wait postgres minio
if ($LASTEXITCODE -ne 0) { throw 'docker compose failed' }

docker compose run --rm minio-init
if ($LASTEXITCODE -ne 0) { throw 'bucket initialisation failed' }

$env:Database__ConnectionString = "Host=localhost;Port=$($settings.POSTGRES_PORT);Database=$($settings.POSTGRES_DB);Username=$($settings.POSTGRES_USER);Password=$($settings.POSTGRES_PASSWORD)"

Write-Host 'Applying migrations...'
dotnet run --project src/Ritocode.DbMigrator
if ($LASTEXITCODE -ne 0) { throw 'migrations failed' }

Write-Host @"

Ready.

  PostgreSQL     localhost:$($settings.POSTGRES_PORT)  (db $($settings.POSTGRES_DB), user $($settings.POSTGRES_USER))
  MinIO API      localhost:$($settings.MINIO_PORT)
  MinIO console  http://localhost:$($settings.MINIO_CONSOLE_PORT)

Run the API:   dotnet run --project src/Ritocode.Api
Run the tests: dotnet test Ritocode.slnx
Stop:          docker compose down          (add -v to also discard the data)
"@
