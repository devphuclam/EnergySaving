[CmdletBinding()]
param([string[]]$ScanRoots)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$scopeRoots = if ($null -eq $ScanRoots -or $ScanRoots.Count -eq 0) {
    @('src', 'database', 'scripts')
}
else {
    @($ScanRoots)
}
$patterns = @(
    '(?i)\bModbus\b',
    '(?i)\bWriteBack|write[- ]back',
    '(?i)\bSetpoint\b|\bActuat(?:e|or|ion)\b',
    '(?i)\bEquipmentCommand\b|\bControlCommand\b',
    '(?i)\bAI/ML\b'
)

foreach ($root in $scopeRoots) {
    $path = if ([IO.Path]::IsPathRooted($root)) {
        [IO.Path]::GetFullPath($root)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repoRoot $root))
    }
    if (-not (Test-Path -LiteralPath $path)) { continue }
    Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](node_modules|bin|obj)[\\/]' -and
        $_.Extension -in @('.cs', '.tsx', '.ts', '.json', '.sql', '.ps1')
    } | ForEach-Object {
        $content = Get-Content -LiteralPath $_.FullName -Raw
        foreach ($pattern in $patterns) {
            if ($content -match $pattern) {
                throw "Permanent product invariant violated by '$pattern' in $($_.FullName)"
            }
        }
    }
}

Write-Output 'PASS: permanent repository scope invariants'
