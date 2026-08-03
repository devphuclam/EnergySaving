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
$script:DeploymentTargetSecretKeyPattern = '(?i)(password|secret|token|credential|connectionstring|privatekey)'

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
        [AllowNull()][string]$EvidencePath = $env:IUMP_DEPLOYMENT_EVIDENCE_PATH
    )

    $flagIsTrue = [string]::Equals($ApprovalFlag, 'true', [StringComparison]::OrdinalIgnoreCase)
    $pathProvided = -not [string]::IsNullOrWhiteSpace($EvidencePath)
    if (-not $flagIsTrue -or -not $pathProvided) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence (("approval flag=true? {0}; evidence path provided? {1}; " +
                'company approval evidence is unavailable') -f $flagIsTrue, $pathProvided)
    }

    if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
        return New-DeploymentTargetResult -Classification 'BLOCKED_BY_COMPANY_APPROVAL' `
            -ExitCode 20 -BlockerId 'BLK-ENV-005' `
            -Evidence 'approval flag=true; evidence path provided=yes; approved manifest file is unavailable'
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
        if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
            return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
                -Evidence 'approval flag=true; evidence path provided=yes; manifest schema is missing a required non-empty field'
        }
    }

    $deploymentModel = [string](Get-DeploymentManifestPropertyValue -Manifest $manifest -Name 'deploymentModel')
    if (-not [string]::Equals($deploymentModel, 'restricted-non-containerized', [StringComparison]::Ordinal)) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; deployment model is not the canonical restricted non-containerized value'
    }

    $approvedAtUtc = [DateTimeOffset]::MinValue
    $dateValid = [DateTimeOffset]::TryParse(
        [string](Get-DeploymentManifestPropertyValue -Manifest $manifest -Name 'approvedAtUtc'),
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind,
        [ref]$approvedAtUtc
    )
    if (-not $dateValid) {
        return New-DeploymentTargetResult -Classification 'FAIL' -ExitCode 1 `
            -Evidence 'approval flag=true; evidence path provided=yes; approvedAtUtc is invalid'
    }

    New-DeploymentTargetResult -Classification 'PASS' -ExitCode 0 `
        -Evidence 'approval flag=true; evidence path provided=yes; manifest schema=valid; deployment model=canonical; approval reference=present; secret-like keys=none'
}
