<#
.SYNOPSIS
Transforms staged legacy account rows into core.accounts.

.DESCRIPTION
Reads raw rows from core.migration_staging_records and maps them into the modern
chart of accounts. The legacy account-class mapping must be supplied explicitly
as JSON so Sage class semantics are not guessed in code.

.EXAMPLE
.\tools\transform-staged-accounts.ps1 `
  -MigrationBatchId 1 `
  -CompanyId 1 `
  -ClassMappingPath .\account-class-map.json `
  -Password "<postgres-password>" `
  -Apply
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [long]$MigrationBatchId,

  [Parameter(Mandatory = $true)]
  [long]$CompanyId,

  [Parameter(Mandatory = $true)]
  [string]$ClassMappingPath,

  [string]$SourceTable = "tAccount",
  [string]$LegacyIdField = "lId",
  [string]$CodeField = "sAcctId",
  [string]$NameField = "sName",
  [string]$ClassField = "nAcctClass",
  [string]$InactiveField = "bInactive",
  [string]$BankFlagField = "bDoBRec",

  [switch]$Apply,

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

if (-not (Test-Path -LiteralPath $ClassMappingPath)) {
  throw "ClassMappingPath not found: $ClassMappingPath"
}

if (-not (Test-Path -LiteralPath $PsqlPath)) {
  throw "psql not found: $PsqlPath"
}

$allowedNatures = @("asset", "liability", "equity", "revenue", "expense")
$classMapping = Get-Content -LiteralPath $ClassMappingPath -Raw | ConvertFrom-Json
$mappingRows = @()

foreach ($property in $classMapping.PSObject.Properties) {
  $nature = [string]$property.Value
  if ($allowedNatures -notcontains $nature) {
    throw "Invalid account nature '$nature' for legacy class '$($property.Name)'."
  }

  $mappingRows += [PSCustomObject]@{
    legacy_class = [string]$property.Name
    nature = $nature
  }
}

if ($mappingRows.Count -eq 0) {
  throw "Class mapping cannot be empty."
}

$env:PATH = "$(Split-Path -Parent $PsqlPath);$PgRuntimePath;$env:PATH"
$env:PGPASSWORD = $Password

function ConvertTo-SqlLiteral {
  param([Parameter(Mandatory = $true)][string]$Value)
  return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-Psql {
  param([Parameter(Mandatory = $true)][string]$Sql)

  & $PsqlPath `
    -h $HostName `
    -p $Port `
    -U $UserName `
    -d $DatabaseName `
    -v ON_ERROR_STOP=1 `
    -c $Sql

  if ($LASTEXITCODE -ne 0) {
    throw "psql command failed"
  }
}

$mappingValues = ($mappingRows | ForEach-Object {
  "(" + (ConvertTo-SqlLiteral $_.legacy_class) + ", " + (ConvertTo-SqlLiteral $_.nature) + ")"
}) -join ",`n    "

$sourceTableLiteral = ConvertTo-SqlLiteral $SourceTable
$legacyIdFieldLiteral = ConvertTo-SqlLiteral $LegacyIdField
$codeFieldLiteral = ConvertTo-SqlLiteral $CodeField
$nameFieldLiteral = ConvertTo-SqlLiteral $NameField
$classFieldLiteral = ConvertTo-SqlLiteral $ClassField
$inactiveFieldLiteral = ConvertTo-SqlLiteral $InactiveField
$bankFlagFieldLiteral = ConvertTo-SqlLiteral $BankFlagField

$previewSql = @"
WITH class_map(legacy_class, nature) AS (
  VALUES
    $mappingValues
),
staged AS (
  SELECT
    id AS staging_record_id,
    source_key,
    raw_data ->> $legacyIdFieldLiteral AS legacy_account_id,
    raw_data ->> $codeFieldLiteral AS code,
    raw_data ->> $nameFieldLiteral AS name,
    raw_data ->> $classFieldLiteral AS legacy_account_class,
    COALESCE(raw_data ->> $inactiveFieldLiteral, 'false') AS inactive_value,
    COALESCE(raw_data ->> $bankFlagFieldLiteral, 'false') AS bank_flag_value
  FROM core.migration_staging_records
  WHERE migration_batch_id = $MigrationBatchId
    AND source_table = $sourceTableLiteral
),
normalized AS (
  SELECT
    staged.*,
    class_map.nature,
    CASE WHEN lower(inactive_value) IN ('true', '1', 'yes', '-1') THEN 'inactive' ELSE 'active' END AS status,
    CASE WHEN lower(bank_flag_value) IN ('true', '1', 'yes', '-1') THEN true ELSE false END AS is_bank_account
  FROM staged
  LEFT JOIN class_map ON class_map.legacy_class = staged.legacy_account_class
)
SELECT
  staging_record_id,
  legacy_account_id,
  code,
  name,
  legacy_account_class,
  nature,
  status,
  is_bank_account,
  CASE
    WHEN code IS NULL OR code = '' THEN 'missing code'
    WHEN name IS NULL OR name = '' THEN 'missing name'
    WHEN nature IS NULL THEN 'unmapped account class'
    ELSE 'ok'
  END AS validation_status
