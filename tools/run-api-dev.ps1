<#
.SYNOPSIS
Runs the local development API.
#>

[CmdletBinding()]
param(
  [string]$Url = "http://localhost:5088",
  [string]$DatabaseName = "accounting_dev",
  [string]$PostgresUser = "postgres",
  [string]$PostgresPassword = "",
  [string]$ConnectionString = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$env:DOTNET_CLI_HOME = $repoRoot.Path

if ($ConnectionString) {
  $env:ACCOUNTING_DB = $ConnectionString
}
elseif ($PostgresPassword) {
  $env:ACCOUNTING_DB = "Host=localhost;Port=5432;Database=$DatabaseName;Username=$PostgresUser;Password=$PostgresPassword"
}
elseif (-not $env:ACCOUNTING_DB) {
  throw "Set ACCOUNTING_DB, pass -ConnectionString, or pass -PostgresPassword."
}

Set-Location $repoRoot
dotnet run --project .\src\Accounting.Api\Accounting.Api.csproj --urls $Url
