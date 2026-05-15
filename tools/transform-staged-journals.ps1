<#
.SYNOPSIS
Transforms staged legacy journal header/line rows into core.journals.

.DESCRIPTION
Reads staged journal headers and account lines, maps legacy accounts through
core.accounts.legacy_account_id, converts signed amounts into explicit debit and
credit using an explicit caller-supplied polarity, and inserts only balanced
journals when -Apply is supplied.

The default mode is preview-only. This script is intentionally conservative
because Sage signed amount semantics must be validated against accepted reports.
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [long]$MigrationBatchId,

  [Parameter(Mandatory = $true)]
  [long]$CompanyId,

  [Parameter(Mandatory = $true)]
  [ValidateSet("Debit", "Credit")]
  [string]$PositiveAmountSide,

  [string]$HeaderSourceTable = "tJourEnt",
  [string]$LineSourceTable = "tJEntAct",
  [string]$HeaderIdField = "lId",
  [string]$JournalDateField = "dtJourDate",
  [string]$JournalNumberField = "",
  [string]$SourceReferenceField = "sSource",
  [string]$MemoField = "sComment",
  [string]$ModuleField = "nModule",
  [string]$LineIdField = "lId",
  [string]$LineJournalIdField = "lJEntId",
  [string]$LineAccountIdField = "lAcctId",
  [string]$LineAmountField = "dAmount",
  [string]$LineDescriptionField = "sComment",
  [string]$DefaultCurrency = "PHP",
  [string]$JournalNumberPrefix = "LEGACY-JE-",

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

$positiveSide = $PositiveAmountSide.ToLowerInvariant()
$headerSourceTableLiteral = ConvertTo-SqlLiteral $HeaderSourceTable
$lineSourceTableLiteral = ConvertTo-SqlLiteral $LineSourceTable
$headerIdFieldLiteral = ConvertTo-SqlLiteral $HeaderIdField
$journalDateFieldLiteral = ConvertTo-SqlLiteral $JournalDateField
$sourceReferenceFieldLiteral = ConvertTo-SqlLiteral $SourceReferenceField
$memoFieldLiteral = ConvertTo-SqlLiteral $MemoField
$moduleFieldLiteral = ConvertTo-SqlLiteral $ModuleField
$lineIdFieldLiteral = ConvertTo-SqlLiteral $LineIdField
$lineJournalIdFieldLiteral = ConvertTo-SqlLiteral $LineJournalIdField
$lineAccountIdFieldLiteral = ConvertTo-SqlLiteral $LineAccountIdField
$lineAmountFieldLiteral = ConvertTo-SqlLiteral $LineAmountField
$lineDescriptionFieldLiteral = ConvertTo-SqlLiteral $LineDescriptionField
$defaultCurrencyLiteral = ConvertTo-SqlLiteral $DefaultCurrency
$journalNumberPrefixLiteral = ConvertTo-SqlLiteral $JournalNumberPrefix

if ($JournalNumberField -eq "") {
  $journalNumberExpression = "$journalNumberPrefixLiteral || (raw_data ->> $headerIdFieldLiteral)"
}
else {
  $journalNumberFieldLiteral = ConvertTo-SqlLiteral $JournalNumberField
  $journalNumberExpression = "COALESCE(NULLIF(raw_data ->> $journalNumberFieldLiteral, ''), $journalNumberPrefixLiteral || (raw_data ->> $headerIdFieldLiteral))"
}

