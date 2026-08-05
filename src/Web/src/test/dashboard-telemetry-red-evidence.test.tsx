import { DASHBOARD_QUALITY_REASON_UNAVAILABLE, OperationalDashboard, dashboardExceptionItems, dashboardExceptionPresentation, dashboardFreshness, dashboardHealthPresentation } from '../features/dashboard/OperationalDashboard'
import { PointCurrentRoute, classifyTelemetryState, formatIntervalSeconds, isRetainableTelemetrySnapshot, qualityOf } from '../features/telemetry/PointCurrentRoute'
import type { OperationalDashboardSnapshot } from '../gateways/webGateways'
import { ChartContainer, chartSegments, type EvidenceChartPoint } from '../components/charts/ChartContainer'

const fixture: OperationalDashboardSnapshot = {
  state: 'ready', roleMode: 'Engineer',
  sites: { count: 1, items: [{ name: 'Site A' }] }, sources: { count: 1, items: [{ name: 'Source A' }] },
  points: { count: 2, items: [{ pointId: 'p-1', code: 'P-001', description: 'Main meter' }, { pointId: 'p-2', code: 'P-002', description: 'Backup meter' }] }, runs: { count: 1, items: [{ runId: 'r-1' }] },
  latest: { count: 1, items: [{ pointId: 'p-1', value: 0, unit: 'kWh', quality: 'Good' }] },
  health: { count: 1, items: [{ pointId: 'p-1', status: 'Stale', lastReceivedAtUtc: '2026-08-05T00:00:00Z' }] },
  incompleteSetup: { count: 0 }, recentAudit: { items: [] }, runtime: { status: 'Available', simulatorRunning: false }, dependency: { status: 'Available' },
}

