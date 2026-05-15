<#
.SYNOPSIS
Read-only inventory helper for authorized Sage Simply Accounting .SDB files.

.DESCRIPTION
Uses Microsoft ACE OLE DB to connect to a Sage/Jet company database with its
paired workgroup .SDW file, then prints table names and row counts. This script
does not modify the source database.

.EXAMPLE
.\tools\inspect-sage-company.ps1 `
  -DatabasePath "C:\project1\winsim\Generic Company.SDB" `
  -WorkgroupPath "C:\project1\winsim\Generic Company.SDW" `
  -UserId "sysadmin"
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$DatabasePath,

  [Parameter(Mandatory = $true)]
  [string]$WorkgroupPath,

  [Parameter(Mandatory = $true)]
  [string]$UserId,

  [Parameter(Mandatory = $false)]
  [string]$Password = "",

  [Parameter(Mandatory = $false)]
  [string[]]$Tables = @(
    "tCompany",
    "tAccount",
    "tJourEnt",
    "tJEntAct",
    "tJEntTax",
    "tJEntPrj",
    "tCustomr",
    "tCusTr",
    "tCusTrDt",
    "tVendor",
    "tVenTr",
    "tVenTrDt",
    "tBRInfo",
    "tBRSum",
    "tBRTr",
    "tUser",
    "tUserLog"
  )
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $DatabasePath)) {
  throw "DatabasePath not found: $DatabasePath"
}

if (-not (Test-Path -LiteralPath $WorkgroupPath)) {
  throw "WorkgroupPath not found: $WorkgroupPath"
}

$databaseFullPath = (Resolve-Path -LiteralPath $DatabasePath).Path
$workgroupFullPath = (Resolve-Path -LiteralPath $WorkgroupPath).Path

$connectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$databaseFullPath;Jet OLEDB:System Database=$workgroupFullPath;User ID=$UserId;Password=$Password;"
$connection = New-Object System.Data.OleDb.OleDbConnection($connectionString)

try {
  $connection.Open()

  Write-Output "Connected to: $databaseFullPath"
  Write-Output ""
  Write-Output "Available user tables:"

  $schema = $connection.GetOleDbSchemaTable(
    [System.Data.OleDb.OleDbSchemaGuid]::Tables,
    $null
  )

  $userTables = $schema |
    Where-Object { $_.TABLE_TYPE -eq "TABLE" -and $_.TABLE_NAME -notlike "MSys*" } |
    Sort-Object TABLE_NAME |
    Select-Object -ExpandProperty TABLE_NAME

  $userTables | ForEach-Object { Write-Output "  $_" }

  Write-Output ""
  Write-Output "Core table row counts:"

  foreach ($table in $Tables) {
    if ($userTables -notcontains $table) {
      [PSCustomObject]@{
        Table = $table
        Rows = $null
        Status = "missing"
      }
      continue
    }

    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT COUNT(*) FROM [$table]"
    $count = $command.ExecuteScalar()

    [PSCustomObject]@{
      Table = $table
      Rows = $count
      Status = "ok"
    }
  }
}
finally {
  if ($connection.State -ne [System.Data.ConnectionState]::Closed) {
    $connection.Close()
  }
}
