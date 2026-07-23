[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tests = @(
    'tests\Verification\verification-contract.tests.ps1',
    'tests\Verification\repository-harness.tests.ps1',
    'tests\Verification\repository-policy.tests.ps1',
    'tests\Verification\repository-scope.tests.ps1',
    'tests\Verification\repository-scope-red-fixture.tests.ps1',
    'tests\Verification\architecture.tests.ps1',
    'tests\Verification\architecture-red-fixture.tests.ps1'
)

$failed = $false
foreach ($relativePath in $tests) {
    $path = Join-Path $repoRoot $relativePath
    try {
        & $path
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { $failed = $true }
    }
    catch {
        Write-Output "FAIL: $relativePath - $($_.Exception.Message)"
        $failed = $true
    }
}

if (-not $failed) {
    & dotnet run --project (Join-Path $repoRoot 'tests\Unit\IUMP.Tests.Unit.csproj') --no-restore
    if ($LASTEXITCODE -ne 0) { $failed = $true }
}

if ($failed) { exit 1 }
exit 0
