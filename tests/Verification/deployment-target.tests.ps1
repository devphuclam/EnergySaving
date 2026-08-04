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
$deploymentSource = Get-Content -Raw $deploymentScript

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

Assert-NotContains $deploymentSource 'Get-FileHash' 'PowerShell must not hash the manifest'
Assert-NotContains $deploymentSource 'Get-Content' 'PowerShell must not read the manifest'
Assert-NotContains $deploymentSource '$manifestText' 'PowerShell must not parse manifest text'
Assert-Contains $deploymentSource '--expected-sha256' 'expected SHA-256 must reach verifier'
Assert-Contains $deploymentSource 'ConvertFrom-DeploymentVerifierProcessResult' 'PowerShell must use the behavioral verifier-result parser'
Assert-Contains $deploymentSource 'IUMP_VERIFICATION_RESULT=' 'PowerShell must use the explicit verifier-result protocol'
Assert-Contains $deploymentSource 'manifestReadCount' 'PowerShell must validate manifest read count'
Assert-Contains $deploymentSource 'jsonLines.Count -ne 1' 'PowerShell parser must reject malformed or multiple verifier JSON results'
Assert-NotContains $deploymentSource "StartsWith('{'," 'PowerShell must not discover protocol by arbitrary JSON braces'
Assert-Contains $deploymentSource 'BLOCKED_BY_MISSING_TOOL' 'PowerShell must preserve missing-tool classification'
Assert-Contains $deploymentSource 'BLOCKED_BY_COMPANY_APPROVAL' 'PowerShell must preserve company-approval classification'

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

function Invoke-Fixture {
    param([Parameter(Mandatory)][string]$Root, [string]$Variant = 'valid')
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- --root $Root --variant $Variant 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'signed fixture generation failed' }
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) { throw 'signed fixture returned no machine-readable result' }
    ConvertFrom-Json -InputObject $jsonLine[0]
}

function New-VerifierProtocolLine {
    param([Parameter(Mandatory)]$Result)
    'IUMP_VERIFICATION_RESULT=' + ($Result | ConvertTo-Json -Compress -Depth 5)
}

