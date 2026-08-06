import { BlockedState } from '../components/feedback/BlockedState'
import { EmptyState } from '../components/feedback/EmptyState'
import { ErrorState } from '../components/feedback/ErrorState'
import { ForbiddenState } from '../components/feedback/ForbiddenState'
import { LoadingState } from '../components/feedback/LoadingState'
import { RetryState } from '../components/feedback/RetryState'
import { FeedbackBanner } from '../components/feedback/FeedbackBanner'
import { ConflictState } from '../components/feedback/ConflictState'
import { DataQualityIndicator } from '../components/status/DataQualityIndicator'
import { FreshnessIndicator } from '../components/status/FreshnessIndicator'
import { classifyTelemetryState, formatIntervalSeconds, hasNumericTelemetryData, isRetainableTelemetrySnapshot } from '../features/telemetry/PointCurrentRoute'

/** T032 corrective source-visible state decisions; runtime execution remains package-policy blocked. */
export const DASHBOARD_TELEMETRY_STATE_EVIDENCE_EXPECTED_FAILURES = 0

export function runDashboardTelemetryStateEvidence(): string[] {
  const failures: string[] = []
  if (DASHBOARD_TELEMETRY_STATE_EVIDENCE_EXPECTED_FAILURES !== 0) failures.push('state evidence is expected to return an empty failure array')
  const components = [BlockedState, EmptyState, ErrorState, ForbiddenState, LoadingState, RetryState, FeedbackBanner, ConflictState, DataQualityIndicator, FreshnessIndicator]
  if (!components.every(component => typeof component === 'function')) failures.push('required state components must be importable')
  const noSelectionMatrix = { state: 'no-selection' as const, value: null, health: 'NoSelection', dataState: 'NoSelection' as const }
  const notConfiguredMatrix = { state: 'ready' as const, value: null, health: 'Unavailable', pointId: 'p-1', dataState: 'NotConfigured' as const }
  const noDataMatrix = { state: 'no-data' as const, value: null, health: 'NoData', pointId: 'p-1', dataState: 'NoData' as const }
  const retainedDataMatrix = { state: 'ready' as const, value: 0, health: 'Online', pointId: 'p-1', dataState: 'Data' as const }
  const classifierMatrix: Array<[string, Parameters<typeof classifyTelemetryState>[0], ReturnType<typeof classifyTelemetryState>]> = [
    ['gateway no-selection', { gatewayState: 'no-selection', dataState: 'NoSelection', snapshot: noSelectionMatrix, previousSnapshot: noSelectionMatrix }, 'no-selection'],
    ['successful NoSelection', { gatewayState: 'ready', dataState: 'NoSelection', snapshot: noSelectionMatrix, previousSnapshot: noSelectionMatrix }, 'no-selection'],
    ['dependency with NoSelection', { gatewayState: 'dependency', dataState: 'NoSelection', snapshot: noSelectionMatrix, previousSnapshot: noSelectionMatrix, retryableRefresh: true }, 'dependency'],
    ['runtime error with NoSelection', { gatewayState: 'runtime-error', dataState: 'NoSelection', snapshot: noSelectionMatrix, previousSnapshot: noSelectionMatrix, retryableRefresh: true }, 'runtime-error'],
    ['dependency with NotConfigured', { gatewayState: 'dependency', dataState: 'NotConfigured', snapshot: notConfiguredMatrix, previousSnapshot: notConfiguredMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'dependency'],
    ['dependency with NoData', { gatewayState: 'dependency', dataState: 'NoData', snapshot: noDataMatrix, previousSnapshot: noDataMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'retryable-stale'],
    ['runtime error with NoData', { gatewayState: 'runtime-error', dataState: 'NoData', snapshot: noDataMatrix, previousSnapshot: noDataMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'retryable-stale'],
    ['dependency with finite zero', { gatewayState: 'dependency', dataState: 'Data', snapshot: retainedDataMatrix, previousSnapshot: retainedDataMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'retryable-stale'],
    ['forbidden with retained Data', { gatewayState: 'forbidden', dataState: 'Data', snapshot: retainedDataMatrix, previousSnapshot: retainedDataMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'forbidden'],
    ['expired with retained Data', { gatewayState: 'expired', dataState: 'Data', snapshot: retainedDataMatrix, previousSnapshot: retainedDataMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'expired'],
    ['conflict with retained Data', { gatewayState: 'conflict', dataState: 'Data', snapshot: retainedDataMatrix, previousSnapshot: retainedDataMatrix, selectedPointId: 'p-1', retryableRefresh: true }, 'conflict'],
    ['successful NoData', { gatewayState: 'ready', dataState: 'NoData', snapshot: noDataMatrix, previousSnapshot: noDataMatrix, selectedPointId: 'p-1' }, 'no-data'],
    ['successful finite Data', { gatewayState: 'ready', dataState: 'Data', snapshot: retainedDataMatrix, previousSnapshot: retainedDataMatrix, selectedPointId: 'p-1' }, 'data'],
  ]
  for (const [label, input, expected] of classifierMatrix) {
    if (classifyTelemetryState(input) !== expected) failures.push(`${label} must classify as ${expected}`)
  }
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
    [{ gatewayState: 'runtime-error', dataState: undefined, snapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, previousSnapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1', retryableRefresh: true }, 'retryable-stale'],
  ]
  for (const [input, expected] of cases) if (classifyTelemetryState(input) !== expected) failures.push(`${expected} classification must remain distinct`)
  if (formatIntervalSeconds() !== 'Chưa có' || formatIntervalSeconds(15) !== '15s') failures.push('interval formatting must fail closed')
  const zero = { state: 'ready' as const, value: 0, health: 'Online', pointId: 'p-1', dataState: 'Data' as const }
  if (!hasNumericTelemetryData(zero, 'p-1') || !isRetainableTelemetrySnapshot(zero, 'p-1')) failures.push('zero is numeric and retainable Data evidence')
  if (!hasNumericTelemetryData({ ...zero, value: 12 }, 'p-1')) failures.push('positive Data is numeric')
  if (isRetainableTelemetrySnapshot({ state: 'ready', value: null, health: 'Online', pointId: 'p-1', dataState: 'Data' }, 'p-1')) failures.push('null Data is not retainable')
  const noData = { state: 'no-data' as const, value: null, health: 'Chưa có dữ liệu', pointId: 'p-1', dataState: 'NoData' as const }
  if (!isRetainableTelemetrySnapshot(noData, 'p-1') || hasNumericTelemetryData(noData, 'p-1')) failures.push('legitimate NoData is retainable but never numeric')
  if (classifyTelemetryState({ gatewayState: 'loading', requestPending: true }) !== 'loading') failures.push('selected pending request must render loading')
  const configured = { state: 'ready' as const, value: null, health: 'Unavailable', pointId: 'p-1', dataState: 'NotConfigured' as const }
  if (classifyTelemetryState({ gatewayState: 'dependency', dataState: undefined, snapshot: configured, previousSnapshot: configured, selectedPointId: 'p-1', retryableRefresh: true }) !== 'dependency') failures.push('NotConfigured plus dependency must be dependency')
  if (classifyTelemetryState({ gatewayState: 'dependency', dataState: undefined, snapshot: noData, previousSnapshot: noData, selectedPointId: 'p-1', retryableRefresh: true }) !== 'retryable-stale') failures.push('NoData plus dependency may retain stale evidence')
  if (classifyTelemetryState({ gatewayState: 'runtime-error', dataState: undefined, snapshot: noData, previousSnapshot: noData, selectedPointId: 'p-1', retryableRefresh: true }) !== 'retryable-stale') failures.push('NoData plus runtime error may retain stale evidence')
  if (classifyTelemetryState({ gatewayState: 'dependency', dataState: undefined, snapshot: { state: 'no-selection', value: null, health: 'NoSelection', dataState: 'NoSelection' }, previousSnapshot: { state: 'no-selection', value: null, health: 'NoSelection', dataState: 'NoSelection' }, retryableRefresh: true }) !== 'dependency') failures.push('NoSelection plus dependency must remain dependency')
  if (classifyTelemetryState({ gatewayState: 'dependency', dataState: undefined, snapshot: { state: 'ready', value: null, health: 'Unavailable', pointId: 'p-1', dataState: 'NotConfigured' }, previousSnapshot: { state: 'ready', value: null, health: 'Unavailable', pointId: 'p-1', dataState: 'NotConfigured' }, selectedPointId: 'p-1', retryableRefresh: true }) !== 'dependency') failures.push('NotConfigured plus dependency must remain dependency')
  if (classifyTelemetryState({ gatewayState: 'forbidden', dataState: undefined, snapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, previousSnapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1', retryableRefresh: true }) !== 'forbidden') failures.push('forbidden must clear prior evidence')
  if (classifyTelemetryState({ gatewayState: 'expired', dataState: undefined, snapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, previousSnapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1', retryableRefresh: true }) !== 'expired') failures.push('expired must clear prior evidence')
  if (classifyTelemetryState({ gatewayState: 'conflict', dataState: undefined, snapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, previousSnapshot: { state: 'ready', value: 12, health: 'Online', pointId: 'p-1', dataState: 'Data' }, selectedPointId: 'p-1', retryableRefresh: true }) !== 'conflict') failures.push('conflict must clear prior evidence')
  return failures
}
