<#
.SYNOPSIS
Creates and seeds the local accounting_dev PostgreSQL database.
#>

[CmdletBinding()]
param(
  [string]$PsqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe",
  [string]$PgRuntimePath = "C:\Program Files\PostgreSQL\18\pgAdmin 4\runtime",
  [string]$HostName = "localhost",
  [int]$Port = 5432,
  [string]$UserName = "postgres",
  [string]$DatabaseName = "accounting_dev",
  [Parameter(Mandatory = $true)]
  [string]$Password
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$schemaPath = Join-Path $repoRoot "database\postgres-core-finance-schema.sql"
$seedPath = Join-Path $repoRoot "database\dev-seed.sql"

$env:PATH = "$(Split-Path -Parent $PsqlPath);$PgRuntimePath;$env:PATH"
$env:PGPASSWORD = $Password

& $PsqlPath `
  -h $HostName `
  -p $Port `
  -U $UserName `
  -d postgres `
  -v ON_ERROR_STOP=1 `
  -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName';" `
  -c "DROP DATABASE IF EXISTS $DatabaseName;" `
  -c "CREATE DATABASE $DatabaseName;"

if ($LASTEXITCODE -ne 0) {
  throw "failed to create development database"
}

& $PsqlPath `
  -h $HostName `
  -p $Port `
  -U $UserName `
  -d $DatabaseName `
  -v ON_ERROR_STOP=1 `
  -f $schemaPath `
  -f $seedPath

if ($LASTEXITCODE -ne 0) {
  throw "failed to apply schema or seed data"
}

Write-Output "Development database '$DatabaseName' is ready."