$validPassJson = [pscustomobject]@{
    status = 'PASS'; classification = 'RUNNABLE_NOW'; exitCode = 0; blockerId = $null
    evidence = 'synthetic parser contract'; synthetic = $false; manifestReadCount = 1; policyReadCount = 1
}
$parsedPass = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('build noise', (New-VerifierProtocolLine $validPassJson)) -ProcessExitCode 0 -Production
Assert-Equal $parsedPass.Classification 'PASS' 'behavioral parser PASS classification'
Assert-Equal $parsedPass.ExitCode 0 'behavioral parser PASS exit'
Assert-Equal $parsedPass.ManifestReadCount 1 'behavioral parser PASS manifest read count'
$parsedNoJson = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('build noise') -ProcessExitCode 1 -Production
Assert-Equal $parsedNoJson.Classification 'FAIL' 'behavioral parser no JSON classification'
$parsedMultiple = ConvertFrom-DeploymentVerifierProcessResult -Stdout @((New-VerifierProtocolLine $validPassJson), (New-VerifierProtocolLine $validPassJson)) -ProcessExitCode 0 -Production
Assert-Equal $parsedMultiple.Classification 'FAIL' 'behavioral parser multiple JSON classification'
$parsedMissingTool = ConvertFrom-DeploymentVerifierProcessResult -Stdout @() -ProcessExitCode 20 -InvocationError 'process-start-failure' -Production
Assert-Equal $parsedMissingTool.Classification 'BLOCKED_BY_MISSING_TOOL' 'behavioral parser missing-tool classification'
Assert-Equal $parsedMissingTool.BlockerId 'BLK-ENV-001' 'behavioral parser missing-tool blocker'
$parsedRuntimeFailure = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('runtime diagnostic') -ProcessExitCode 150 -InvocationError 'verifier-process-failure' -Production
Assert-Equal $parsedRuntimeFailure.Classification 'FAIL' 'behavioral parser started-process crash classification'
Assert-Equal $parsedRuntimeFailure.ExitCode 1 'behavioral parser started-process crash exit'
$parsedExitWithoutProtocol = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('runtime diagnostic') -ProcessExitCode 0 -Production
Assert-Equal $parsedExitWithoutProtocol.Classification 'FAIL' 'behavioral parser zero-exit without protocol classification'
Assert-Equal $parsedExitWithoutProtocol.ExitCode 1 'behavioral parser zero-exit without protocol exit'
$parsedExplicitCrash = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('runtime diagnostic') -ProcessExitCode 150 -InvocationOutcome 'ProcessExitedWithoutProtocol' -Production
Assert-Equal $parsedExplicitCrash.Classification 'FAIL' 'explicit started-process crash classification'
$parsedExplicitStartFailure = ConvertFrom-DeploymentVerifierProcessResult -Stdout @() -ProcessExitCode 20 -InvocationOutcome 'ProcessStartFailure' -Production
Assert-Equal $parsedExplicitStartFailure.Classification 'BLOCKED_BY_MISSING_TOOL' 'explicit process-start failure classification'
$validFailJson = [pscustomobject]@{
    status = 'FAIL'; classification = 'RUNNABLE_NOW'; exitCode = 1; blockerId = $null
    evidence = 'single signed-evidence fault'; synthetic = $false; manifestReadCount = 1; policyReadCount = 0
}
$parsedFail = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $validFailJson) -ProcessExitCode 1 -Production
Assert-Equal $parsedFail.Classification 'FAIL' 'behavioral parser FAIL classification'
Assert-Equal $parsedFail.ExitCode 1 'behavioral parser FAIL exit'
$validCompanyBlockedJson = [pscustomobject]@{
    status = 'BLOCKED'; classification = 'BLOCKED_BY_COMPANY_APPROVAL'; exitCode = 20; blockerId = 'BLK-ENV-005'
    evidence = 'company-managed policy is unavailable'; synthetic = $false; manifestReadCount = 1; policyReadCount = 0
}
$parsedCompanyBlocked = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $validCompanyBlockedJson) -ProcessExitCode 20 -Production
Assert-Equal $parsedCompanyBlocked.Classification 'BLOCKED_BY_COMPANY_APPROVAL' 'behavioral parser company blocker classification'
Assert-Equal $parsedCompanyBlocked.BlockerId 'BLK-ENV-005' 'behavioral parser company blocker id'
$missingFieldJson = [ordered]@{}
$validPassJson.PSObject.Properties | ForEach-Object { $missingFieldJson[$_.Name] = $_.Value }
$missingFieldJson.Remove('policyReadCount')
$parsedMissingField = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('IUMP_VERIFICATION_RESULT=' + ($missingFieldJson | ConvertTo-Json -Compress)) -ProcessExitCode 0 -Production
Assert-Equal $parsedMissingField.Classification 'FAIL' 'behavioral parser missing required field'
$wrongTypeJson = $validPassJson | Select-Object *
$wrongTypeJson.exitCode = '0'
$parsedWrongType = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $wrongTypeJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedWrongType.Classification 'FAIL' 'behavioral parser wrong numeric type'
$wrongBooleanJson = $validPassJson | Select-Object *
$wrongBooleanJson.synthetic = 'false'
$parsedWrongBoolean = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $wrongBooleanJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedWrongBoolean.Classification 'FAIL' 'behavioral parser wrong boolean type'
$wrongBlockerJson = $validPassJson | Select-Object *
$wrongBlockerJson.blockerId = [pscustomobject]@{ value = 'BLK-ENV-001' }
$parsedWrongBlocker = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $wrongBlockerJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedWrongBlocker.Classification 'FAIL' 'behavioral parser wrong blocker type'
$parsedMismatch = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $validPassJson) -ProcessExitCode 1 -Production
Assert-Equal $parsedMismatch.Classification 'FAIL' 'behavioral parser process-exit mismatch'
$invalidBlockerJson = $validPassJson | Select-Object *
$invalidBlockerJson.status = 'BLOCKED'
$invalidBlockerJson.classification = 'BLOCKED_BY_COMPANY_APPROVAL'
$invalidBlockerJson.exitCode = 20
$invalidBlockerJson.blockerId = 'BLK-ENV-001'
$parsedInvalidBlocker = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $invalidBlockerJson) -ProcessExitCode 20 -Production
Assert-Equal $parsedInvalidBlocker.Classification 'FAIL' 'behavioral parser unexpected blocker id'
$redactedJson = $validPassJson | Select-Object *
$redactedJson.evidence = 'secret=must-not-appear'
$parsedRedacted = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $redactedJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedRedacted.Classification 'FAIL' 'behavioral parser secret redaction'
$colonRedactedJson = $validPassJson | Select-Object *
$colonRedactedJson.evidence = 'connectionString: must-not-appear'
$parsedColonRedacted = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $colonRedactedJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedColonRedacted.Classification 'FAIL' 'behavioral parser colon-secret redaction'
$pathEvidenceJson = $validPassJson | Select-Object *
$pathEvidenceJson.evidence = 'C:\sensitive\manifest.json'
$parsedPathEvidence = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $pathEvidenceJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedPathEvidence.Classification 'FAIL' 'behavioral parser path redaction'
$uncEvidenceJson = $validPassJson | Select-Object *
$uncEvidenceJson.evidence = '\\server\share\manifest.json'
$parsedUncEvidence = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $uncEvidenceJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedUncEvidence.Classification 'FAIL' 'behavioral parser UNC path redaction'
$unixEvidenceJson = $validPassJson | Select-Object *
$unixEvidenceJson.evidence = '/tmp/sensitive/manifest.json'
$parsedUnixEvidence = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $unixEvidenceJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedUnixEvidence.Classification 'FAIL' 'behavioral parser Unix path redaction'
$syntheticProductionJson = $validPassJson | Select-Object *
$syntheticProductionJson.synthetic = $true
$parsedSyntheticProduction = ConvertFrom-DeploymentVerifierProcessResult -Stdout @(New-VerifierProtocolLine $syntheticProductionJson) -ProcessExitCode 0 -Production
Assert-Equal $parsedSyntheticProduction.Classification 'FAIL' 'behavioral parser production synthetic rejection'
$malformedJson = ConvertFrom-DeploymentVerifierProcessResult -Stdout @('IUMP_VERIFICATION_RESULT={not-json}') -ProcessExitCode 1 -Production
Assert-Equal $malformedJson.Classification 'FAIL' 'behavioral parser malformed JSON'

