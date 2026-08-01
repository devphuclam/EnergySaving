$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$route = Get-Content (Join-Path $root 'src/Web/src/features/telemetry/PointCurrentRoute.tsx') -Raw
$gateway = Get-Content (Join-Path $root 'src/Web/src/gateways/webGateways.ts') -Raw
$api = Get-Content (Join-Path $root 'src/Api/TelemetryQueryEndpoints.cs') -Raw
$failures = [System.Collections.Generic.List[string]]::new()
if ($route -match 'points\[0\]|FirstOrDefault') { $failures.Add('telemetry route must not implicitly select the first Point') }
if ($gateway -match 'points\[0\]|FirstOrDefault') { $failures.Add('telemetry gateway must not implicitly select the first Point') }
if ($route -notmatch '10_000|10000') { $failures.Add('telemetry route must schedule the ten-second refresh') }
if ($route -notmatch 'autoRefresh') { $failures.Add('telemetry route must expose the auto-refresh control') }
if ($route -notmatch 'refreshNonce') { $failures.Add('telemetry route must expose manual refresh') }
if ($api -notmatch '/api/v1/telemetry/workspace/options' -or $api -notmatch '/api/v1/telemetry/workspace/current') { $failures.Add('telemetry selector endpoints are missing') }
if ($failures.Count -gt 0) { $failures | ForEach-Object { Write-Output "FAIL: $_" }; exit 1 }
Write-Output 'PASS: telemetry Phase 4 selector/refresh closure contract'
exit 0
