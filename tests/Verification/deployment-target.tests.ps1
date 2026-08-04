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

function Get-ManifestSha256 {
    param([Parameter(Mandatory)][string]$Path)
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

try {
    $validPath = Join-Path $tempRoot 'valid.json'
    (New-ValidManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $validPath -Encoding UTF8
    $validSha = Get-ManifestSha256 -Path $validPath

    $blocked = Test-DeploymentTargetApproval -ApprovalFlag '' -EvidencePath ''
    Assert-Equal $blocked.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing approval classification'
    Assert-Equal $blocked.blockerId 'BLK-ENV-005' 'missing approval blocker'
    Assert-Equal $blocked.exitCode 20 'missing approval exit'

    $noCi = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci '' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $noCi.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'no CI context classification'
    Assert-Equal $noCi.blockerId 'BLK-ENV-005' 'no CI context blocker'

    $falseCi = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci 'false' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $falseCi.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'false CI classification'

    $notCompanyApproved = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'false' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $notCompanyApproved.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'company CI not approved classification'

    $noRoot = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot '' -ExpectedSha256 $validSha
    Assert-Equal $noRoot.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing trusted root classification'

    $noFlagWithContext = Test-DeploymentTargetApproval -ApprovalFlag '' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $noFlagWithContext.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing flag with context classification'

    $outsideDir = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $outsidePath = Join-Path $outsideDir ('iump-outside-' + [Guid]::NewGuid().ToString('N') + '.json')
    (New-ValidManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $outsidePath -Encoding UTF8
    try {
        $outsideResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $outsidePath `
            -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
        Assert-Equal $outsideResult.classification 'FAIL' 'outside root classification'
        Assert-Blank $outsideResult.blockerId 'outside root blocker'
        Assert-Equal $outsideResult.exitCode 1 'outside root exit'
    }
    finally {
        Remove-Item -LiteralPath $outsidePath -Force -ErrorAction SilentlyContinue
    }

    $traversalPath = Join-Path $tempRoot ('..\iump-traversal-' + [Guid]::NewGuid().ToString('N') + '.json')
    (New-ValidManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $traversalPath -Encoding UTF8
    try {
        $traversalResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $traversalPath `
            -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
        Assert-Equal $traversalResult.classification 'FAIL' 'path traversal classification'
        Assert-Blank $traversalResult.blockerId 'path traversal blocker'
        Assert-Equal $traversalResult.exitCode 1 'path traversal exit'
    }
    finally {
        Remove-Item -LiteralPath $traversalPath -Force -ErrorAction SilentlyContinue
    }

    $escapeLink = Join-Path $tempRoot ('escape-' + [Guid]::NewGuid().ToString('N'))
    $escapeTarget = Join-Path $outsideDir ('iump-escape-target-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $escapeTarget -Force | Out-Null
    $escapedManifestPath = Join-Path $escapeTarget 'manifest.json'
    (New-ValidManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $escapedManifestPath -Encoding UTF8
    $escapedSha = Get-ManifestSha256 -Path $escapedManifestPath
    try {
        New-Item -ItemType Junction -Path $escapeLink -Target $escapeTarget -ErrorAction Stop | Out-Null
        $reparseResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' `
            -EvidencePath (Join-Path $escapeLink 'manifest.json') `
            -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $escapedSha
        Assert-Equal $reparseResult.classification 'FAIL' 'reparse escape classification'
        Assert-Blank $reparseResult.blockerId 'reparse escape blocker'
        Assert-Equal $reparseResult.exitCode 1 'reparse escape exit'
    }
    finally {
        if (Test-Path -LiteralPath $escapeLink) {
            & cmd /c rmdir "$escapeLink" 2>$null
        }
        Remove-Item -LiteralPath $escapeTarget -Recurse -Force -ErrorAction SilentlyContinue
    }

    $noSha = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 ''
    Assert-Equal $noSha.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing attestation classification'

    $mismatchSha = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 ('0' * 64)
    Assert-Equal $mismatchSha.classification 'FAIL' 'attestation mismatch classification'
    Assert-Equal $mismatchSha.exitCode 1 'attestation mismatch exit'

    $falseFlag = Test-DeploymentTargetApproval -ApprovalFlag 'false' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $falseFlag.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'false approval classification'
    Assert-Equal $falseFlag.blockerId 'BLK-ENV-005' 'false approval blocker'

    $missingEvidence = Test-DeploymentTargetApproval -ApprovalFlag 'true' `
        -EvidencePath (Join-Path $tempRoot 'missing.json') `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $missingEvidence.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing evidence classification'
    Assert-Equal $missingEvidence.blockerId 'BLK-ENV-005' 'missing evidence blocker'

    $malformedPath = Join-Path $tempRoot 'malformed.json'
    '{"deploymentModel":' | Set-Content -LiteralPath $malformedPath -Encoding UTF8
    $malformedSha = Get-ManifestSha256 -Path $malformedPath
    $malformed = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $malformedPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $malformedSha
    Assert-Equal $malformed.classification 'FAIL' 'malformed JSON classification'
    Assert-Blank $malformed.blockerId 'malformed JSON blocker'
    Assert-Equal $malformed.exitCode 1 'malformed JSON exit'

    $missingFieldPath = Join-Path $tempRoot 'missing-field.json'
    $missingField = New-ValidManifest
    $missingField.Remove('rollbackRunbookReference')
    ($missingField | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $missingFieldPath -Encoding UTF8
    $missingFieldSha = Get-ManifestSha256 -Path $missingFieldPath
    $missingFieldResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $missingFieldPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $missingFieldSha
    Assert-Equal $missingFieldResult.classification 'FAIL' 'missing field classification'

    $wrongModelPath = Join-Path $tempRoot 'wrong-model.json'
    $wrongModel = New-ValidManifest
    $wrongModel.deploymentModel = 'containerized'
    ($wrongModel | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $wrongModelPath -Encoding UTF8
    $wrongModelSha = Get-ManifestSha256 -Path $wrongModelPath
    $wrongModelResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $wrongModelPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $wrongModelSha
    Assert-Equal $wrongModelResult.classification 'FAIL' 'wrong model classification'

    $nonScalarPath = Join-Path $tempRoot 'non-scalar.json'
    $nonScalarManifest = New-ValidManifest
    $nonScalarManifest.webHosting = [ordered]@{ value = 'not-a-scalar' }
    ($nonScalarManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $nonScalarPath -Encoding UTF8
    $nonScalarSha = Get-ManifestSha256 -Path $nonScalarPath
    $nonScalarResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $nonScalarPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $nonScalarSha
    Assert-Equal $nonScalarResult.classification 'FAIL' 'non-scalar field classification'

    $apiKeyPath = Join-Path $tempRoot 'api-key.json'
    $apiKeyManifest = New-ValidManifest
    $apiKeyManifest.apiKey = 'must-not-be-accepted'
    ($apiKeyManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $apiKeyPath -Encoding UTF8
    $apiKeySha = Get-ManifestSha256 -Path $apiKeyPath
    $apiKeyResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $apiKeyPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $apiKeySha
    Assert-Equal $apiKeyResult.classification 'FAIL' 'api key field classification'

    $localDatePath = Join-Path $tempRoot 'local-date.json'
    $localDateManifest = New-ValidManifest
    $localDateManifest.approvedAtUtc = '2026-08-03T00:00:00'
    ($localDateManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $localDatePath -Encoding UTF8
    $localDateSha = Get-ManifestSha256 -Path $localDatePath
    $localDateResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $localDatePath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $localDateSha
    Assert-Equal $localDateResult.classification 'FAIL' 'non-UTC date classification'

    $futureDatePath = Join-Path $tempRoot 'future-date.json'
    $futureDateManifest = New-ValidManifest
    $futureDateManifest.approvedAtUtc = '2099-08-03T00:00:00Z'
    ($futureDateManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $futureDatePath -Encoding UTF8
    $futureDateSha = Get-ManifestSha256 -Path $futureDatePath
    $futureDateResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $futureDatePath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $futureDateSha
    Assert-Equal $futureDateResult.classification 'FAIL' 'future date classification'

    $secretPath = Join-Path $tempRoot 'secret-key.json'
    $secretManifest = New-ValidManifest
    $secretManifest.secret = 'must-not-be-echoed'
    ($secretManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $secretPath -Encoding UTF8
    $secretSha = Get-ManifestSha256 -Path $secretPath
    $secretResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $secretPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $secretSha
    Assert-Equal $secretResult.classification 'FAIL' 'secret key classification'
    Assert-NotContains ([string]$secretResult.evidence) 'must-not-be-echoed' 'secret value redaction'

    $nestedSecretPath = Join-Path $tempRoot 'nested-secret-key.json'
    $nestedSecretManifest = New-ValidManifest
    $nestedSecretManifest.metadata = [ordered]@{ token = 'nested-must-not-be-echoed' }
    ($nestedSecretManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $nestedSecretPath -Encoding UTF8
    $nestedSecretSha = Get-ManifestSha256 -Path $nestedSecretPath
    $nestedSecretResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $nestedSecretPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $nestedSecretSha
    Assert-Equal $nestedSecretResult.classification 'FAIL' 'nested secret key classification'
    Assert-NotContains ([string]$nestedSecretResult.evidence) 'nested-must-not-be-echoed' 'nested secret value redaction'

    $badDatePath = Join-Path $tempRoot 'bad-date.json'
    $badDate = New-ValidManifest
    $badDate.approvedAtUtc = 'not-a-date'
    ($badDate | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $badDatePath -Encoding UTF8
    $badDateSha = Get-ManifestSha256 -Path $badDatePath
    $badDateResult = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $badDatePath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $badDateSha
    Assert-Equal $badDateResult.classification 'FAIL' 'invalid date classification'

    $unsignedPass = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $validPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot $tempRoot -ExpectedSha256 $validSha
    Assert-Equal $unsignedPass.classification 'FAIL' 'unsigned manifest cannot pass'
    Assert-Equal $unsignedPass.exitCode 1 'unsigned manifest exit'
    Assert-Contains ([string]$unsignedPass.evidence) 'detached signature evidence is unavailable' 'unsigned manifest evidence text'
    Assert-NotContains ([string]$unsignedPass.evidence) 'approved static web host' 'manifest content not logged'
    Assert-NotContains ([string]$unsignedPass.evidence) 'APPROVAL-2026-001' 'approval reference not logged'

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
