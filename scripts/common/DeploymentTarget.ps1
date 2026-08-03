Set-StrictMode -Version Latest

if (-not (Get-Command New-VerificationResult -ErrorAction SilentlyContinue)) {
    . (Join-Path $PSScriptRoot 'Verification.ps1')
}

$script:DeploymentTargetRequiredFields = @(
    'deploymentModel',
    'webHosting',
    'apiHosting',
    'workerServiceManager',
    'databaseHosting',
    'lifecycleRunbookReference',
    'rollbackRunbookReference',
    'approvalReference',
    'approvedBy',
    'approvedAtUtc'
)
$script:DeploymentTargetSecretKeyPattern = '(?i)(password|secret|token|credential|connectionstring|privatekey|apikey|accesskey)'

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
        -Command 'deployment approval manifest contract (secret redacted)' `
        -Mandatory $true `
        -Evidence $Evidence `
        -ExitCode $ExitCode `
        -BlockerId $BlockerId
}

function Get-DeploymentManifestPropertyValue {
    [CmdletBinding()]
    param(
        [AllowNull()][object]$Manifest,
        [Parameter(Mandatory)][string]$Name
    )

    if ($null -eq $Manifest) {
        return $null
    }
    $property = $Manifest.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function Find-DeploymentManifestSecretKey {
    [CmdletBinding()]
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IEnumerable] -and
        -not ($Value -is [string]) -and
        -not ($Value -is [System.Collections.IDictionary])) {
        foreach ($item in $Value) {
            $nested = Find-DeploymentManifestSecretKey -Value $item
            if (-not [string]::IsNullOrWhiteSpace([string]$nested)) {
                return $nested
            }
        }
        return $null
    }

    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Name -match $script:DeploymentTargetSecretKeyPattern) {
            return [string]$property.Name
        }
        $nested = Find-DeploymentManifestSecretKey -Value $property.Value
        if (-not [string]::IsNullOrWhiteSpace([string]$nested)) {
            return $nested
        }
    }
    return $null
}

