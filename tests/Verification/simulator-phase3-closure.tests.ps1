[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$issues = @()

$endpointSource = Get-Content (Join-Path $repoRoot 'src\Api\SimulatorEndpoints.cs') -Raw
$workspacePorts = Get-Content (Join-Path $repoRoot 'src\Composition\Postgres\PostgresSimulatorWorkspacePorts.cs') -Raw
$routeSource = Get-Content (Join-Path $repoRoot 'src\Web\src\features\simulator\SimulatorRoute.tsx') -Raw
$gatewaySource = Get-Content (Join-Path $repoRoot 'src\Web\src\gateways\webGateways.ts') -Raw
$retryHelper = Get-Content (Join-Path $repoRoot 'src\Web\src\gateways\simulatorRetry.ts') -Raw -ErrorAction SilentlyContinue

if ($endpointSource -match 'MapPost\("/\{sourceId:guid\}/start"') {
    $issues += 'Legacy source-only Start route remains mapped.'
}
if ($endpointSource -match 'MapPost\("/\{runId:guid\}/(pause|resume|stop)"') {
    $issues += 'Legacy Run-only control route remains mapped.'
}
if ($workspacePorts -match 'commands\.ExecuteAsync\(operation, selection\.SourceId') {
    $issues += 'Workspace Start still delegates with Source ID only.'
}
if (-not (Test-Path (Join-Path $repoRoot 'src\Web\src\gateways\simulatorRetry.ts'))) {
    $issues += 'Pure simulator retry-key helper is missing.'
}
if ($retryHelper -notmatch 'createPendingSimulatorMutation' -or
    $retryHelper -notmatch 'mutationIdentityMatches' -or
    $retryHelper -notmatch 'selectionFingerprint') {
    $issues += 'Pure retry-key helper must fingerprint the complete selection and preserve identity.'
}
if ($routeSource -notmatch 'URLSearchParams|window\.location') {
    $issues += 'Simulator selection is not reconstructed from the URL.'
}
if ($routeSource -notmatch 'id="simulator-site"' -or
    $routeSource -notmatch 'id="simulator-area"' -or
    $routeSource -notmatch 'id="simulator-asset"' -or
    $routeSource -notmatch 'id="simulator-source"' -or
    $routeSource -notmatch 'id="simulator-configuration"') {
    $issues += 'Dependent Site/Area/Asset/Source/configuration selectors are incomplete.'
}
if ($gatewaySource -notmatch 'pendingSimulatorMutation|idempotencyKey') {
    $issues += 'Web gateway has no persisted pending mutation identity.'
}
if ($retryHelper -notmatch 'RUNTIME_DEPENDENCY_UNAVAILABLE' -or
    $retryHelper -notmatch 'DEPENDENCY_UNAVAILABLE' -or
    $retryHelper -notmatch 'status === 503' -or
    $retryHelper -notmatch 'runtime-error') {
    $issues += 'Pure Simulator error helper must distinguish dependency codes/status from runtime errors.'
}
if ($gatewaySource -notmatch 'simulatorErrorKind' -or
    $gatewaySource -notmatch 'isRetryableSimulatorError' -or
    $gatewaySource -notmatch 'request-503' -or
    $gatewaySource -notmatch 'TypeError' -or
    $gatewaySource -notmatch 'MALFORMED_RESPONSE' -or
    $gatewaySource -notmatch "error.message === 'MALFORMED_RESPONSE'") {
    $issues += 'Simulator gateway must apply dependency/runtime mapping and preserve retryable failures.'
}
if ($routeSource -notmatch 'dependencyMessage' -or
    $routeSource -notmatch 'runtimeMessage') {
    $issues += 'Simulator UI must expose distinct Vietnamese dependency and runtime messages.'
}

if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Error $_ }
    Write-Output "simulator-phase3-closure: failures=$($issues.Count)"
    exit 1
}

Write-Output 'simulator-phase3-closure: failures=0'
