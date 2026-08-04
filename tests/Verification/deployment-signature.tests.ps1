[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $repoRoot 'scripts\common\DeploymentTarget.ps1')

$checks = 0
$failures = 0
$verifierSource = Get-Content -Raw (Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\Program.cs')

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

Assert-SourceContains $verifierSource 'allowedSignerCertificateSha256' 'policy v2 certificate SHA-256 identity'
Assert-SourceContains $verifierSource 'policyReadCount' 'policy single-read evidence'
Assert-SourceContains $verifierSource 'X509RevocationMode.Online' 'online revocation support'
Assert-SourceContains $verifierSource 'X509RevocationMode.Offline' 'offline revocation support'
Assert-SourceContains $verifierSource 'FileShare.Read' 'policy snapshot denies write/delete sharing'
Assert-SourceContains $verifierSource 'GetAccessControl' 'policy ACL verification'
Assert-SourceContains $verifierSource 'DeleteSubdirectoriesAndFiles' 'parent delete-child ACL verification'
Assert-SourceContains $verifierSource 'ReadPolicySnapshot' 'policy single-read implementation'
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
        [string]$PolicyPath = $Fixture.policyPath
    )
    $project = Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj'
    $trustedRoot = Split-Path -Parent $Fixture.manifestPath
    $expectedSha = (Get-FileHash -LiteralPath $Fixture.manifestPath -Algorithm SHA256).Hash
    $output = & dotnet run --project $project --no-restore -- --mode synthetic `
        --manifest $Fixture.manifestPath --signature $Fixture.signaturePath --policy $PolicyPath `
        --trusted-root $trustedRoot --expected-sha256 $expectedSha --repository-root $repoRoot 2>$null
    $jsonLine = @($output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
    if ($jsonLine.Count -ne 1) {
        throw 'signed verifier returned no machine-readable result'
    }
    ConvertFrom-Json -InputObject $jsonLine[0]
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
    $mismatchJson = @($mismatchOutput | Where-Object { $_ -match '^\s*\{' })
    Assert-Equal ((ConvertFrom-Json $mismatchJson[0]).status) 'FAIL' 'expected SHA mismatch status'

    foreach ($variant in @('wrong-signer', 'expired', 'eku-mismatch', 'secret', 'sha1', 'weak-rsa', 'policy-v1')) {
        $fixture = Invoke-Fixture -Root (Join-Path $tempRoot $variant) -Variant $variant
        Assert-Equal (Invoke-SyntheticVerifier -Fixture $fixture).status 'FAIL' "$variant contract status"
    }

    $missingPolicy = Invoke-SyntheticVerifier -Fixture $valid -PolicyPath (Join-Path $tempRoot 'missing-policy.json')
    Assert-Equal $missingPolicy.status 'BLOCKED' 'missing trust anchor status'
    Assert-Equal $missingPolicy.classification 'BLOCKED_BY_COMPANY_APPROVAL' 'missing trust anchor classification'

    $productionProject = Join-Path $repoRoot 'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj'
    $validSha = (Get-FileHash -LiteralPath $valid.manifestPath -Algorithm SHA256).Hash
    $productionOutput = & dotnet run --project $productionProject --no-restore -- `
        --manifest $valid.manifestPath --signature $valid.signaturePath `
        --trusted-root (Split-Path -Parent $valid.manifestPath) --expected-sha256 $validSha --repository-root $repoRoot 2>$null
    $productionJson = @($productionOutput | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1)
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