function Test-DeploymentTargetApproval {
    [CmdletBinding()]
    param(
        [AllowNull()][string]$ApprovalFlag = $env:IUMP_DEPLOYMENT_TARGET_APPROVED,
        [AllowNull()][string]$EvidencePath = $env:IUMP_DEPLOYMENT_EVIDENCE_PATH,
        [AllowNull()][string]$Ci = $env:CI,
        [AllowNull()][string]$CompanyCiApproved = $env:IUMP_COMPANY_CI_APPROVED,
        [AllowNull()][string]$TrustedEvidenceRoot = $env:IUMP_DEPLOYMENT_TRUSTED_ROOT,
        [AllowNull()][string]$ExpectedSha256 = $env:IUMP_DEPLOYMENT_EVIDENCE_SHA256
    )

    $ciApproved = [string]::Equals($Ci, 'true', [StringComparison]::OrdinalIgnoreCase) -and
        [string]::Equals($CompanyCiApproved, 'true', [StringComparison]::OrdinalIgnoreCase)
    if (-not $ciApproved) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approved company CI context unavailable; trusted deployment approval evidence cannot be verified'
    }

    $flagIsTrue = [string]::Equals($ApprovalFlag, 'true', [StringComparison]::OrdinalIgnoreCase)
    $pathProvided = -not [string]::IsNullOrWhiteSpace($EvidencePath)
    $rootProvided = -not [string]::IsNullOrWhiteSpace($TrustedEvidenceRoot)
    if (-not $flagIsTrue -or -not $pathProvided -or -not $rootProvided) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence (("approval flag=true? {0}; evidence path provided? {1}; " +
                'trusted evidence root provided? {2}; company approval evidence is unavailable') -f `
                $flagIsTrue, $pathProvided, $rootProvided)
    }

    if (-not (Test-Path -LiteralPath $TrustedEvidenceRoot -PathType Container)) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approval flag=true; evidence path provided=yes; trusted evidence root is unavailable'
    }

    try {
        $rootFull = [IO.Path]::GetFullPath($TrustedEvidenceRoot)
        $rootFull = $rootFull.TrimEnd([char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar))
        $evidenceFull = [IO.Path]::GetFullPath($EvidencePath)
    }
    catch {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; trusted path syntax is invalid'
    }
    $separator = [IO.Path]::DirectorySeparatorChar
    $rootItem = Get-Item -LiteralPath $TrustedEvidenceRoot -Force
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approved company CI context=yes; trusted evidence root is a reparse point and cannot establish trust'
    }
    if (-not $evidenceFull.StartsWith($rootFull + $separator, [StringComparison]::OrdinalIgnoreCase)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approved evidence is outside the trusted evidence root'
    }

    $current = $rootFull
    foreach ($segment in ($evidenceFull.Substring($rootFull.Length + 1) -split [regex]::Escape($separator))) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }
        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) {
            break
        }
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
                -Evidence 'approval flag=true; evidence path provided=yes; approved evidence path contains a reparse point escape'
        }
    }

    if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approval flag=true; evidence path provided=yes; approved manifest file is unavailable'
    }

    $expectedSha = [string]$ExpectedSha256
    if ([string]::IsNullOrWhiteSpace($expectedSha)) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approval flag=true; evidence path provided=yes; approved manifest attestation is unavailable'
    }

    try {
        $actualSha = (Get-FileHash -LiteralPath $EvidencePath -Algorithm SHA256 -ErrorAction Stop).Hash
    }
    catch {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approved manifest attestation could not be computed'
    }
    if (-not [string]::Equals($actualSha, $expectedSha, [StringComparison]::OrdinalIgnoreCase)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approved manifest attestation does not match'
    }

    $manifest = $null
    try {
        $manifestText = Get-Content -LiteralPath $EvidencePath -Raw -ErrorAction Stop
        $manifest = $manifestText | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; manifest JSON is malformed or unreadable'
    }

    if ($null -eq $manifest) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; manifest schema is invalid'
    }

    $secretKey = Find-DeploymentManifestSecretKey -Value $manifest
    if (-not [string]::IsNullOrWhiteSpace([string]$secretKey)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; manifest contains a prohibited secret-like field name'
    }

    foreach ($requiredField in $script:DeploymentTargetRequiredFields) {
        $value = Get-DeploymentManifestPropertyValue -Manifest $manifest -Name $requiredField
        if ($null -eq $value -or $value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
            return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
                -Evidence 'approval flag=true; evidence path provided=yes; manifest schema requires non-empty scalar string fields'
        }
    }

    $deploymentModel = [string](Get-DeploymentManifestPropertyValue -Manifest $manifest -Name 'deploymentModel')
    if (-not [string]::Equals($deploymentModel, 'restricted-non-containerized', [StringComparison]::Ordinal)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; deployment model is not the canonical restricted non-containerized value'
    }

    $approvedAtText = [string](Get-DeploymentManifestPropertyValue -Manifest $manifest -Name 'approvedAtUtc')
    if ($approvedAtText -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|\+00:00)$') {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approvedAtUtc must be ISO-8601 UTC with Z or +00:00'
    }

    $approvedAtUtc = [DateTimeOffset]::MinValue
    $dateValid = [DateTimeOffset]::TryParse(
        $approvedAtText,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind,
        [ref]$approvedAtUtc
    )
    if (-not $dateValid) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approvedAtUtc is invalid'
    }
    if ($approvedAtUtc -gt [DateTimeOffset]::UtcNow.AddMinutes(5)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approvedAtUtc is unreasonably in the future'
    }

    New-DeploymentTargetResult -Classification 'PASS' -ExitCode 0 `
        -Evidence 'approved company CI context=yes; approval flag=true; trusted evidence root=inside; attestation=verified; manifest schema=valid; deployment model=canonical; approval reference=present; secret-like keys=none'
}