$signedVerifierProject = Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj'
function Invoke-SyntheticFixtureVerifier {
    param([Parameter(Mandatory)]$Fixture)
    $trustedRoot = Split-Path -Parent $Fixture.manifestPath
    $expectedSha = (Get-FileHash -LiteralPath $Fixture.manifestPath -Algorithm SHA256).Hash
    $output = & dotnet run --project $signedVerifierProject --no-restore -- --mode synthetic `
        --manifest $Fixture.manifestPath --signature $Fixture.signaturePath --policy $Fixture.policyPath `
        --trusted-root $trustedRoot --expected-sha256 $expectedSha --repository-root $repoRoot 2>$null
    $exitCode = [int]$LASTEXITCODE
    $jsonLine = @($output | Where-Object { $_ -like 'IUMP_VERIFICATION_RESULT=*' } | ForEach-Object { $_.Substring('IUMP_VERIFICATION_RESULT='.Length) })
    if ($jsonLine.Count -ne 1) { throw 'synthetic verifier returned no protocol result' }
    [pscustomobject]@{ Result = ConvertFrom-Json -InputObject $jsonLine[0]; ExitCode = $exitCode }
}

try {
    $validPath = Join-Path $tempRoot 'valid.json'
    (New-ValidManifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $validPath -Encoding UTF8
    $validSha = Get-ManifestSha256 -Path $validPath

    $signedRoot = Join-Path $tempRoot 'signed-fixture'
    $fixtureProject = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $fixtureOutput = & dotnet run --project $fixtureProject --no-restore -- --root $signedRoot 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'signed fixture generation failed' }
    $fixtureJson = @($fixtureOutput | Where-Object { $_ -match '^\s*\{' })
    if ($fixtureJson.Count -ne 1) { throw 'signed fixture JSON result unavailable' }
    $signedFixture = ConvertFrom-Json -InputObject $fixtureJson[0]
    $signedSha = Get-ManifestSha256 -Path $signedFixture.manifestPath

    $singleFaultExpectations = @{
        'malformed-json' = 'manifest JSON is malformed or unreadable'
        'missing-field' = 'manifest schema requires non-empty scalar string fields'
        'non-scalar' = 'manifest schema requires non-empty scalar string fields'
        'wrong-model' = 'deployment model is not the canonical restricted non-containerized value'
        'secret' = 'manifest contains a prohibited secret-like field name'
        'non-utc' = 'approvedAtUtc must be ISO-8601 UTC and not unreasonably in the future'
        'future' = 'approvedAtUtc must be ISO-8601 UTC and not unreasonably in the future'
    }
    foreach ($variant in $singleFaultExpectations.Keys) {
        $faultFixture = Invoke-Fixture -Root (Join-Path $tempRoot ('single-fault-' + $variant)) -Variant $variant
        $faultResult = Invoke-SyntheticFixtureVerifier -Fixture $faultFixture
        Assert-Equal $faultResult.Result.status 'FAIL' "$variant single-fault status"
        Assert-Equal $faultResult.ExitCode 1 "$variant single-fault exit"
        Assert-Contains ([string]$faultResult.Result.evidence) $singleFaultExpectations[$variant] "$variant single-fault evidence"
    }

    $shaMismatchOutput = & dotnet run --project $signedVerifierProject --no-restore -- --mode synthetic `
        --manifest $signedFixture.manifestPath --signature $signedFixture.signaturePath --policy $signedFixture.policyPath `
        --trusted-root (Split-Path -Parent $signedFixture.manifestPath) --expected-sha256 ('0' * 64) --repository-root $repoRoot 2>$null
    $shaMismatchJson = @($shaMismatchOutput | Where-Object { $_ -like 'IUMP_VERIFICATION_RESULT=*' } | ForEach-Object { $_.Substring('IUMP_VERIFICATION_RESULT='.Length) })
    $shaMismatchResult = ConvertFrom-Json -InputObject $shaMismatchJson[0]
    Assert-Equal $shaMismatchResult.status 'FAIL' 'SHA mismatch single-fault status'
    Assert-Contains ([string]$shaMismatchResult.evidence) 'approved manifest attestation does not match' 'SHA mismatch single-fault evidence'

    $productionBlocked = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $signedFixture.manifestPath `
        -SignaturePath $signedFixture.signaturePath -Ci 'true' -CompanyCiApproved 'true' `
        -TrustedEvidenceRoot $signedRoot -ExpectedSha256 $signedSha
    Assert-Equal $productionBlocked.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'signed production policy blocker'
    Assert-Equal $productionBlocked.blockerId 'BLK-ENV-005' 'signed production policy blocker id'

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
            -SignaturePath $signedFixture.signaturePath -Ci 'true' -CompanyCiApproved 'true' `
            -TrustedEvidenceRoot $signedRoot -ExpectedSha256 (Get-ManifestSha256 -Path $outsidePath)
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
            -SignaturePath $signedFixture.signaturePath -Ci 'true' -CompanyCiApproved 'true' `
            -TrustedEvidenceRoot $signedRoot -ExpectedSha256 (Get-ManifestSha256 -Path $traversalPath)
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
