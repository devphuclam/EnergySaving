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
if ((Get-VerificationExitCode -Results @($blocked)) -ne 20) {
    throw 'Mandatory blocked checks must produce aggregate exit code 20.'
}

$failed = New-VerificationResult -CheckId 'architecture' -Classification 'FAIL' `
    -Command 'architecture.tests.ps1' -Mandatory $true -Evidence 'forbidden dependency'
if ((Get-VerificationExitCode -Results @($failed)) -ne 1) {
    throw 'Mandatory failed checks must produce aggregate exit code 1.'
}
if ((Get-VerificationExitCode -Results @($blocked, $failed)) -ne 1) {
    throw 'FAIL must take precedence over blocked results.'
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

$schemaPath = Join-Path $repoRoot 'docs\contracts\verification-result.schema.json'
if (-not (Test-Path -LiteralPath $schemaPath)) {
    throw "Project-wide verification schema is missing: $schemaPath"
}
$schema = Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json
if ($schema.'$id' -ne 'urn:iump:verification-result:v1') {
    throw 'Verification schema must use the project-wide identifier.'
}
$schemaClassifications = @($schema.properties.classification.enum)
foreach ($classification in @(
    'PASS',
    'FAIL',
    'NOT_RUN',
    'BLOCKED_BY_MISSING_TOOL',
    'BLOCKED_BY_PACKAGE_POLICY',
    'BLOCKED_BY_DATABASE_ACCESS',
    'BLOCKED_BY_COMPANY_APPROVAL'
)) {
    if ($classification -notin $schemaClassifications) {
        throw "Verification schema is missing classification '$classification'."
    }
}

Write-Output 'PASS: verification result contract'
