<#
.SYNOPSIS
Transforms staged legacy customer/vendor rows into core.business_partners.

.DESCRIPTION
Reads raw rows from core.migration_staging_records and maps them into the modern
business partner table. The transform is intentionally conservative: it preserves
legacy IDs and only maps common master-data fields.
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [long]$MigrationBatchId,

  [Parameter(Mandatory = $true)]
  [long]$CompanyId,

  [Parameter(Mandatory = $true)]
  [ValidateSet("customer", "vendor")]
  [string]$PartnerType,

  [Parameter(Mandatory = $true)]
  [string]$SourceTable,

  [string]$LegacyIdField = "lId",
  [string]$NameField = "sName",
  [string]$ContactNameField = "sContact",
  [string]$EmailField = "sEmail",
  [string]$PhoneField = "sPhone1",
  [string]$TaxIdentifierField = "sTaxId",
  [string]$InactiveField = "bInactive",

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

if (-not (Test-Path -LiteralPath $PsqlPath)) {
  throw "psql not found: $PsqlPath"
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

$sourceTableLiteral = ConvertTo-SqlLiteral $SourceTable
$partnerTypeLiteral = ConvertTo-SqlLiteral $PartnerType
$legacyIdFieldLiteral = ConvertTo-SqlLiteral $LegacyIdField
$nameFieldLiteral = ConvertTo-SqlLiteral $NameField
$contactNameFieldLiteral = ConvertTo-SqlLiteral $ContactNameField
$emailFieldLiteral = ConvertTo-SqlLiteral $EmailField
$phoneFieldLiteral = ConvertTo-SqlLiteral $PhoneField
$taxIdentifierFieldLiteral = ConvertTo-SqlLiteral $TaxIdentifierField
$inactiveFieldLiteral = ConvertTo-SqlLiteral $InactiveField

$previewSql = @"
WITH staged AS (
  SELECT
    id AS staging_record_id,
    source_key,
    raw_data ->> $legacyIdFieldLiteral AS legacy_partner_id,
    raw_data ->> $nameFieldLiteral AS name,
    NULLIF(raw_data ->> $contactNameFieldLiteral, '') AS contact_name,
    NULLIF(raw_data ->> $emailFieldLiteral, '') AS email,
    NULLIF(raw_data ->> $phoneFieldLiteral, '') AS phone,
    NULLIF(raw_data ->> $taxIdentifierFieldLiteral, '') AS tax_identifier,
    COALESCE(raw_data ->> $inactiveFieldLiteral, 'false') AS inactive_value
  FROM core.migration_staging_records
  WHERE migration_batch_id = $MigrationBatchId
    AND source_table = $sourceTableLiteral
),
normalized AS (
  SELECT
    staged.*,
    CASE WHEN lower(inactive_value) IN ('true', '1', 'yes', '-1') THEN false ELSE true END AS is_active
  FROM staged
)
SELECT
  staging_record_id,
  legacy_partner_id,
  $partnerTypeLiteral AS partner_type,
  name,
  contact_name,
  email,
  phone,
  tax_identifier,
  is_active,
  CASE
    WHEN name IS NULL OR name = '' THEN 'missing name'
    ELSE 'ok'
  END AS validation_status
FROM normalized
ORDER BY staging_record_id;
"@

if (-not $Apply) {
  Invoke-Psql -Sql $previewSql
  Write-Output "Preview only. Re-run with -Apply to insert/update core.business_partners."
  return
}

$applySql = @"
WITH staged AS (
  SELECT
    id AS staging_record_id,
    source_key,
    raw_hash,
    raw_data ->> $legacyIdFieldLiteral AS legacy_partner_id,
    raw_data ->> $nameFieldLiteral AS name,
    NULLIF(raw_data ->> $contactNameFieldLiteral, '') AS contact_name,
    NULLIF(raw_data ->> $emailFieldLiteral, '') AS email,
    NULLIF(raw_data ->> $phoneFieldLiteral, '') AS phone,
    NULLIF(raw_data ->> $taxIdentifierFieldLiteral, '') AS tax_identifier,
    COALESCE(raw_data ->> $inactiveFieldLiteral, 'false') AS inactive_value
  FROM core.migration_staging_records
  WHERE migration_batch_id = $MigrationBatchId
    AND source_table = $sourceTableLiteral
),
normalized AS (
  SELECT
    staged.*,
    CASE WHEN lower(inactive_value) IN ('true', '1', 'yes', '-1') THEN false ELSE true END AS is_active
  FROM staged
  WHERE staged.name IS NOT NULL
    AND staged.name <> ''
),
upserted AS (
  INSERT INTO core.business_partners (
    company_id,
    partner_type,
    name,
    contact_name,
    email,
    phone,
    tax_identifier,
    legacy_partner_id,
    is_active
  )
  SELECT
    $CompanyId,
    $partnerTypeLiteral,
    name,
    contact_name,
    email,
    phone,
    tax_identifier,
    legacy_partner_id,
    is_active
  FROM normalized
  ON CONFLICT (company_id, partner_type, name) DO UPDATE
  SET
    contact_name = EXCLUDED.contact_name,
    email = EXCLUDED.email,
    phone = EXCLUDED.phone,
    tax_identifier = EXCLUDED.tax_identifier,
    legacy_partner_id = EXCLUDED.legacy_partner_id,
    is_active = EXCLUDED.is_active
  RETURNING id, name, legacy_partner_id
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
    COALESCE(upserted.legacy_partner_id, upserted.name),
    'core.business_partners',
    upserted.id,
    staging.raw_hash
  FROM upserted
  JOIN core.migration_staging_records staging
    ON staging.migration_batch_id = $MigrationBatchId
   AND staging.source_table = $sourceTableLiteral
   AND staging.raw_data ->> $legacyIdFieldLiteral = upserted.legacy_partner_id
  ON CONFLICT (migration_batch_id, source_table, source_key, target_table) DO UPDATE
  SET target_id = EXCLUDED.target_id,
      raw_hash = EXCLUDED.raw_hash
  RETURNING id
)
SELECT
  (SELECT count(*) FROM upserted) AS partners_upserted,
  (SELECT count(*) FROM refs) AS source_refs_upserted;
"@

Invoke-Psql -Sql $applySql
