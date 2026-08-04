[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $repoRoot 'scripts\common\DeploymentTarget.ps1')

$checks = 0
$failures = 0
$verifierSource = Get-Content -Raw (Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\Program.cs')
$classifierSource = Get-Content -Raw (Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\ChainStatusClassifier.cs')
$aclSource = Get-Content -Raw (Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\PolicyAclEvaluator.cs')
$pathSource = Get-Content -Raw (Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\CanonicalPathPolicy.cs')
$handleSecurityPath = Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\HandleSecurityEvaluator.cs'
$handleSecuritySource = if (Test-Path -LiteralPath $handleSecurityPath) { Get-Content -Raw $handleSecurityPath } else { '' }
$handleTestSeamPath = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\HandleSecurityEvaluatorTestSeam.cs'
$handleTestSeamSource = if (Test-Path -LiteralPath $handleTestSeamPath) { Get-Content -Raw $handleTestSeamPath } else { '' }

function Assert-SourceContains {
    param([string]$Text, [string]$Expected, [string]$Name)
    $script:checks++
    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        $script:failures++
        Write-Error ("FAIL: {0}; missing={1}" -f $Name, $Expected)
    }
}

function Assert-SourceNotContains {
    param([string]$Text, [string]$Unexpected, [string]$Name)
    $script:checks++
    if ($Text.IndexOf($Unexpected, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $script:failures++
        Write-Error ("FAIL: {0}; forbidden={1}" -f $Name, $Unexpected)
    }
}

function Assert-SourcePattern {
    param([string]$Text, [string]$Pattern, [string]$Name)
    $script:checks++
    if ($Text -notmatch $Pattern) {
        $script:failures++
        Write-Error ("FAIL: {0}; missing pattern={1}" -f $Name, $Pattern)
    }
}

Assert-SourceContains $verifierSource 'allowedSignerCertificateSha256' 'policy v2 certificate SHA-256 identity'
Assert-SourceContains $verifierSource 'policyReadCount' 'policy single-read evidence'
Assert-SourceContains $verifierSource 'X509RevocationMode.Online' 'online revocation support'
Assert-SourceContains $verifierSource 'X509RevocationMode.Offline' 'offline revocation support'
Assert-SourceContains $verifierSource 'HandleSecurityEvaluator' 'production policy uses handle security evaluator'
Assert-SourceContains $handleSecuritySource 'GetSecurityInfo' 'handle security descriptor retrieval'
Assert-SourceContains $handleSecuritySource 'AccessCheck' 'Windows effective-access evaluation'
Assert-SourceContains $handleSecuritySource 'GetFileInformationByHandle' 'handle file identity retrieval'
Assert-SourceContains $handleTestSeamSource 'HasUnsafeEffectiveAccessForTest' 'fixture-only effective-access seam'
Assert-SourceContains $verifierSource 'FileShare.Read' 'policy snapshot denies write/delete sharing'
Assert-SourceNotContains $verifierSource 'GetAccessControl' 'production policy does not reopen pathname ACLs'
Assert-SourceNotContains $verifierSource 'PolicyAclEvaluator.HasEffectiveUnsafePermission' 'production policy does not use custom ACL authority'
Assert-SourceNotContains $verifierSource 'HasUnsafeEffectiveAccessForTest' 'production verifier does not use fixture seam'
Assert-SourceContains $verifierSource 'ReadPolicySnapshot' 'policy single-read implementation'
Assert-SourceContains $pathSource 'CanonicalizeRoot' 'root path canonicalization'
Assert-SourceContains $verifierSource 'CanonicalPathPolicy.CanonicalizeRoot' 'verifier uses rooted path policy'
Assert-SourceContains $verifierSource 'Path.GetFullPath(Path.Combine' 'rooted ancestor traversal'
Assert-SourceContains $verifierSource 'HandleSecurityTarget.PolicyFile' 'policy file threat model seam'
Assert-SourceContains $verifierSource 'HandleSecurityTarget.ImmediateDirectory' 'immediate policy directory threat model seam'
Assert-SourceContains $verifierSource 'HandleSecurityTarget.AncestorDirectory' 'higher policy ancestor threat model seam'
Assert-SourcePattern $handleSecuritySource '(?s)AncestorUnsafeRights\s*=\s*\[\s*FileDeleteChild' 'ancestor delete-child threat right'
Assert-SourceContains $aclSource 'InheritanceFlags' 'ACL inheritance applicability'
Assert-SourceContains $aclSource 'PropagationFlags' 'ACL propagation applicability'
Assert-SourceContains $aclSource 'AccessControlType.Deny' 'ACL deny precedence'
Assert-SourceContains $classifierSource 'ChainStatusClassifier' 'chain status classifier extraction'
Assert-SourceContains $classifierSource 'RevocationStatusUnknown' 'revocation-unavailable chain classification'
Assert-SourceContains $classifierSource 'OfflineRevocation' 'offline-revocation chain classification'
Assert-SourceContains $verifierSource 'ChainStatusClassifier.Classify' 'verifier uses chain status classifier'
Assert-SourceContains $classifierSource 'ClassifyException' 'chain exception classification'
Assert-SourceContains $aclSource 'AccessControlType.Deny' 'ACL deny precedence implementation'
Assert-SourceContains $aclSource 'PropagationFlags.InheritOnly' 'ACL propagation implementation'
Assert-SourceContains $verifierSource 'BLOCKED_BY_MISSING_TOOL' 'missing capability result'
Assert-SourceNotContains $verifierSource 'X509RevocationMode.NoCheck' 'production revocation must not disable checks'
Assert-SourceNotContains $verifierSource 'allowedSignerThumbprints' 'SHA-1 thumbprint policy must be retired'

function Assert-Equal {
    param($Actual, $Expected, [string]$Name)
    $script:checks++
    if ($Actual -ne $Expected) {
        $script:failures++
        Write-Error ("FAIL: {0}; expected={1}; actual={2}" -f $Name, $Expected, $Actual)
    }
}

function Assert-NotEqual {
    param($Actual, $Unexpected, [string]$Name)
    $script:checks++
    if ($Actual -eq $Unexpected) {
        $script:failures++
        Write-Error ("FAIL: {0}; unexpected={1}" -f $Name, $Unexpected)
    }
}

function Invoke-Fixture {
    param([Parameter(Mandatory)][string]$Root, [string]$Variant = 'valid')
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- --root $Root --variant $Variant 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'signature fixture generator failed'
    }
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw 'signature fixture generator returned no machine-readable result'
    }
    ConvertFrom-Json -InputObject $jsonLine[0]
}

function Invoke-SyntheticVerifier {
    param(
        [Parameter(Mandatory)]$Fixture,
        [string]$PolicyPath = $Fixture.policyPath,
        [string]$TrustedRoot = $null
    )
    $project = Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj'
    $trustedRoot = if ([string]::IsNullOrWhiteSpace($TrustedRoot)) { Split-Path -Parent $Fixture.manifestPath } else { $TrustedRoot }
    $expectedSha = (Get-FileHash -LiteralPath $Fixture.manifestPath -Algorithm SHA256).Hash
    $output = & dotnet run --project $project --no-restore -- --mode synthetic `
        --manifest $Fixture.manifestPath --signature $Fixture.signaturePath --policy $PolicyPath `
        --trusted-root $trustedRoot --expected-sha256 $expectedSha --repository-root $repoRoot 2>$null
    $jsonLine = @($output | Where-Object { $_ -like 'IUMP_VERIFICATION_RESULT=*' } | ForEach-Object { $_.Substring('IUMP_VERIFICATION_RESULT='.Length) } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw 'signed verifier returned no machine-readable result'
    }
    ConvertFrom-Json -InputObject $jsonLine[0]
}

function Invoke-ChainScenario {
    param([Parameter(Mandatory)][string]$Scenario)
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- --chain-status $Scenario 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "chain-status fixture failed for $Scenario"
    }
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw "chain-status fixture returned no result for $Scenario"
    }
    (ConvertFrom-Json -InputObject $jsonLine[0]).disposition
}

