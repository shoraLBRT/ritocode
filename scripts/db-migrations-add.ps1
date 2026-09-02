#Requires -Version 7
<#
.SYNOPSIS
  Adds a migration to one module's DbContext.
.DESCRIPTION
  Wraps `dotnet ef migrations add` so the module name is the only thing to remember: the context
  name, project paths and output directory all follow from it.
.PARAMETER Module
  Module name as it appears in src/Modules/Ritocode.Modules.<Module>, e.g. Problems.
.PARAMETER Name
  Migration name, e.g. AddProblemLanguage.
.EXAMPLE
  ./scripts/db-migrations-add.ps1 -Module Problems -Name AddProblemLanguage
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Users', 'Auth', 'Problems', 'Workspaces', 'Submissions')]
    [string] $Module,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*$')]
    [string] $Name
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$project = "src/Modules/Ritocode.Modules.$Module"
if (-not (Test-Path $project)) { throw "No module project at $project" }

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

dotnet ef migrations add $Name `
    --project $project `
    --startup-project src/Ritocode.DbMigrator `
    --context "$($Module)DbContext" `
    --output-dir Persistence/Migrations
if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations add failed' }

Write-Host "`nAdded migration '$Name' to $Module. Review it, then apply with:"
Write-Host '  dotnet run --project src/Ritocode.DbMigrator'