$baseCte = @"
WITH headers AS (
  SELECT
    id AS staging_header_id,
    source_key,
    raw_hash,
    raw_data ->> $headerIdFieldLiteral AS legacy_journal_id,
    ($journalNumberExpression) AS journal_number,
    CAST(raw_data ->> $journalDateFieldLiteral AS date) AS journal_date,
    COALESCE(NULLIF(raw_data ->> $sourceReferenceFieldLiteral, ''), raw_data ->> $headerIdFieldLiteral) AS source_reference,
    NULLIF(raw_data ->> $memoFieldLiteral, '') AS memo,
    COALESCE(NULLIF(raw_data ->> $moduleFieldLiteral, ''), 'legacy') AS legacy_module
  FROM core.migration_staging_records
  WHERE migration_batch_id = $MigrationBatchId
    AND source_table = $headerSourceTableLiteral
),
raw_lines AS (
  SELECT
    id AS staging_line_id,
    raw_hash,
    raw_data ->> $lineIdFieldLiteral AS legacy_line_id,
    raw_data ->> $lineJournalIdFieldLiteral AS legacy_journal_id,
    raw_data ->> $lineAccountIdFieldLiteral AS legacy_account_id,
    CAST(raw_data ->> $lineAmountFieldLiteral AS numeric(19,4)) AS legacy_signed_amount,
    NULLIF(raw_data ->> $lineDescriptionFieldLiteral, '') AS description
  FROM core.migration_staging_records
  WHERE migration_batch_id = $MigrationBatchId
    AND source_table = $lineSourceTableLiteral
),
mapped_lines AS (
  SELECT
    raw_lines.*,
    accounts.id AS account_id,
    CASE
      WHEN raw_lines.legacy_signed_amount >= 0 AND '$positiveSide' = 'debit' THEN raw_lines.legacy_signed_amount
      WHEN raw_lines.legacy_signed_amount < 0 AND '$positiveSide' = 'credit' THEN abs(raw_lines.legacy_signed_amount)
      ELSE 0
    END AS debit,
    CASE
      WHEN raw_lines.legacy_signed_amount >= 0 AND '$positiveSide' = 'credit' THEN raw_lines.legacy_signed_amount
      WHEN raw_lines.legacy_signed_amount < 0 AND '$positiveSide' = 'debit' THEN abs(raw_lines.legacy_signed_amount)
      ELSE 0
    END AS credit
  FROM raw_lines
  LEFT JOIN core.accounts accounts
    ON accounts.company_id = $CompanyId
   AND accounts.legacy_account_id = raw_lines.legacy_account_id
),
numbered_lines AS (
  SELECT
    mapped_lines.*,
    row_number() OVER (PARTITION BY legacy_journal_id ORDER BY staging_line_id) AS line_number
  FROM mapped_lines
),
journal_totals AS (
  SELECT
    headers.legacy_journal_id,
    count(numbered_lines.staging_line_id) AS line_count,
    count(numbered_lines.staging_line_id) FILTER (WHERE numbered_lines.account_id IS NULL) AS unmapped_account_count,
    COALESCE(sum(numbered_lines.debit), 0) AS total_debit,
    COALESCE(sum(numbered_lines.credit), 0) AS total_credit
  FROM headers
  LEFT JOIN numbered_lines ON numbered_lines.legacy_journal_id = headers.legacy_journal_id
  GROUP BY headers.legacy_journal_id
)
"@

$previewSql = $baseCte + @"
SELECT
  headers.legacy_journal_id,
  headers.journal_number,
  headers.journal_date,
  journal_totals.line_count,
  journal_totals.unmapped_account_count,
  journal_totals.total_debit,
  journal_totals.total_credit,
  CASE
    WHEN headers.legacy_journal_id IS NULL OR headers.legacy_journal_id = '' THEN 'missing journal id'
    WHEN headers.journal_date IS NULL THEN 'missing journal date'
    WHEN journal_totals.line_count < 2 THEN 'fewer than two lines'
    WHEN journal_totals.unmapped_account_count > 0 THEN 'unmapped account'
    WHEN journal_totals.total_debit <> journal_totals.total_credit THEN 'unbalanced'
    ELSE 'ok'
  END AS validation_status
FROM headers
JOIN journal_totals ON journal_totals.legacy_journal_id = headers.legacy_journal_id
ORDER BY headers.staging_header_id;
"@

if (-not $Apply) {
  Invoke-Psql -Sql $previewSql
  Write-Output "Preview only. Re-run with -Apply to insert posted journals."
  return
}

$applySql = @"
BEGIN;

CREATE TEMP TABLE tmp_valid_headers ON COMMIT DROP AS
$baseCte
SELECT headers.*
FROM headers
JOIN journal_totals ON journal_totals.legacy_journal_id = headers.legacy_journal_id
WHERE headers.legacy_journal_id IS NOT NULL
  AND headers.legacy_journal_id <> ''
  AND headers.journal_date IS NOT NULL
  AND journal_totals.line_count >= 2
  AND journal_totals.unmapped_account_count = 0
  AND journal_totals.total_debit = journal_totals.total_credit;

CREATE TEMP TABLE tmp_numbered_lines ON COMMIT DROP AS
$baseCte
SELECT numbered_lines.*
FROM numbered_lines
JOIN tmp_valid_headers ON tmp_valid_headers.legacy_journal_id = numbered_lines.legacy_journal_id;

