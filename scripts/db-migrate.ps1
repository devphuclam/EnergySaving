[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    Write-Output 'database-migration: BLOCKED_BY_MISSING_TOOL [BLK-R0-002] - psql is missing'
    exit 20
}
if ([string]::IsNullOrWhiteSpace($env:PGSERVICE)) {
    Write-Output 'database-migration: BLOCKED_BY_DATABASE_ACCESS [BLK-R0-002] - PGSERVICE is not set'
    exit 20
}

$migrations = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'database\migrations') -Filter '*.sql' | Sort-Object Name
foreach ($migration in $migrations) {
    Write-Output "Applying migration $($migration.Name) using approved PostgreSQL service (credential redacted)."
    & psql "service=$env:PGSERVICE" --no-psqlrc --set ON_ERROR_STOP=1 --file $migration.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
exit 0
