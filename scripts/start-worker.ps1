[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Output 'worker-start: BLOCKED_BY_MISSING_TOOL - dotnet is missing'
    exit 20
}
& dotnet run --project (Join-Path $repoRoot 'src\Worker\IUMP.Worker.csproj') --no-restore
exit $LASTEXITCODE
