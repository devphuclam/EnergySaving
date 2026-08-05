import { BlockedState } from '../components/feedback/BlockedState'
import { EmptyState } from '../components/feedback/EmptyState'
import { ErrorState } from '../components/feedback/ErrorState'
import { ForbiddenState } from '../components/feedback/ForbiddenState'
import { LoadingState } from '../components/feedback/LoadingState'
import { RetryState } from '../components/feedback/RetryState'
import { FeedbackBanner } from '../components/feedback/FeedbackBanner'
import { DataQualityIndicator } from '../components/status/DataQualityIndicator'
import { FreshnessIndicator } from '../components/status/FreshnessIndicator'
import { classifyTelemetryState, formatIntervalSeconds, isRetainableTelemetrySnapshot, qualityOf } from '../features/telemetry/PointCurrentRoute'

/** T032 corrective source-visible state decisions; runtime execution remains package-policy blocked. */
export function runDashboardTelemetryStateEvidence(): string[] {
  const failures: string[] = []
  const components = [BlockedState, EmptyState, ErrorState, ForbiddenState, LoadingState, RetryState, FeedbackBanner, DataQualityIndicator, FreshnessIndicator]
  if (!components.every(component => typeof component === 'function')) failures.push('required state components must be importable')
  const cases: Array<[Parameters<typeof classifyTelemetryState>[0], ReturnType<typeof classifyTelemetryState>]> = [
    [{ gatewayState: 'no-selection', dataState: 'NoSelection' }, 'no-selection'],
    [{ gatewayState: 'ready', dataState: 'NotConfigured' }, 'not-configured'],
    [{ gatewayState: 'no-data', dataState: 'NoData' }, 'no-data'],
    [{ gatewayState: 'ready', snapshot: { state: 'ready', value: 0, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1' }, 'data'],
    [{ gatewayState: 'conflict', dataState: 'Ambiguous' }, 'conflict'],
    [{ gatewayState: 'forbidden' }, 'forbidden'],
    [{ gatewayState: 'not-found' }, 'not-found'],
    [{ gatewayState: 'dependency' }, 'dependency'],
    [{ gatewayState: 'expired' }, 'expired'],
    [{ gatewayState: 'runtime-error' }, 'runtime-error'],
    [{ gatewayState: 'runtime-error', previousSnapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1', retryableRefresh: true }, 'retryable-stale'],
  ]
  for (const [input, expected] of cases) if (classifyTelemetryState(input) !== expected) failures.push(`${expected} classification must remain distinct`)
  for (const quality of ['Good', 'Uncertain', 'Bad', 'Missing'] as const) if (qualityOf(quality) !== quality) failures.push(`${quality} quality must remain visible`)
  if (qualityOf(undefined) !== 'Missing' || formatIntervalSeconds() !== 'Chưa có' || formatIntervalSeconds(15) !== '15s') failures.push('quality and interval formatting must fail closed')
  if (!isRetainableTelemetrySnapshot({ state: 'ready', value: 0, health: 'Online', pointId: 'p-1', dataState: 'Data' }, 'p-1')) failures.push('zero is retainable Data evidence')
  if (isRetainableTelemetrySnapshot({ state: 'ready', value: null, health: 'Online', pointId: 'p-1', dataState: 'Data' }, 'p-1')) failures.push('null Data is not retainable')
  if (!isRetainableTelemetrySnapshot({ state: 'no-data', value: null, health: 'Chưa có dữ liệu', pointId: 'p-1', dataState: 'NoData' }, 'p-1')) failures.push('legitimate NoData evidence is retainable')
  if (classifyTelemetryState({ gatewayState: 'loading', requestPending: true }) !== 'loading') failures.push('selected pending request must render loading')
  const configured = { state: 'ready' as const, value: null, health: 'Unavailable', pointId: 'p-1', dataState: 'NotConfigured' as const }
  if (classifyTelemetryState({ gatewayState: 'dependency', previousSnapshot: configured, selectedPointId: 'p-1', retryableRefresh: true }) !== 'dependency') failures.push('NotConfigured plus dependency must be dependency')
  const noData = { state: 'no-data' as const, value: null, health: 'Chưa có dữ liệu', pointId: 'p-1', dataState: 'NoData' as const }
  if (classifyTelemetryState({ gatewayState: 'dependency', previousSnapshot: noData, selectedPointId: 'p-1', retryableRefresh: true }) !== 'retryable-stale') failures.push('NoData plus dependency may retain stale evidence')
  if (classifyTelemetryState({ gatewayState: 'forbidden', previousSnapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1', retryableRefresh: true }) !== 'forbidden') failures.push('forbidden must clear prior evidence')
  return failures
}
