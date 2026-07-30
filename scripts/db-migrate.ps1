[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'common\PostgresRuntime.ps1')

$runtime = Resolve-IumpPostgresCliRuntime -RepoRoot $repoRoot -Purpose Migration
if (-not $runtime.Available) {
    Write-Output "database-migration: $($runtime.Classification) [BLK-R0-002] - $($runtime.Evidence)"
    exit $(if ($runtime.Classification -eq 'BLOCKED_BY_MISSING_TOOL') { 20 } else { 1 })
}

$migrations = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'database\migrations') -Filter '*.sql' | Sort-Object Name
foreach ($migration in $migrations) {
    Write-Output "Applying migration $($migration.Name) to approved PostgreSQL target (credential redacted)."
    Invoke-IumpPsql -Runtime $runtime -Arguments @(
        '--set', 'ON_ERROR_STOP=1', '--file', $migration.FullName)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { exit $exitCode }
}
exit 0
