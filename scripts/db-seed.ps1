[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'common\PostgresRuntime.ps1')

$runtime = Resolve-IumpPostgresCliRuntime -RepoRoot $repoRoot -Purpose Migration
if (-not $runtime.Available) {
    Write-Output "database-seed: $($runtime.Classification) [BLK-R0-002] - $($runtime.Evidence)"
    exit $(if ($runtime.Classification -eq 'BLOCKED_BY_MISSING_TOOL') { 20 } else { 1 })
}

$seeds = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'database\seeds') -Filter '*.sql' | Sort-Object Name
if ($seeds.Count -eq 0) {
    Write-Output 'database-seed: NOT_RUN - R0 defines no business seed data'
    exit 0
}
foreach ($seed in $seeds) {
    Write-Output "Applying seed $($seed.Name) to approved PostgreSQL target (credential redacted)."
    Invoke-IumpPsql -Runtime $runtime -Arguments @(
        '--set', 'ON_ERROR_STOP=1', '--file', $seed.FullName)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) { exit $exitCode }
}
exit 0
