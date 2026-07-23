[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modulePath = Join-Path $repoRoot 'scripts\common\Verification.ps1'

if (-not (Test-Path -LiteralPath $modulePath)) {
    throw "RED: verification interface is missing at $modulePath"
}

. $modulePath

$required = @(
    'New-VerificationResult',
    'Test-CommandAvailable',
    'Get-VerificationExitCode'
)
foreach ($name in $required) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "RED: required verification command '$name' is missing"
    }
}

$blocked = New-VerificationResult -CheckId 'db' -Classification 'BLOCKED_BY_DATABASE_ACCESS' `
    -Command 'psql --version' -Mandatory $true -Evidence 'psql missing' -BlockerId 'BLK-R0-002'

if ($blocked.classification -ne 'BLOCKED_BY_DATABASE_ACCESS') {
    throw 'Expected blocked classification to be preserved.'
}
if ((Get-VerificationExitCode -Results @($blocked)) -eq 0) {
    throw 'Mandatory blocked checks must produce a non-zero aggregate exit code.'
}

$pass = New-VerificationResult -CheckId 'git' -Classification 'PASS' -Command 'git --version' `
    -Mandatory $true -Evidence 'installed'
if ((Get-VerificationExitCode -Results @($pass)) -ne 0) {
    throw 'All-pass checks must produce exit code zero.'
}

$json = $pass | ConvertTo-Json -Compress
foreach ($requiredKey in @('checkId', 'classification', 'command', 'timestamp', 'mandatory')) {
    if ($json -notmatch ('"' + [regex]::Escape($requiredKey) + '"')) {
        throw "Serialized verification result is missing canonical key '$requiredKey'."
    }
}
if ($json -cmatch '"CheckId"|"Classification"') {
    throw 'Serialized verification result must use lower-camel contract keys.'
}
$allowedKeys = @('checkId', 'classification', 'command', 'timestamp', 'mandatory', 'exitCode', 'evidence', 'blockerId')
$actualKeys = @($pass.PSObject.Properties.Name)
if (@($actualKeys | Where-Object { $_ -notin $allowedKeys }).Count -gt 0) {
    throw 'Serialized verification result contains a key outside the canonical schema.'
}

Write-Output 'PASS: verification result contract'
