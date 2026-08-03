[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$deploymentScript = Join-Path $repoRoot 'scripts\common\DeploymentTarget.ps1'

if (-not (Test-Path -LiteralPath $deploymentScript -PathType Leaf)) {
    throw "RED: deployment-target verifier is missing at $deploymentScript"
}
. $deploymentScript

$failures = [System.Collections.Generic.List[string]]::new()
$checks = 0

function Assert-Equal {
    param([object]$Actual, [object]$Expected, [string]$Label)
    $script:checks++
    if ($Actual -ne $Expected) {
        $script:failures.Add("$Label`: expected '$Expected', got '$Actual'")
    }
}

function Assert-Contains {
    param([string]$Text, [string]$Expected, [string]$Label)
    $script:checks++
    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        $script:failures.Add("$Label`: expected output to contain '$Expected'")
    }
}

function Assert-NotContains {
    param([string]$Text, [string]$Unexpected, [string]$Label)
    $script:checks++
    if ($Text.IndexOf($Unexpected, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $script:failures.Add("$Label`: output must not contain '$Unexpected'")
    }
}

function Assert-Blank {
    param([AllowNull()][string]$Text, [string]$Label)
    $script:checks++
    if (-not [string]::IsNullOrWhiteSpace($Text)) {
        $script:failures.Add("$Label`: expected blank, got '$Text'")
    }
}

function New-ValidManifest {
    [ordered]@{
        deploymentModel = 'restricted-non-containerized'
        webHosting = 'approved static web host'
        apiHosting = 'approved ASP.NET Core process host'
        workerServiceManager = 'approved Windows service manager'
        databaseHosting = 'approved internal PostgreSQL service'
        lifecycleRunbookReference = 'RUNBOOK-LIFECYCLE-001'
        rollbackRunbookReference = 'RUNBOOK-ROLLBACK-001'
        approvalReference = 'APPROVAL-2026-001'
        approvedBy = 'Infrastructure and Security'
        approvedAtUtc = '2026-08-03T00:00:00Z'
    }
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('iump-deployment-target-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    $validPath = Join-Path $tempRoot 'valid.json'
    (New-ValidManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $validPath -Encoding UTF8

    $blocked = Test-DeploymentTargetApproval -ApprovalFlag '' -EvidencePath ''
    Assert-Equal $blocked.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing approval classification'
    Assert-Equal $blocked.blockerId 'BLK-ENV-005' 'missing approval blocker'
    Assert-Equal $blocked.exitCode 20 'missing approval exit'

    $falseFlag = Test-DeploymentTargetApproval -ApprovalFlag 'false' -EvidencePath $validPath
    Assert-Equal $falseFlag.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'false approval classification'
    Assert-Equal $falseFlag.blockerId 'BLK-ENV-005' 'false approval blocker'

    $missingEvidence = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath (Join-Path $tempRoot 'missing.json')
    Assert-Equal $missingEvidence.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing evidence classification'
    Assert-Equal $missingEvidence.blockerId 'BLK-ENV-005' 'missing evidence blocker'

    $malformedPath = Join-Path $tempRoot 'malformed.json'
    '{"deploymentModel":' | Set-Content -LiteralPath $malformedPath -Encoding UTF8
    $malformed = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $malformedPath
    Assert-Equal $malformed.classification 'FAIL' 'malformed JSON classification'
    Assert-Blank $malformed.blockerId 'malformed JSON blocker'
    Assert-Equal $malformed.exitCode 1 'malformed JSON exit'

    $missingFieldPath = Join-Path $tempRoot 'missing-field.json'
    $missingField = New-ValidManifest
    $missingField.Remove('rollbackRunbookReference')
    ($missingField | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $missingFieldPath -Encoding UTF8
    $missingFieldResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $missingFieldPath
    Assert-Equal $missingFieldResult.classification 'FAIL' 'missing field classification'

    $wrongModelPath = Join-Path $tempRoot 'wrong-model.json'
    $wrongModel = New-ValidManifest
    $wrongModel.deploymentModel = 'containerized'
    ($wrongModel | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $wrongModelPath -Encoding UTF8
    $wrongModelResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $wrongModelPath
    Assert-Equal $wrongModelResult.classification 'FAIL' 'wrong model classification'

    $secretPath = Join-Path $tempRoot 'secret-key.json'
    $secretManifest = New-ValidManifest
    $secretManifest.secret = 'must-not-be-echoed'
    ($secretManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $secretPath -Encoding UTF8
    $secretResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $secretPath
    Assert-Equal $secretResult.classification 'FAIL' 'secret key classification'
    Assert-NotContains ([string]$secretResult.evidence) 'must-not-be-echoed' 'secret value redaction'

    $nestedSecretPath = Join-Path $tempRoot 'nested-secret-key.json'
    $nestedSecretManifest = New-ValidManifest
    $nestedSecretManifest.metadata = [ordered]@{ token = 'nested-must-not-be-echoed' }
    ($nestedSecretManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $nestedSecretPath -Encoding UTF8
    $nestedSecretResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $nestedSecretPath
    Assert-Equal $nestedSecretResult.classification 'FAIL' 'nested secret key classification'
    Assert-NotContains ([string]$nestedSecretResult.evidence) 'nested-must-not-be-echoed' 'nested secret value redaction'

    $badDatePath = Join-Path $tempRoot 'bad-date.json'
    $badDate = New-ValidManifest
    $badDate.approvedAtUtc = 'not-a-date'
    ($badDate | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $badDatePath -Encoding UTF8
    $badDateResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $badDatePath
    Assert-Equal $badDateResult.classification 'FAIL' 'invalid date classification'

    $pass = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath
    Assert-Equal $pass.classification 'PASS' 'valid manifest classification'
    Assert-Blank $pass.blockerId 'valid manifest blocker'
    Assert-Equal $pass.exitCode 0 'valid manifest exit'
    Assert-NotContains ([string]$pass.evidence) 'approved static web host' 'manifest content not logged'
    Assert-NotContains ([string]$pass.evidence) 'APPROVAL-2026-001' 'approval reference not logged'

    . (Join-Path $repoRoot 'scripts\common\Harness.ps1')
    $fastPlan = @(Get-HarnessCheckPlan -Mode Fast)
    $fullPlan = @(Get-HarnessCheckPlan -Mode Full)
    Assert-Equal ($fastPlan -contains 'deployment-target') $false 'Fast excludes deployment-target'
    Assert-Equal ($fullPlan -contains 'deployment-target') $true 'Full includes deployment-target'

    . (Join-Path $repoRoot 'scripts\common\Verification.ps1')
    $allPass = @(New-VerificationResult -CheckId 'ci' -Classification 'PASS' -Command 'fixture' -Mandatory $true -Evidence 'pass') +
        @(New-VerificationResult -CheckId 'deployment-target' -Classification 'PASS' -Command 'fixture' -Mandatory $true -Evidence 'pass')
    Assert-Equal (Get-VerificationExitCode -Results $allPass) 0 'all PASS exit code'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

if ($failures.Count -gt 0) {
    Write-Host "DeploymentTarget: checks=$checks failures=$($failures.Count)"
    $failures | ForEach-Object { Write-Host "FAIL: $_" }
    exit 1
}

Write-Host "DeploymentTarget: checks=$checks failures=0"
Write-Host 'PASS: deployment approval manifest contract'
exit 0
