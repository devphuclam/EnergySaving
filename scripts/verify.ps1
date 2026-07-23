[CmdletBinding()]
param([string]$Feature)

$arguments = @('-Mode', 'Full')
if (-not [string]::IsNullOrWhiteSpace($Feature)) {
    $arguments += @('-Feature', $Feature)
}

& (Join-Path $PSScriptRoot 'harness.ps1') @arguments
exit $LASTEXITCODE
