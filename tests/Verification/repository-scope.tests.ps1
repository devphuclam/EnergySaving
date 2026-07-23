[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$scopeRoots = @('src', 'database', 'scripts')
$patterns = @(
    '(?i)Modbus',
    '(?i)write[- ]?back',
    '(?i)docker(?:file|[- ]compose|\s+build)',
    '(?i)Testcontainers',
    '(?i)\bAI/ML\b'
)

foreach ($root in $scopeRoots) {
    $path = Join-Path $repoRoot $root
    if (-not (Test-Path -LiteralPath $path)) { continue }
    Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](node_modules|bin|obj)[\\/]' -and
        $_.Extension -in @('.cs', '.tsx', '.ts', '.json', '.sql', '.ps1')
    } | ForEach-Object {
        $content = Get-Content -LiteralPath $_.FullName -Raw
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) {
                throw "Out-of-scope surface '$pattern' found in $($_.FullName)"
            }
        }
    }
}

Write-Output 'PASS: repository scope contract'
