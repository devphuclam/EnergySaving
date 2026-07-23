[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$scopeTest = Join-Path $PSScriptRoot 'repository-scope.tests.ps1'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture = [IO.Path]::GetFullPath((Join-Path $tempRoot "iump-scope-$([guid]::NewGuid())"))
if (-not $fixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe fixture path: $fixture"
}

try {
    $sourceDirectory = Join-Path $fixture 'src\Forbidden'
    New-Item -ItemType Directory -Path $sourceDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $sourceDirectory 'WriteBackCommand.cs') `
        -Value 'public sealed class WriteBackCommand { }' -Encoding UTF8

    $failedForExpectedReason = $false
    try {
        & $scopeTest -ScanRoots @($fixture)
    }
    catch {
        $failedForExpectedReason = $_.Exception.Message -like 'Permanent product invariant violated*'
    }

    if (-not $failedForExpectedReason) {
        throw 'Forbidden scope fixture did not fail for the expected permanent-invariant reason.'
    }
}
finally {
    if ((Test-Path -LiteralPath $fixture) -and
        $fixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}

Write-Output 'PASS: permanent scope invariant fixture is red-capable'