function Invoke-ChainExceptionScenario {
    param([Parameter(Mandatory)][string]$Scenario)
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- --chain-exception $Scenario 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "chain-exception fixture failed for $Scenario"
    }
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw "chain-exception fixture returned no result for $Scenario"
    }
    (ConvertFrom-Json -InputObject $jsonLine[0]).disposition
}

function Invoke-AclScenario {
    param([Parameter(Mandatory)][string]$Scenario)
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- --acl-status $Scenario 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "acl-status fixture failed for $Scenario"
    }
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw "acl-status fixture returned no result for $Scenario"
    }
    (ConvertFrom-Json -InputObject $jsonLine[0]).unsafePermission
}

function Invoke-RootScenario {
    param([Parameter(Mandatory)][string]$Scenario)
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- --root-path $Scenario 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "root-path fixture failed for $Scenario"
    }
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw "root-path fixture returned no result for $Scenario"
    }
    (ConvertFrom-Json -InputObject $jsonLine[0]).canonicalRoot
}

function Invoke-HandleScenario {
    param([Parameter(Mandatory)][string]$Scenario)
    $project = Join-Path $repoRoot 'tests\Verification\DeploymentSignatureFixture\DeploymentSignatureFixture.csproj'
    $output = & dotnet run --project $project --no-restore -- ("--{0}" -f $Scenario) 'true' 2>$null
    $exitCode = [int]$LASTEXITCODE
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw "handle fixture returned no result for $Scenario"
    }
    [pscustomobject]@{ Result = ConvertFrom-Json -InputObject $jsonLine[0]; ExitCode = $exitCode }
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('iump-signed-approval-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    $valid = Invoke-Fixture -Root (Join-Path $tempRoot 'valid')
    $validResult = Invoke-SyntheticVerifier -Fixture $valid
    Assert-Equal $validResult.status 'PASS' 'valid synthetic signature status'
    Assert-Equal $validResult.synthetic $true 'valid synthetic signature boundary'
    Assert-Equal $validResult.manifestReadCount 1 'manifest single-read count'
    Assert-Equal $validResult.policyReadCount 1 'policy single-read count'
    Assert-Equal $validResult.exitCode 0 'valid synthetic exit code'
    $driveRootResult = Invoke-SyntheticVerifier -Fixture $valid -TrustedRoot ([IO.Path]::GetPathRoot($valid.manifestPath))
    Assert-Equal $driveRootResult.status 'PASS' 'drive-root trusted evidence boundary'

    $unsigned = [pscustomobject]@{
        manifestPath = $valid.manifestPath
        signaturePath = Join-Path $tempRoot 'missing.p7s'
        policyPath = $valid.policyPath
    }
    $unsignedResult = Invoke-SyntheticVerifier -Fixture $unsigned
    Assert-Equal $unsignedResult.status 'FAIL' 'unsigned manifest status'

    $malformedSignature = Join-Path $tempRoot 'malformed.p7s'
    [IO.File]::WriteAllBytes($malformedSignature, [Text.Encoding]::UTF8.GetBytes('not-cms'))
    $malformed = [pscustomobject]@{
        manifestPath = $valid.manifestPath
        signaturePath = $malformedSignature
        policyPath = $valid.policyPath
    }
    Assert-Equal (Invoke-SyntheticVerifier -Fixture $malformed).status 'FAIL' 'malformed signature status'

    $modifiedManifest = Join-Path $tempRoot 'modified.json'
    Copy-Item -LiteralPath $valid.manifestPath -Destination $modifiedManifest
    Add-Content -LiteralPath $modifiedManifest -Value ' ' -NoNewline
    $modified = [pscustomobject]@{
        manifestPath = $modifiedManifest
        signaturePath = $valid.signaturePath
        policyPath = $valid.policyPath
    }
    Assert-Equal (Invoke-SyntheticVerifier -Fixture $modified).status 'FAIL' 'modified manifest status'

    $mismatch = Invoke-SyntheticVerifier -Fixture $valid
    $mismatchOutput = & dotnet run --project (Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj') --no-restore -- --mode synthetic `
        --manifest $valid.manifestPath --signature $valid.signaturePath --policy $valid.policyPath `
        --trusted-root (Split-Path -Parent $valid.manifestPath) --expected-sha256 ('0' * 64) --repository-root $repoRoot 2>$null
    $mismatchJson = @($mismatchOutput | Where-Object { $_ -like 'IUMP_VERIFICATION_RESULT=*' } | ForEach-Object { $_.Substring('IUMP_VERIFICATION_RESULT='.Length) })
    Assert-Equal ((ConvertFrom-Json $mismatchJson[0]).status) 'FAIL' 'expected SHA mismatch status'

    foreach ($variant in @('wrong-signer', 'expired', 'eku-mismatch', 'secret', 'sha1', 'weak-rsa', 'policy-v1')) {
        $fixture = Invoke-Fixture -Root (Join-Path $tempRoot $variant) -Variant $variant
        Assert-Equal (Invoke-SyntheticVerifier -Fixture $fixture).status 'FAIL' "$variant contract status"
    }

    Assert-Equal (Invoke-ChainScenario -Scenario 'fatal') 'Invalid' 'fatal chain status takes precedence over revocation uncertainty'
    Assert-Equal (Invoke-ChainScenario -Scenario 'mixed') 'Invalid' 'mixed fatal and revocation-unavailable chain status fails'
    Assert-Equal (Invoke-ChainScenario -Scenario 'revocation-unavailable') 'Blocked' 'revocation-unavailable-only chain status blocks'
    Assert-Equal (Invoke-ChainScenario -Scenario 'empty') 'Invalid' 'empty failed chain status is invalid'
    Assert-Equal (Invoke-ChainExceptionScenario -Scenario 'crypto') 'Invalid' 'cryptographic chain exception fails closed'
    Assert-Equal (Invoke-ChainExceptionScenario -Scenario 'platform') 'MissingTool' 'platform chain capability is missing-tool'
    Assert-Equal (Invoke-AclScenario -Scenario 'allow') $true 'ACL explicit allow is unsafe'
    Assert-Equal (Invoke-AclScenario -Scenario 'deny') $false 'ACL deny takes precedence over allow'
    Assert-Equal (Invoke-AclScenario -Scenario 'inherit-only') $false 'ACL inherit-only rule is not effective on directory'
    Assert-Equal (Invoke-AclScenario -Scenario 'inherited') $true 'ACL inherited container rule is effective on directory'
    Assert-Equal (Invoke-AclScenario -Scenario 'inherited-file') $true 'ACL inherited object rule is effective on file'
    Assert-Equal (Invoke-AclScenario -Scenario 'replacement') $true 'ACL replacement rights are unsafe'
    Assert-Equal (Invoke-AclScenario -Scenario 'delete-child') $true 'ACL delete-child rights are unsafe'
    Assert-Equal (Invoke-AclScenario -Scenario 'other-user') $false 'ACL unrelated identity is not applicable'
    Assert-Equal (Invoke-RootScenario -Scenario 'drive') ([IO.Path]::GetPathRoot($env:SystemDrive + '\')) 'drive root remains rooted'
    $uncRoot = Invoke-RootScenario -Scenario 'unc'
    Assert-Equal ($uncRoot -like '\\server\share*') $true 'UNC root remains rooted'
    Assert-Equal ($uncRoot -like '\\server\sharefolder*') $false 'UNC root boundary remains canonical'

    $handle = Invoke-HandleScenario -Scenario 'handle-contract'
    Assert-Equal $handle.ExitCode 0 'handle security fixture exit'
    Assert-Equal $handle.Result.identityStable $true 'file identity is stable before and after read'
    Assert-Equal $handle.Result.replacementBlocked $true 'no-delete sharing blocks replacement while handle is open'
    Assert-Equal $handle.Result.fileUnsafe $true 'effective file write/ownership rights are not trusted'
    Assert-Equal $handle.Result.directoryUnsafe $true 'effective immediate-directory replacement rights are not trusted'
    Assert-Equal $handle.Result.ancestorUnsafe $true 'effective ancestor delete/ownership rights are not trusted'
    Assert-Equal $handle.Result.policyReadCount 1 'handle policy read count'
    Assert-Equal $handle.Result.securitySource 'handle' 'security decision source is opened handle'
    $capability = Invoke-HandleScenario -Scenario 'handle-capability'
    Assert-Equal $capability.ExitCode 0 'missing handle capability fixture exit'
    Assert-Equal $capability.Result.capabilityUnavailable $true 'missing handle capability fails closed'

    $effective = Invoke-HandleScenario -Scenario 'effective-access'
    Assert-Equal $effective.ExitCode 0 'positive effective-access fixture exit'
    Assert-Equal $effective.Result.safeNoUnsafe $true 'empty descriptor has no unsafe access'
    Assert-Equal $effective.Result.readOnlySafe $true 'read-only descriptor is safe'
    Assert-Equal $effective.Result.writeDataUnsafe $true 'effective FILE_WRITE_DATA is unsafe'
    Assert-Equal $effective.Result.deleteUnsafe $true 'effective DELETE is unsafe'
    Assert-Equal $effective.Result.ancestorDeleteUnsafe $true 'ancestor DELETE is unsafe'
    Assert-Equal $effective.Result.ancestorDeleteChildUnsafe $true 'ancestor FILE_DELETE_CHILD is unsafe'
    Assert-Equal $effective.Result.writeDacUnsafe $true 'effective WRITE_DAC is unsafe'
    Assert-Equal $effective.Result.ancestorWriteDacUnsafe $true 'ancestor WRITE_DAC is unsafe'
    Assert-Equal $effective.Result.writeOwnerUnsafe $true 'effective WRITE_OWNER is unsafe'
    Assert-Equal $effective.Result.ancestorWriteOwnerUnsafe $true 'ancestor WRITE_OWNER is unsafe'
    Assert-Equal $effective.Result.explicitDenySafe $true 'explicit deny wins in AccessCheck'
    Assert-Equal $effective.Result.ancestorSiblingCreateSafe $true 'ancestor sibling creation is not descendant replacement'
    Assert-Equal $effective.Result.invalidDescriptorBlocked $true 'invalid descriptor fails closed'

    $missingPolicy = Invoke-SyntheticVerifier -Fixture $valid -PolicyPath (Join-Path $tempRoot 'missing-policy.json')
    Assert-Equal $missingPolicy.status 'BLOCKED' 'missing trust anchor status'
    Assert-Equal $missingPolicy.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing trust anchor classification'

    $productionProject = Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj'
    $validSha = (Get-FileHash -LiteralPath $valid.manifestPath -Algorithm SHA256).Hash
    $productionOutput = & dotnet run --project $productionProject --no-restore -- `
        --manifest $valid.manifestPath --signature $valid.signaturePath `
        --trusted-root (Split-Path -Parent $valid.manifestPath) --expected-sha256 $validSha --repository-root $repoRoot 2>$null
    $productionJson = @($productionOutput | Where-Object { $_ -like 'IUMP_VERIFICATION_RESULT=*' } | ForEach-Object { $_.Substring('IUMP_VERIFICATION_RESULT='.Length) } | Select-Object -Last 1)
    if ($productionJson.Count -ne 1) { throw 'production verifier returned no machine-readable result' }
    $production = ConvertFrom-Json -InputObject $productionJson[0]
    Assert-NotEqual $production.status 'PASS' 'production synthetic signer cannot pass'

    $environmentOnly = Test-DeploymentTargetApproval -ApprovalFlag 'true' -EvidencePath $valid.manifestPath `
        -Ci 'true' -CompanyCiApproved 'true' -TrustedEvidenceRoot (Split-Path $valid.manifestPath) `
        -ExpectedSha256 $validSha -SignaturePath $valid.signaturePath
    Assert-NotEqual $environmentOnly.classification 'PASS' 'environment-only approval cannot pass'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output ("DeploymentSignature: checks={0} failures={1}" -f $checks, $failures)
if ($failures -gt 0) {
    Write-Output 'FAIL: signed deployment approval contract'
    exit 1
}

Write-Output 'PASS: signed deployment approval contract'
exit 0
