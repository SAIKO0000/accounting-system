<#
.SYNOPSIS
Applies and validates the PostgreSQL core finance schema in a disposable database.

.DESCRIPTION
Creates a disposable database, applies database/postgres-core-finance-schema.sql,
and runs database/schema-validation.sql. The target database is dropped and
recreated each run.
#>

[CmdletBinding()]
param(
  [string]$PsqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe",
  [string]$PgRuntimePath = "C:\Program Files\PostgreSQL\18\pgAdmin 4\runtime",
  [string]$HostName = "localhost",
  [int]$Port = 5432,
  [string]$UserName = "postgres",
  [string]$DatabaseName = "accounting_schema_test",
  [Parameter(Mandatory = $true)]
  [string]$Password
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$schemaPath = Join-Path $repoRoot "database\postgres-core-finance-schema.sql"
$validationPath = Join-Path $repoRoot "database\schema-validation.sql"

if (-not (Test-Path -LiteralPath $PsqlPath)) {
  throw "psql not found: $PsqlPath"
}

if (-not (Test-Path -LiteralPath $schemaPath)) {
  throw "schema not found: $schemaPath"
}

if (-not (Test-Path -LiteralPath $validationPath)) {
  throw "validation script not found: $validationPath"
}

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
  throw "failed to create disposable database"
}

& $PsqlPath `
  -h $HostName `
  -p $Port `
  -U $UserName `
  -d $DatabaseName `
  -v ON_ERROR_STOP=1 `
  -f $schemaPath

if ($LASTEXITCODE -ne 0) {
  throw "failed to apply schema"
}

& $PsqlPath `
  -h $HostName `
  -p $Port `
  -U $UserName `
  -d $DatabaseName `
  -v ON_ERROR_STOP=1 `
  -f $validationPath

if ($LASTEXITCODE -ne 0) {
  throw "schema validation failed"
}

Write-Output "Schema apply-test passed in database '$DatabaseName'."
