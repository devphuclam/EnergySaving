[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    Write-Output 'database-seed: BLOCKED_BY_MISSING_TOOL [BLK-R0-002] - psql is missing'
    exit 20
}
if ([string]::IsNullOrWhiteSpace($env:PGSERVICE)) {
    Write-Output 'database-seed: BLOCKED_BY_DATABASE_ACCESS [BLK-R0-002] - PGSERVICE is not set'
    exit 20
}

$seeds = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'database\seeds') -Filter '*.sql' | Sort-Object Name
if ($seeds.Count -eq 0) {
    Write-Output 'database-seed: NOT_RUN - R0 defines no business seed data'
    exit 0
}
foreach ($seed in $seeds) {
    Write-Output "Applying seed $($seed.Name) using approved PostgreSQL service (credential redacted)."
    & psql "service=$env:PGSERVICE" --no-psqlrc --set ON_ERROR_STOP=1 --file $seed.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
exit 0
