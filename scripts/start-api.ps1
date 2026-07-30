[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Output 'api-start: BLOCKED_BY_MISSING_TOOL - dotnet is missing'
    exit 20
}
if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
}
& dotnet run --project (Join-Path $repoRoot 'src\Api\IUMP.Api.csproj') --no-restore
exit $LASTEXITCODE
