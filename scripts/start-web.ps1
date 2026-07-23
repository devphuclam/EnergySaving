[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$webRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\src\Web')).Path
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    Write-Output 'web-start: BLOCKED_BY_MISSING_TOOL - npm is missing'
    exit 20
}
if (-not (Test-Path -LiteralPath (Join-Path $webRoot 'node_modules'))) {
    Write-Output 'web-start: BLOCKED_BY_PACKAGE_POLICY [BLK-R0-001] - installed dependency tree is missing'
    exit 20
}
Push-Location $webRoot
try {
    & npm run dev
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
