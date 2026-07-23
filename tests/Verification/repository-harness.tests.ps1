[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$verificationPath = Join-Path $repoRoot 'scripts\common\Verification.ps1'
$harnessPath = Join-Path $repoRoot 'scripts\common\Harness.ps1'

. $verificationPath

if (-not (Test-Path -LiteralPath $harnessPath)) {
    throw "RED: harness interface is missing at $harnessPath"
}

. $harnessPath

$entry = Join-Path $repoRoot 'scripts\harness.ps1'
if (-not (Test-Path -LiteralPath $entry)) {
    throw "RED: public harness command is missing at $entry"
}

$entryText = Get-Content -LiteralPath $entry -Raw
foreach ($requiredText in @(
    '[ValidateSet(''Fast'', ''Full'')]',
    'verification-results.json',
    'Get-VerificationExitCode',
    'Resolve-HarnessFeature'
)) {
    if ($entryText -notmatch [regex]::Escape($requiredText)) {
        throw "Public harness command is missing '$requiredText'."
    }
}

$verifyText = Get-Content -LiteralPath (Join-Path $repoRoot 'scripts\verify.ps1') -Raw
if ($verifyText -notmatch 'harness\.ps1' -or $verifyText -notmatch 'Full') {
    throw 'verify.ps1 must delegate to Full harness mode.'
}

$knowledgeMap = Join-Path $repoRoot 'docs\repository-harness.md'
if (-not (Test-Path -LiteralPath $knowledgeMap)) {
    throw 'Repository harness knowledge map is missing.'
}
$knowledgeText = Get-Content -LiteralPath $knowledgeMap -Raw
foreach ($required in @(
    'Business Docs',
    'CONTEXT.md',
    'docs/source-register.md',
    'spec.md',
    'plan.md',
    'tasks.md',
    'scripts/harness.ps1 -Mode Fast',
    'scripts/harness.ps1 -Mode Full'
)) {
    if ($knowledgeText -notmatch [regex]::Escape($required)) {
        throw "Knowledge map is missing '$required'."
    }
}

$agentsText = Get-Content -LiteralPath (Join-Path $repoRoot 'AGENTS.md') -Raw
if ($agentsText -notmatch 'docs/repository-harness\.md') {
    throw 'AGENTS.md does not point to the repository harness.'
}
if ($agentsText -notmatch 'Full') {
    throw 'AGENTS.md does not require Full verification before completion.'
}

function Assert-Equal {
    param(
        [AllowNull()]$Actual,
        [AllowNull()]$Expected,
        [Parameter(Mandatory)][string]$Scenario
    )

    if ($Actual -ne $Expected) {
        throw "Expected '$Expected' for $Scenario, got '$Actual'."
    }
}

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture = [IO.Path]::GetFullPath((Join-Path $tempRoot "iump-harness-$([guid]::NewGuid())"))
if (-not $fixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe fixture path: $fixture"
}

try {
    $alpha = Join-Path $fixture 'specs\001-alpha'
    $beta = Join-Path $fixture 'specs\002-beta'
    $stateDirectory = Join-Path $fixture '.specify'
    New-Item -ItemType Directory -Path $alpha,$beta,$stateDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $alpha 'spec.md') -Value '# Alpha' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $beta 'spec.md') -Value '# Beta' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $beta 'plan.md') -Value '# Plan' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $beta 'tasks.md') -Value '# Tasks' -Encoding UTF8
    @{ feature_directory = 'specs/002-beta' } |
        ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $stateDirectory 'feature.json') -Encoding UTF8

    $explicit = Resolve-HarnessFeature -RepoRoot $fixture -Feature '002-beta'
    Assert-Equal $explicit.name '002-beta' 'explicit feature name'
    Assert-Equal $explicit.source 'argument' 'explicit feature source'

    $relative = Resolve-HarnessFeature -RepoRoot $fixture -Feature 'specs/001-alpha'
    Assert-Equal $relative.name '001-alpha' 'relative feature path'

    $fromState = Resolve-HarnessFeature -RepoRoot $fixture
    Assert-Equal $fromState.name '002-beta' 'state-file resolution'
    Assert-Equal $fromState.source 'state' 'state-file source'

    Remove-Item -LiteralPath (Join-Path $stateDirectory 'feature.json')
    $fromBranch = Resolve-HarnessFeature -RepoRoot $fixture -BranchName '001-alpha-work'
    Assert-Equal $fromBranch.name '001-alpha' 'branch resolution'
    Assert-Equal $fromBranch.source 'branch' 'branch feature source'

    $traversal = Resolve-HarnessFeature -RepoRoot $fixture -Feature '..\outside'
    Assert-Equal $traversal.resolved $false 'path traversal rejection'

    $missing = Resolve-HarnessFeature -RepoRoot $fixture -Feature '999-missing'
    Assert-Equal $missing.resolved $false 'missing feature rejection'

    $fullArtifacts = Test-FeatureArtifacts -FeatureResolution $explicit -Mode Full
    Assert-Equal $fullArtifacts.classification 'PASS' 'full artifact set'

    Remove-Item -LiteralPath (Join-Path $explicit.path 'tasks.md')
    $missingTasks = Test-FeatureArtifacts -FeatureResolution $explicit -Mode Full
    Assert-Equal $missingTasks.classification 'FAIL' 'full mode missing tasks'

    $fastArtifacts = Test-FeatureArtifacts -FeatureResolution $explicit -Mode Fast
    Assert-Equal $fastArtifacts.classification 'PASS' 'fast mode requires spec only'

    $unresolvedFast = Test-FeatureArtifacts -FeatureResolution $missing -Mode Fast
    Assert-Equal $unresolvedFast.classification 'NOT_RUN' 'fast mode without feature'
    Assert-Equal $unresolvedFast.mandatory $false 'fast unresolved feature is optional'

    $unresolvedFull = Test-FeatureArtifacts -FeatureResolution $missing -Mode Full
    Assert-Equal $unresolvedFull.classification 'FAIL' 'full mode without feature'
    Assert-Equal $unresolvedFull.mandatory $true 'full unresolved feature is mandatory'

    $fastPlan = @(Get-HarnessCheckPlan -Mode Fast)
    $fullPlan = @(Get-HarnessCheckPlan -Mode Full)
    foreach ($fullOnly in @('backend-build', 'frontend', 'database', 'ci', 'container-target')) {
        if ($fullOnly -in $fastPlan) {
            throw "Fast mode contains Full-only check '$fullOnly'."
        }
        if ($fullOnly -notin $fullPlan) {
            throw "Full mode is missing '$fullOnly'."
        }
    }
}
finally {
    if ((Test-Path -LiteralPath $fixture) -and
        $fixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}

Write-Output 'PASS: repository harness contract'
