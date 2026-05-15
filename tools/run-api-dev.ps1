<#
.SYNOPSIS
Runs the local development API.
#>

[CmdletBinding()]
param(
  [string]$Url = "http://localhost:5088"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$env:DOTNET_CLI_HOME = $repoRoot.Path

Set-Location $repoRoot
dotnet run --project .\src\Accounting.Api\Accounting.Api.csproj --urls $Url
