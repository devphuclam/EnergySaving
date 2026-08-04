Set-StrictMode -Version Latest

if (-not (Get-Command New-VerificationResult -ErrorAction SilentlyContinue)) {
    . (Join-Path $PSScriptRoot 'Verification.ps1')
}

function New-DeploymentTargetResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Classification,
        [Parameter(Mandatory)][string]$Evidence,
        [Parameter(Mandatory)][int]$ExitCode,
        [AllowNull()][string]$BlockerId = $null
    )

    New-VerificationResult -CheckId 'deployment-target' `
        -Classification $Classification `
        -Command 'atomic signed deployment approval verifier (secret redacted)' `
        -Mandatory $true `
        -Evidence $Evidence `
        -ExitCode $ExitCode `
        -BlockerId $BlockerId
}

function Invoke-DeploymentSignatureVerifier {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string]$SignaturePath,
        [Parameter(Mandatory)][string]$TrustedEvidenceRoot,
        [Parameter(Mandatory)][string]$ExpectedSha256,
        [Parameter(Mandatory)][string]$RepositoryRoot
    )

    if (-not (Test-CommandAvailable -Name 'dotnet')) {
        return [pscustomobject]@{
            Classification = 'BLOCKED_BY_MISSING_TOOL'
            ExitCode = 20
            BlockerId = 'BLK-ENV-001'
            Evidence = 'signed approval verifier requires the preinstalled .NET runtime; dotnet is unavailable'
        }
    }

    $verifierProject = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) `
        'src\Infrastructure\DeploymentApproval\IUMP.Infrastructure.DeploymentApproval.csproj'
    if (-not (Test-Path -LiteralPath $verifierProject -PathType Leaf)) {
        return [pscustomobject]@{
            Classification = 'BLOCKED_BY_MISSING_TOOL'
            ExitCode = 20
            BlockerId = 'BLK-ENV-001'
            Evidence = 'atomic signed approval verifier utility is unavailable'
        }
    }

    $stderrPath = Join-Path ([IO.Path]::GetTempPath()) ('iump-verifier-' + [Guid]::NewGuid().ToString('N') + '.log')
    try {
        $arguments = @(
            'run', '--project', $verifierProject, '--no-restore', '--',
            '--manifest', $ManifestPath,
            '--signature', $SignaturePath,
            '--trusted-root', $TrustedEvidenceRoot,
            '--expected-sha256', $ExpectedSha256,
            '--repository-root', $RepositoryRoot
        )

        try {
            $output = @(& dotnet @arguments 2> $stderrPath)
            $processExitCode = [int]$LASTEXITCODE
        }
        catch {
            return [pscustomobject]@{
                Classification = 'BLOCKED_BY_MISSING_TOOL'
                ExitCode = 20
                BlockerId = 'BLK-ENV-001'
                Evidence = 'atomic signed approval verifier could not be invoked'
            }
        }

        $jsonLines = @($output | ForEach-Object {
                $jsonLine = ([string]$_).Trim()
                if ($jsonLine.StartsWith('{', [StringComparison]::Ordinal) -and
                    $jsonLine.EndsWith('}', [StringComparison]::Ordinal)) {
                    $jsonLine
                }
            })
        if ($jsonLines.Count -ne 1) {
            return [pscustomobject]@{
                Classification = 'FAIL'
                ExitCode = 1
                BlockerId = $null
                Evidence = 'atomic signed approval verifier returned malformed or multiple machine-readable results'
            }
        }

        try {
            $jsonLine = $jsonLines[0]
            $verifierResult = ConvertFrom-Json -InputObject $jsonLine -ErrorAction Stop
            $status = [string]$verifierResult.status
            $classification = [string]$verifierResult.classification
            $exitCode = [int]$verifierResult.exitCode
            $blockerId = if ($null -eq $verifierResult.blockerId) { $null } else { [string]$verifierResult.blockerId }
            $synthetic = [bool]$verifierResult.synthetic
            $manifestReadCount = [int]$verifierResult.manifestReadCount
            $policyReadCount = [int]$verifierResult.policyReadCount
            $evidence = [string]$verifierResult.evidence
        }
        catch {
            return [pscustomobject]@{
                Classification = 'FAIL'
                ExitCode = 1
                BlockerId = $null
                Evidence = 'atomic signed approval verifier returned malformed JSON'
            }
        }

        $validStatus = $status -in @('PASS', 'FAIL', 'BLOCKED')
        $validClassification = $classification -in @('RUNNABLE_NOW', 'BLOCKED_BY_MISSING_TOOL', 'BLOCKED_BY_COMPANY_APPROVAL')
        $validExit = ($status -eq 'PASS' -and $classification -eq 'RUNNABLE_NOW' -and $exitCode -eq 0 -and $null -eq $blockerId) -or
            ($status -eq 'FAIL' -and $classification -eq 'RUNNABLE_NOW' -and $exitCode -eq 1 -and $null -eq $blockerId) -or
            ($status -eq 'BLOCKED' -and $classification -in @('BLOCKED_BY_MISSING_TOOL', 'BLOCKED_BY_COMPANY_APPROVAL') -and $exitCode -eq 20 -and -not [string]::IsNullOrWhiteSpace($blockerId))
        $validReads = $status -eq 'BLOCKED' -or
            ($status -eq 'FAIL' -and $manifestReadCount -in @(0, 1) -and $policyReadCount -in @(0, 1)) -or
            ($status -eq 'PASS' -and $manifestReadCount -eq 1 -and $policyReadCount -eq 1)
        if (-not $validStatus -or -not $validClassification -or -not $validExit -or $synthetic -or -not $validReads -or $processExitCode -ne $exitCode) {
            return [pscustomobject]@{
                Classification = 'FAIL'
                ExitCode = 1
                BlockerId = $null
                Evidence = 'atomic signed approval verifier returned an invalid structured result contract'
            }
        }

        return [pscustomobject]@{
            Classification = if ($status -eq 'PASS') { 'PASS' } elseif ($status -eq 'FAIL') { 'FAIL' } else { $classification }
            ExitCode = $exitCode
            BlockerId = $blockerId
            Evidence = $evidence
            ManifestReadCount = $manifestReadCount
            PolicyReadCount = $policyReadCount
        }
    }
    finally {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Test-DeploymentTargetApproval {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$ApprovalFlag = $env:IUMP_DEPLOYMENT_TARGET_APPROVED,
        [AllowNull()][string]$EvidencePath = $env:IUMP_DEPLOYMENT_EVIDENCE_PATH,
        [AllowNull()][string]$Ci = $env:CI,
        [AllowNull()][string]$CompanyCiApproved = $env:IUMP_COMPANY_CI_APPROVED,
        [AllowNull()][string]$TrustedEvidenceRoot = $env:IUMP_DEPLOYMENT_TRUSTED_ROOT,
        [AllowNull()][string]$ExpectedSha256 = $env:IUMP_DEPLOYMENT_EVIDENCE_SHA256,
        [AllowNull()][string]$SignaturePath = $env:IUMP_DEPLOYMENT_SIGNATURE_PATH
    )

    $ciApproved = [string]::Equals($Ci, 'true', [StringComparison]::OrdinalIgnoreCase) -and
        [string]::Equals($CompanyCiApproved, 'true', [StringComparison]::OrdinalIgnoreCase)
    if (-not $ciApproved) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approved company CI context unavailable; trusted deployment approval evidence cannot be verified'
    }

    $flagIsTrue = [string]::Equals($ApprovalFlag, 'true', [StringComparison]::OrdinalIgnoreCase)
    if (-not $flagIsTrue -or [string]::IsNullOrWhiteSpace($TrustedEvidenceRoot) -or
        [string]::IsNullOrWhiteSpace($EvidencePath) -or [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approval flag, trusted evidence root, manifest path, and SHA-256 attestation are all required company evidence'
    }

    if (-not [IO.File]::Exists($EvidencePath)) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approved manifest evidence is unavailable'
    }

    if ([string]::IsNullOrWhiteSpace($SignaturePath)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'detached signature evidence is unavailable; production approval cannot pass without it'
    }

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $signed = Invoke-DeploymentSignatureVerifier -ManifestPath $EvidencePath `
        -SignaturePath $SignaturePath -TrustedEvidenceRoot $TrustedEvidenceRoot `
        -ExpectedSha256 $ExpectedSha256 -RepositoryRoot $repoRoot
    New-DeploymentTargetResult -Classification $signed.Classification -ExitCode $signed.ExitCode `
        -BlockerId $signed.BlockerId -Evidence $signed.Evidence
}