/** T028/T029/T030/T031 source-visible red evidence; runtime frontend runner remains package-policy blocked. */
export function runDashboardTelemetryRedEvidence(): string[] {
  const failures: string[] = []
  if (typeof OperationalDashboard !== 'function' || typeof PointCurrentRoute !== 'function') failures.push('dashboard and telemetry routes must be importable')
  if (typeof ChartContainer !== 'function') failures.push('ChartContainer must be importable')
  const exceptions = dashboardExceptionItems(fixture)
  if (!exceptions.some(item => item.kind === 'health' && item.status === 'Stale')) failures.push('stale source health must surface before summary')
  if (!exceptions.some(item => item.key === 'missing-latest')) failures.push('points without latest evidence must remain visible')
  if (dashboardFreshness(fixture) !== 'Stale') failures.push('stale health must drive stale freshness')
  if (dashboardHealthPresentation('Online').status !== 'Good' || dashboardHealthPresentation('Online').freshness !== 'Live') failures.push('Online must map to Good/Live')
  if (dashboardHealthPresentation('NoData').status !== 'Missing' || dashboardHealthPresentation('NoData').freshness !== 'Degraded') failures.push('NoData must map to Missing/Degraded')
  if (dashboardHealthPresentation('Suspended').status !== 'Blocked' || dashboardHealthPresentation('Suspended').freshness !== 'Degraded') failures.push('Suspended must map to Blocked/Degraded')
  if (dashboardHealthPresentation('Decommissioned').status !== 'Unavailable' || dashboardHealthPresentation('Decommissioned').freshness !== 'Degraded') failures.push('Decommissioned must map to Unavailable/Degraded')
  if (dashboardHealthPresentation('Unknown').freshness === 'Live' || dashboardFreshness({ ...fixture, health: { count: 0, items: [] } }) !== 'Unavailable') failures.push('unknown or empty health must never become Live')
  if (!exceptions.some(item => item.title.includes('P-001'))) failures.push('health identity must join to point code')
  if (classifyTelemetryState({ gatewayState: 'ready', dataState: 'NotConfigured' }) !== 'not-configured') failures.push('NotConfigured must have its own presentation')
  if (classifyTelemetryState({ gatewayState: 'no-data', dataState: 'NoData' }) !== 'no-data') failures.push('NoData must remain distinct from configuration absence')
  if (classifyTelemetryState({ gatewayState: 'conflict', dataState: 'HierarchyConflict' }) !== 'conflict') failures.push('HierarchyConflict must use ConflictState')
  if (classifyTelemetryState({ gatewayState: 'expired' }) !== 'expired') failures.push('expired must have explicit session presentation')
  const retainedData = { state: 'ready' as const, value: 0, health: 'Online', pointId: 'p-1', dataState: 'Data' as const }
  if (!isRetainableTelemetrySnapshot(retainedData, 'p-1')) failures.push('finite Data with matching point identity must be retainable')
  if (classifyTelemetryState({ gatewayState: 'dependency', previousSnapshot: retainedData, selectedPointId: 'p-1', retryableRefresh: true }) !== 'retryable-stale') failures.push('retryable refresh must retain only legitimate previous Data evidence')
  const notConfigured = { state: 'ready' as const, value: null, health: 'Unavailable', pointId: 'p-1', dataState: 'NotConfigured' as const }
  if (classifyTelemetryState({ gatewayState: 'dependency', previousSnapshot: notConfigured, selectedPointId: 'p-1', retryableRefresh: true }) !== 'dependency') failures.push('NotConfigured plus dependency must not become retryable-stale')
  if (classifyTelemetryState({ gatewayState: 'loading', requestPending: true }) !== 'loading') failures.push('selected pending request must render loading')
  if (classifyTelemetryState({ gatewayState: 'ready', dataState: 'Data', snapshot: { ...retainedData, value: null }, selectedPointId: 'p-1' }) !== 'runtime-error') failures.push('malformed Data must fail closed')
  if (qualityOf('unknown') !== 'Missing') failures.push('unknown quality must fail closed to Missing')
  if (formatIntervalSeconds() !== 'Chưa có' || formatIntervalSeconds(10) !== '10s') failures.push('interval formatting must not produce Chưa cós')
  if (DASHBOARD_QUALITY_REASON_UNAVAILABLE !== 'Dashboard contract không cung cấp quality reason.') failures.push('dashboard contract limitation must not be passed as quality reason')
  const beyondVisibleLimit = { ...fixture, health: { count: 10, items: Array.from({ length: 9 }, (_, index) => ({ pointId: `p-${index + 1}`, status: index === 8 ? 'Stale' : 'Online' })) } }
  const exceptionPresentation = dashboardExceptionPresentation(beyondVisibleLimit, 8)
  if (exceptionPresentation.totalCount !== 1 || exceptionPresentation.hiddenCount !== 0) failures.push('exceptions must be classified before presentation cap')
  const mixed = { ...fixture, health: { count: 5, items: [{ pointId: 'bad', status: 'Bad' }, { pointId: 'suspended', status: 'Suspended' }, { pointId: 'missing', status: 'NoData' }, { pointId: 'stale', status: 'Stale' }, { pointId: 'uncertain', status: 'Uncertain' }] }, latest: fixture.latest }
  const mixedExceptions = dashboardExceptionItems(mixed)
  if (mixedExceptions.filter(item => item.kind === 'health').map(item => item.priority).join(',') !== '1,2,3,4,5') failures.push('mixed exceptions must use deterministic semantic priority')
  const capped = dashboardExceptionPresentation({ ...mixed, incompleteSetup: { count: 2 } }, 3)
  if (capped.totalCount !== 6 || capped.visible.length !== 3 || capped.hiddenCount !== 3) failures.push('hidden exception count must be visible and exact')
  const points: EvidenceChartPoint[] = [
    { timestamp: '2026-08-05T00:00:00Z', value: 0, quality: 'Good' },
    { timestamp: '2026-08-05T00:05:00Z', value: null, quality: 'Missing', qualityReason: 'no accepted sample' },
    { timestamp: '2026-08-05T00:10:00Z', value: 4, quality: 'Uncertain' },
  ]
  const segments = chartSegments(points)
  if (points[0].value !== 0 || segments.length !== 2) failures.push('zero must remain numeric and Missing must create a chart gap')
  return failures
}
