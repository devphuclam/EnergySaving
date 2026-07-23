Set-StrictMode -Version Latest

$script:ValidVerificationClassifications = @(
    'PASS',
    'FAIL',
    'NOT_RUN',
    'BLOCKED_BY_MISSING_TOOL',
    'BLOCKED_BY_PACKAGE_POLICY',
    'BLOCKED_BY_DATABASE_ACCESS',
    'BLOCKED_BY_COMPANY_APPROVAL'
)

function New-VerificationResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CheckId,
        [Parameter(Mandatory)]
        [ValidateScript({
            if ($_ -notin $script:ValidVerificationClassifications) {
                throw "Unknown verification classification: $_"
            }
            $true
        })]
        [string]$Classification,
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][bool]$Mandatory,
        [Parameter(Mandatory)][string]$Evidence,
        [AllowNull()][Nullable[int]]$ExitCode = $null,
        [AllowNull()][string]$BlockerId = $null
    )

    [pscustomobject]@{
        checkId = $CheckId
        classification = $Classification
        command = $Command
        timestamp = [DateTimeOffset]::UtcNow.ToString('o')
        mandatory = $Mandatory
        exitCode = $ExitCode
        evidence = $Evidence
        blockerId = $BlockerId
    }
}

function Test-CommandAvailable {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)

    $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Get-VerificationExitCode {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object[]]$Results)

    $blockingResult = $Results | Where-Object {
        $_.mandatory -and $_.classification -ne 'PASS'
    } | Select-Object -First 1

    if ($null -eq $blockingResult) { return 0 }
    return 20
}

function Write-VerificationResult {
    [CmdletBinding()]
    param([Parameter(Mandatory)][object]$Result)

    $blocker = if ([string]::IsNullOrWhiteSpace($Result.blockerId)) { '' } else { " [$($Result.blockerId)]" }
    Write-Output ("{0}: {1}{2} - {3}" -f $Result.checkId, $Result.classification, $blocker, $Result.evidence)
}