FROM normalized
ORDER BY staging_record_id;
"@

if (-not $Apply) {
  Invoke-Psql -Sql $previewSql
  Write-Output "Preview only. Re-run with -Apply to insert/update core.accounts."
  return
}

$applySql = @"
WITH class_map(legacy_class, nature) AS (
  VALUES
    $mappingValues
),
staged AS (
  SELECT
    id AS staging_record_id,
    source_key,
    raw_data ->> $legacyIdFieldLiteral AS legacy_account_id,
    raw_data ->> $codeFieldLiteral AS code,
    raw_data ->> $nameFieldLiteral AS name,
    raw_data ->> $classFieldLiteral AS legacy_account_class,
    COALESCE(raw_data ->> $inactiveFieldLiteral, 'false') AS inactive_value,
    COALESCE(raw_data ->> $bankFlagFieldLiteral, 'false') AS bank_flag_value
  FROM core.migration_staging_records
  WHERE migration_batch_id = $MigrationBatchId
    AND source_table = $sourceTableLiteral
),
normalized AS (
  SELECT
    staged.*,
    class_map.nature,
    CASE WHEN lower(inactive_value) IN ('true', '1', 'yes', '-1') THEN 'inactive' ELSE 'active' END AS status,
    CASE WHEN lower(bank_flag_value) IN ('true', '1', 'yes', '-1') THEN true ELSE false END AS is_bank_account
  FROM staged
  JOIN class_map ON class_map.legacy_class = staged.legacy_account_class
  WHERE staged.code IS NOT NULL
    AND staged.code <> ''
    AND staged.name IS NOT NULL
    AND staged.name <> ''
),
upserted AS (
  INSERT INTO core.accounts (
    company_id,
    code,
    name,
    nature,
    status,
    is_bank_account,
    legacy_account_id,
    legacy_account_class
  )
  SELECT
    $CompanyId,
    code,
    name,
    CAST(nature AS core.account_nature),
    CAST(status AS core.account_status),
    is_bank_account,
    legacy_account_id,
    legacy_account_class
  FROM normalized
  ON CONFLICT (company_id, code) DO UPDATE
  SET
    name = EXCLUDED.name,
    nature = EXCLUDED.nature,
    status = EXCLUDED.status,
    is_bank_account = EXCLUDED.is_bank_account,
    legacy_account_id = EXCLUDED.legacy_account_id,
    legacy_account_class = EXCLUDED.legacy_account_class,
    updated_at = now()
  RETURNING id, code, legacy_account_id
),
refs AS (
  INSERT INTO core.migration_source_refs (
    migration_batch_id,
    source_table,
    source_key,
    target_table,
    target_id,
    raw_hash
  )
  SELECT
    $MigrationBatchId,
    $sourceTableLiteral,
    COALESCE(upserted.legacy_account_id, upserted.code),
    'core.accounts',
    upserted.id,
    staging.raw_hash
  FROM upserted
  JOIN core.migration_staging_records staging
    ON staging.migration_batch_id = $MigrationBatchId
   AND staging.source_table = $sourceTableLiteral
   AND staging.raw_data ->> $legacyIdFieldLiteral = upserted.legacy_account_id
  ON CONFLICT (migration_batch_id, source_table, source_key, target_table) DO UPDATE
  SET target_id = EXCLUDED.target_id,
      raw_hash = EXCLUDED.raw_hash
  RETURNING id
)
SELECT
  (SELECT count(*) FROM upserted) AS accounts_upserted,
  (SELECT count(*) FROM refs) AS source_refs_upserted;
"@

Invoke-Psql -Sql $applySql
