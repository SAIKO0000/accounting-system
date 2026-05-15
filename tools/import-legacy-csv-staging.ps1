<#
.SYNOPSIS
Imports an authorized legacy CSV extract into PostgreSQL migration staging.

.DESCRIPTION
Loads a CSV export into core.migration_staging_records as raw JSONB rows. This
script does not transform accounting data into live tables; it preserves source
table name, source key, row number, raw JSON, and a SHA-256 hash so later
migration mapping can be repeated and audited.
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$CsvPath,

  [Parameter(Mandatory = $true)]
  [string]$SourceName,

  [Parameter(Mandatory = $true)]
  [string]$SourceTable,

  [string]$SourceKeyColumn = "",

  [long]$MigrationBatchId = 0,

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

if (-not (Test-Path -LiteralPath $CsvPath)) {
  throw "CsvPath not found: $CsvPath"
}

if (-not (Test-Path -LiteralPath $PsqlPath)) {
  throw "psql not found: $PsqlPath"
}

$csvFullPath = (Resolve-Path -LiteralPath $CsvPath).Path
$env:PATH = "$(Split-Path -Parent $PsqlPath);$PgRuntimePath;$env:PATH"
$env:PGPASSWORD = $Password

function Invoke-PsqlScalar {
  param([Parameter(Mandatory = $true)][string]$Sql)

  $result = & $PsqlPath `
    -h $HostName `
    -p $Port `
    -U $UserName `
    -d $DatabaseName `
    -v ON_ERROR_STOP=1 `
    -At `
    -c $Sql

  if ($LASTEXITCODE -ne 0) {
    throw "psql command failed"
  }

  return ($result | Select-Object -First 1).Trim()
}

function ConvertTo-SqlLiteral {
  param([Parameter(Mandatory = $true)][string]$Value)
  return "'" + $Value.Replace("'", "''") + "'"
}

function Get-Sha256Hex {
  param([Parameter(Mandatory = $true)][string]$Value)

  $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
  $sha256 = [System.Security.Cryptography.SHA256]::Create()
  try {
    $hashBytes = $sha256.ComputeHash($bytes)
    return (($hashBytes | ForEach-Object { $_.ToString("x2") }) -join "")
  }
  finally {
    $sha256.Dispose()
  }
}

if ($MigrationBatchId -le 0) {
  $batchSql = @"
INSERT INTO core.migration_batches (source_name, source_path, notes)
VALUES ($(ConvertTo-SqlLiteral $SourceName), $(ConvertTo-SqlLiteral $csvFullPath), 'CSV staging import')
RETURNING id;
"@
  $MigrationBatchId = [long](Invoke-PsqlScalar -Sql $batchSql)
}

$rows = Import-Csv -LiteralPath $csvFullPath
$tempFile = [System.IO.Path]::GetTempFileName()
$tempCsvPath = [System.IO.Path]::ChangeExtension($tempFile, ".csv")
Move-Item -LiteralPath $tempFile -Destination $tempCsvPath -Force

try {
  $stagingRows = New-Object System.Collections.Generic.List[object]
  $rowNumber = 0

  foreach ($row in $rows) {
    $rowNumber++
    $rawJson = $row | ConvertTo-Json -Compress -Depth 20

    $sourceKey = ""
    if ($SourceKeyColumn -ne "" -and $row.PSObject.Properties.Name -contains $SourceKeyColumn) {
      $sourceKey = [string]$row.$SourceKeyColumn
    }

    if ([string]::IsNullOrWhiteSpace($sourceKey)) {
      $sourceKey = [string]$rowNumber
    }

    $stagingRows.Add([PSCustomObject]@{
      migration_batch_id = $MigrationBatchId
      source_table = $SourceTable
      source_key = $sourceKey
      source_row_number = $rowNumber
      raw_hash = Get-Sha256Hex -Value $rawJson
      raw_data = $rawJson
    })
  }

  $stagingRows | Export-Csv -LiteralPath $tempCsvPath -NoTypeInformation
  $copyPath = $tempCsvPath.Replace("\", "/").Replace("'", "''")

  & $PsqlPath `
    -h $HostName `
    -p $Port `
    -U $UserName `
    -d $DatabaseName `
    -v ON_ERROR_STOP=1 `
    -c "\copy core.migration_staging_records (migration_batch_id, source_table, source_key, source_row_number, raw_hash, raw_data) FROM '$copyPath' WITH (FORMAT csv, HEADER true)"

  if ($LASTEXITCODE -ne 0) {
    Invoke-PsqlScalar -Sql "UPDATE core.migration_batches SET status = 'failed', completed_at = now() WHERE id = $MigrationBatchId RETURNING id;" | Out-Null
    throw "failed to import CSV staging rows"
  }

  Invoke-PsqlScalar -Sql "UPDATE core.migration_batches SET status = 'completed', completed_at = now() WHERE id = $MigrationBatchId RETURNING id;" | Out-Null

  [PSCustomObject]@{
    MigrationBatchId = $MigrationBatchId
    SourceTable = $SourceTable
    ImportedRows = $rowNumber
    Status = "completed"
  }
}
finally {
  if (Test-Path -LiteralPath $tempCsvPath) {
    Remove-Item -LiteralPath $tempCsvPath -Force
  }
}
