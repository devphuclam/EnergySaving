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

Write-Output 'PASS: all forbidden architecture fixtures are red-capable'
