$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$route = Get-Content (Join-Path $root 'src/Web/src/features/telemetry/PointCurrentRoute.tsx') -Raw
$gateway = Get-Content (Join-Path $root 'src/Web/src/gateways/webGateways.ts') -Raw
$coordinator = Get-Content (Join-Path $root 'src/Web/src/features/telemetry/telemetryRefreshCoordinator.ts') -Raw -ErrorAction SilentlyContinue
$coordinatorTest = Join-Path $root 'tests/Verification/telemetry-refresh-coordinator.tests.ts'
$api = Get-Content (Join-Path $root 'src/Api/TelemetryQueryEndpoints.cs') -Raw
$failures = [System.Collections.Generic.List[string]]::new()
if ($route -match 'points\[0\]|FirstOrDefault') { $failures.Add('telemetry route must not implicitly select the first Point') }
if ($gateway -match 'points\[0\]|FirstOrDefault') { $failures.Add('telemetry gateway must not implicitly select the first Point') }
if ($route -notmatch '10_000|10000') { $failures.Add('telemetry route must schedule the ten-second refresh') }
if ($route -notmatch 'autoRefresh') { $failures.Add('telemetry route must expose the auto-refresh control') }
if ($route -notmatch 'refreshNonce' -and $route -notmatch 'refreshCoordinator\.current\?*\.refresh') { $failures.Add('telemetry route must expose manual refresh') }
if ($api -notmatch '/api/v1/telemetry/workspace/options' -or $api -notmatch '/api/v1/telemetry/workspace/current') { $failures.Add('telemetry selector endpoints are missing') }
if ([string]::IsNullOrWhiteSpace($coordinator)) { $failures.Add('telemetry route must have the pure refresh coordinator helper') }
else {
    foreach ($token in @('class LatestRefreshCoordinator', 'select(', 'setAutoRefresh(', 'refresh(', 'dispose(', 'AbortController', 'isCurrent')) {
        if ($coordinator -notmatch [regex]::Escape($token)) { $failures.Add("refresh coordinator is missing '$token'") }
    }
}
foreach ($token in @('mergeSelectedPointOption', 'refreshCoordinator.current', '.select(', '.refresh()', '.setAutoRefresh(')) {
    if ($route -notmatch [regex]::Escape($token)) { $failures.Add("telemetry route is missing corrective coordination token '$token'") }
}
if ($gateway -notmatch 'getSnapshot: \(selection\?: TelemetrySelection, signal\?: AbortSignal\)') {
    $failures.Add('Latest gateway must accept an AbortSignal for selection invalidation')
}
if ($gateway -notmatch 'request<\{[\s\S]*\}>\(`/api/v1/telemetry/workspace/current\?\$\{query\}`, \{ signal \}\)') {
    $failures.Add('Latest gateway must pass the AbortSignal to the current-data request')
}
if (-not (Test-Path -LiteralPath $coordinatorTest)) { $failures.Add('pure coordinator contract test is missing') }
elseif (-not (Get-Command node -ErrorAction SilentlyContinue)) { $failures.Add('Node runtime is missing for pure coordinator contract test') }
else {
    & node --experimental-strip-types $coordinatorTest
    if ($LASTEXITCODE -ne 0) { $failures.Add('pure coordinator contract test failed') }
}
if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Output "FAIL: $_" }; exit 1 }
Write-Output 'PASS: telemetry Phase 4 selector/refresh closure contract'
exit 0
