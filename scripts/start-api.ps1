[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Output 'api-start: BLOCKED_BY_MISSING_TOOL - dotnet is missing'
    exit 20
}
if ([string]::IsNullOrWhiteSpace($env:IUMP_CONNECTION_STRING)) {
    Write-Output 'api-start: BLOCKED_BY_DATABASE_ACCESS [BLK-R0-002] - IUMP_CONNECTION_STRING is not set'
    exit 20
}
& dotnet run --project (Join-Path $repoRoot 'src\Api\IUMP.Api.csproj') --no-restore
exit $LASTEXITCODE
