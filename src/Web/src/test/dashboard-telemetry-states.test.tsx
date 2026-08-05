import { BlockedState } from '../components/feedback/BlockedState'
import { EmptyState } from '../components/feedback/EmptyState'
import { ErrorState } from '../components/feedback/ErrorState'
import { ForbiddenState } from '../components/feedback/ForbiddenState'
import { LoadingState } from '../components/feedback/LoadingState'
import { RetryState } from '../components/feedback/RetryState'
import { FeedbackBanner } from '../components/feedback/FeedbackBanner'
import { DataQualityIndicator } from '../components/status/DataQualityIndicator'
import { FreshnessIndicator } from '../components/status/FreshnessIndicator'
import { classifyTelemetryState, formatIntervalSeconds, qualityOf } from '../features/telemetry/PointCurrentRoute'

/** T032 corrective source-visible state decisions; runtime execution remains package-policy blocked. */
export function runDashboardTelemetryStateEvidence(): string[] {
  const failures: string[] = []
  const components = [BlockedState, EmptyState, ErrorState, ForbiddenState, LoadingState, RetryState, FeedbackBanner, DataQualityIndicator, FreshnessIndicator]
  if (!components.every(component => typeof component === 'function')) failures.push('required state components must be importable')
  const cases: Array<[Parameters<typeof classifyTelemetryState>[0], ReturnType<typeof classifyTelemetryState>]> = [
    [{ gatewayState: 'no-selection', dataState: 'NoSelection', hasUsableSnapshot: false }, 'no-selection'],
    [{ gatewayState: 'ready', dataState: 'NotConfigured', hasUsableSnapshot: true }, 'not-configured'],
    [{ gatewayState: 'no-data', dataState: 'NoData', hasUsableSnapshot: true }, 'no-data'],
    [{ gatewayState: 'ready', dataState: 'Data', hasUsableSnapshot: true }, 'data'],
    [{ gatewayState: 'conflict', dataState: 'Ambiguous', hasUsableSnapshot: false }, 'conflict'],
    [{ gatewayState: 'forbidden', hasUsableSnapshot: false }, 'forbidden'],
    [{ gatewayState: 'not-found', hasUsableSnapshot: false }, 'not-found'],
    [{ gatewayState: 'dependency', hasUsableSnapshot: false }, 'dependency'],
    [{ gatewayState: 'expired', hasUsableSnapshot: false }, 'expired'],
    [{ gatewayState: 'runtime-error', hasUsableSnapshot: false }, 'runtime-error'],
    [{ gatewayState: 'runtime-error', dataState: 'Data', hasUsableSnapshot: true, retryableRefresh: true }, 'retryable-stale'],
  ]
  for (const [input, expected] of cases) if (classifyTelemetryState(input) !== expected) failures.push(`${expected} classification must remain distinct`)
  for (const quality of ['Good', 'Uncertain', 'Bad', 'Missing'] as const) if (qualityOf(quality) !== quality) failures.push(`${quality} quality must remain visible`)
  if (qualityOf(undefined) !== 'Missing' || formatIntervalSeconds() !== 'Chưa có' || formatIntervalSeconds(15) !== '15s') failures.push('quality and interval formatting must fail closed')
  return failures
}
