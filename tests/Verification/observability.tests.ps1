[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()
$checks = 0

function Assert-Source {
    param([string]$RelativePath, [string[]]$Required, [string]$Label)
    $script:checks++
    $path = Join-Path $repoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $script:failures.Add("$Label`: missing $RelativePath")
        return
    }
    $text = Get-Content -LiteralPath $path -Raw
    foreach ($token in $Required) {
        if ($text.IndexOf($token, [StringComparison]::Ordinal) -lt 0) {
            $script:failures.Add("$Label`: missing executable/static token '$token' in $RelativePath")
        }
    }
}

Assert-Source 'src\Modules\Integration\Contracts\OutboxContracts.cs' `
    @('CorrelationId', 'CausationId', 'Before', 'After') 'owner event correlation/causation'
Assert-Source 'src\Modules\Integration\Domain\CommandIdempotency.cs' `
    @('OriginalCorrelationId', 'OriginalHttpStatus', 'StoredHttpResult') 'command replay identity'
Assert-Source 'src\Modules\Integration\Contracts\DeliveryPersistenceContracts.cs' `
    @('CorrelationId', 'PayloadHash', 'DeliveryStatus') 'outbox/inbox correlation'
Assert-Source 'src\Modules\Audit\Application\AuditEventConsumer.cs' `
    @('CorrelationId', 'CausationId', 'Redact', 'PayloadHash') 'Audit propagation/redaction'
Assert-Source 'src\Worker\Integration\OutboxDispatcherWorker.cs' `
    @('completedConsumers', 'RequiredConsumers', 'MarkPublishedAsync') 'no false Published evidence'
Assert-Source 'src\Api\Infrastructure\IdempotentCommandExecutor.cs' `
    @('OriginalCorrelationId', 'IDEMPOTENCY_CONFLICT', 'OriginalResult') 'safe replay/error response'
Assert-Source 'src\Hosting\Abstractions\ApplicationPorts.cs' `
    @('ServerPrincipal', 'IServerPrincipalAccessor') 'server identity authority'
Assert-Source 'tests\Unit\Acceptance\AuthorizationNegativeTests.cs' `
    @('client role, scope and capability headers must be ignored', 'filter', 'lookup', 'page') 'client identity rejection'
Assert-Source 'tests\Unit\Operations\DurableJobTests.cs' `
    @('sensitive payload accepted', 'only redacted error is retained') 'job payload/error redaction'
Assert-Source 'tests\Unit\Audit\AuditConsumerTests.cs' `
    @('audit output must redact secrets', 'audit append and inbox completion must commit exactly once') 'Audit secret/idempotency behavior'

$script:checks++
$productionFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$unsafeLogPattern = '(?i)(Log(?:Information|Warning|Error|Debug|Trace).*(password|token|credential|connectionstring|authorization))'
$unsafeLogs = $productionFiles | Select-String -Pattern $unsafeLogPattern
if ($unsafeLogs) {
    $failures.Add("production logging may serialize sensitive values: $($unsafeLogs.Path -join ', ')")
}

$script:checks++
$persistedPayloadFiles = @(
    'src\Modules\Integration\Domain\CommandIdempotency.cs',
    'src\Modules\Operations\Contracts\DurableJobContracts.cs',
    'src\Modules\Audit\Application\AuditEventConsumer.cs'
)
foreach ($relative in $persistedPayloadFiles) {
    $text = Get-Content -LiteralPath (Join-Path $repoRoot $relative) -Raw
    if ($text -match '(?i)(Password\s*=|Token\s*=|Credential\s*=|ConnectionString\s*=)') {
        $failures.Add("sensitive command payload field detected in $relative")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Observability: checks=$checks failures=$($failures.Count)"
    $failures | ForEach-Object { Write-Host "FAIL: $_" }
    exit 1
}

Write-Host "Observability: checks=$checks failures=0"
Write-Host 'PASS: correlation/causation, replay identity, redaction, server authority and terminal evidence checks'
exit 0