CREATE TEMP TABLE tmp_target_journals ON COMMIT DROP AS
WITH inserted AS (
  INSERT INTO core.journals (
    company_id,
    journal_number,
    journal_date,
    status,
    source_module,
    source_reference,
    memo,
    currency,
    created_by_user_id,
    legacy_journal_id,
    legacy_source_table
  )
  SELECT
    $CompanyId,
    tmp_valid_headers.journal_number,
    tmp_valid_headers.journal_date,
    'draft',
    'legacy_import',
    tmp_valid_headers.source_reference,
    tmp_valid_headers.memo,
    $defaultCurrencyLiteral,
    users.id,
    tmp_valid_headers.legacy_journal_id,
    $headerSourceTableLiteral
  FROM tmp_valid_headers
  CROSS JOIN LATERAL (
    SELECT id FROM core.users WHERE external_identity_id = 'api-dev' LIMIT 1
  ) users
  ON CONFLICT (company_id, journal_number) DO UPDATE
  SET
    source_reference = EXCLUDED.source_reference,
    memo = EXCLUDED.memo,
    legacy_journal_id = EXCLUDED.legacy_journal_id,
    legacy_source_table = EXCLUDED.legacy_source_table
  WHERE core.journals.status = 'draft'
  RETURNING id, legacy_journal_id
)
SELECT * FROM inserted;

DELETE FROM core.journal_lines
USING tmp_target_journals
WHERE core.journal_lines.journal_id = tmp_target_journals.id;

CREATE TEMP TABLE tmp_inserted_lines ON COMMIT DROP AS
WITH inserted AS (
  INSERT INTO core.journal_lines (
    journal_id,
    line_number,
    account_id,
    debit,
    credit,
    description,
    legacy_line_id,
    legacy_signed_amount
  )
  SELECT
    tmp_target_journals.id,
    tmp_numbered_lines.line_number,
    tmp_numbered_lines.account_id,
    tmp_numbered_lines.debit,
    tmp_numbered_lines.credit,
    tmp_numbered_lines.description,
    tmp_numbered_lines.legacy_line_id,
    tmp_numbered_lines.legacy_signed_amount
  FROM tmp_target_journals
  JOIN tmp_numbered_lines ON tmp_numbered_lines.legacy_journal_id = tmp_target_journals.legacy_journal_id
  RETURNING id, journal_id, legacy_line_id
)
SELECT * FROM inserted;

CREATE TEMP TABLE tmp_posted_journals ON COMMIT DROP AS
WITH posted AS (
  UPDATE core.journals
  SET
    status = 'posted',
    posted_at = now(),
    posted_by_user_id = users.id
  FROM tmp_target_journals
  CROSS JOIN LATERAL (
    SELECT id FROM core.users WHERE external_identity_id = 'api-dev' LIMIT 1
  ) users
  WHERE core.journals.id = tmp_target_journals.id
    AND core.journals.status = 'draft'
  RETURNING core.journals.id, core.journals.legacy_journal_id
)
SELECT * FROM posted;

CREATE TEMP TABLE tmp_journal_refs ON COMMIT DROP AS
WITH refs AS (
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
    $headerSourceTableLiteral,
    tmp_posted_journals.legacy_journal_id,
    'core.journals',
    tmp_posted_journals.id,
    tmp_valid_headers.raw_hash
  FROM tmp_posted_journals
  JOIN tmp_valid_headers ON tmp_valid_headers.legacy_journal_id = tmp_posted_journals.legacy_journal_id
  ON CONFLICT (migration_batch_id, source_table, source_key, target_table) DO UPDATE
  SET target_id = EXCLUDED.target_id,
      raw_hash = EXCLUDED.raw_hash
  RETURNING id
)
SELECT * FROM refs;

CREATE TEMP TABLE tmp_line_refs ON COMMIT DROP AS
WITH refs AS (
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
    $lineSourceTableLiteral,
    tmp_numbered_lines.legacy_line_id,
    'core.journal_lines',
    tmp_inserted_lines.id,
    tmp_numbered_lines.raw_hash
  FROM tmp_inserted_lines
  JOIN tmp_numbered_lines ON tmp_numbered_lines.legacy_line_id = tmp_inserted_lines.legacy_line_id
  WHERE tmp_numbered_lines.legacy_line_id IS NOT NULL
    AND tmp_numbered_lines.legacy_line_id <> ''
  ON CONFLICT (migration_batch_id, source_table, source_key, target_table) DO UPDATE
  SET target_id = EXCLUDED.target_id,
      raw_hash = EXCLUDED.raw_hash
  RETURNING id
)
SELECT * FROM refs;

SELECT
  (SELECT count(*) FROM tmp_posted_journals) AS journals_posted,
  (SELECT count(*) FROM tmp_inserted_lines) AS journal_lines_inserted,
  (SELECT count(*) FROM tmp_journal_refs) AS journal_refs_upserted,
  (SELECT count(*) FROM tmp_line_refs) AS line_refs_upserted;

COMMIT;
"@

Invoke-Psql -Sql $applySql
