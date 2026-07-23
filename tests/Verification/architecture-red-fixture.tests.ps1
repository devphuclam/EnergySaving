[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$architectureTest = Join-Path $PSScriptRoot 'architecture.tests.ps1'
$fixtureRoot = Join-Path $repoRoot 'tests\Architecture\fixtures'

function Assert-ArchitectureFailure {
    param([scriptblock]$Action, [string]$ExpectedPattern, [string]$Scenario)
    $failedForExpectedReason = $false
    try { & $Action }
    catch { $failedForExpectedReason = $_.Exception.Message -like $ExpectedPattern }
    if (-not $failedForExpectedReason) {
        throw "Forbidden architecture fixture did not fail for the expected $Scenario reason."
    }
}

Assert-ArchitectureFailure { & $architectureTest -ModuleRoot $fixtureRoot } `
    'Module-to-module project reference is forbidden*' 'module-reference'
Assert-ArchitectureFailure { & $architectureTest -BuildingBlocksProject (Join-Path $fixtureRoot 'Foundation\Foundation.csproj') } `
    'BuildingBlocks must remain framework-light and dependency-free*' 'foundation-reference'
Assert-ArchitectureFailure { & $architectureTest -HostSourceRoot (Join-Path $fixtureRoot 'Host') } `
    'Host references module internals*' 'host-internal-reference'
Assert-ArchitectureFailure { & $architectureTest -ContractSourceRoot (Join-Path $fixtureRoot 'Contracts') } `
    'Prohibited command/write-back contract surface*' 'command-contract'

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$positiveRoot = [IO.Path]::GetFullPath((Join-Path $tempRoot "iump-architecture-$([guid]::NewGuid())"))
if (-not $positiveRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe fixture path: $positiveRoot"
}
try {
    $domainRoot = Join-Path $positiveRoot 'AllowedModule\Domain'
    New-Item -ItemType Directory -Path $domainRoot -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $domainRoot 'AllowedEntity.cs') `
        -Value 'namespace AllowedModule.Domain; public sealed class AllowedEntity { }' -Encoding UTF8
    & $architectureTest -ModuleRoot $positiveRoot
}
catch {
    throw "Valid internal module implementation was rejected: $($_.Exception.Message)"
}
finally {
    if ((Test-Path -LiteralPath $positiveRoot) -and
        $positiveRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $positiveRoot -Recurse -Force
    }
}

Write-Output 'PASS: all forbidden architecture fixtures are red-capable'
