import { DASHBOARD_QUALITY_REASON_UNAVAILABLE, OperationalDashboard, dashboardExceptionItems, dashboardFreshness, dashboardHealthPresentation } from '../features/dashboard/OperationalDashboard'
import { PointCurrentRoute, classifyTelemetryState, formatIntervalSeconds, qualityOf } from '../features/telemetry/PointCurrentRoute'
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
  if (!exceptions.some(item => item.key === 'health-0')) failures.push('stale source health must surface before summary')
  if (!exceptions.some(item => item.key === 'missing-latest')) failures.push('points without latest evidence must remain visible')
  if (dashboardFreshness(fixture) !== 'Stale') failures.push('stale health must drive stale freshness')
  if (dashboardHealthPresentation('Online').status !== 'Good' || dashboardHealthPresentation('Online').freshness !== 'Live') failures.push('Online must map to Good/Live')
  if (dashboardHealthPresentation('NoData').status !== 'Missing' || dashboardHealthPresentation('NoData').freshness !== 'Degraded') failures.push('NoData must map to Missing/Degraded')
  if (dashboardHealthPresentation('Suspended').status !== 'Blocked' || dashboardHealthPresentation('Suspended').freshness !== 'Degraded') failures.push('Suspended must map to Blocked/Degraded')
  if (dashboardHealthPresentation('Decommissioned').status !== 'Unavailable' || dashboardHealthPresentation('Decommissioned').freshness !== 'Degraded') failures.push('Decommissioned must map to Unavailable/Degraded')
  if (dashboardHealthPresentation('Unknown').freshness === 'Live' || dashboardFreshness({ ...fixture, health: { count: 0, items: [] } }) !== 'Unavailable') failures.push('unknown or empty health must never become Live')
  if (!exceptions.some(item => item.title.includes('P-001'))) failures.push('health identity must join to point code')
  if (classifyTelemetryState({ gatewayState: 'ready', dataState: 'NotConfigured', hasUsableSnapshot: true }) !== 'not-configured') failures.push('NotConfigured must have its own presentation')
  if (classifyTelemetryState({ gatewayState: 'no-data', dataState: 'NoData', hasUsableSnapshot: true }) !== 'no-data') failures.push('NoData must remain distinct from configuration absence')
  if (classifyTelemetryState({ gatewayState: 'conflict', dataState: 'HierarchyConflict', hasUsableSnapshot: false }) !== 'conflict') failures.push('HierarchyConflict must use ConflictState')
  if (classifyTelemetryState({ gatewayState: 'expired', hasUsableSnapshot: false }) !== 'expired') failures.push('expired must have explicit session presentation')
  if (classifyTelemetryState({ gatewayState: 'dependency', dataState: 'Data', hasUsableSnapshot: true, retryableRefresh: true }) !== 'retryable-stale') failures.push('retryable refresh must retain stale evidence only for retryable states')
  if (qualityOf('unknown') !== 'Missing') failures.push('unknown quality must fail closed to Missing')
  if (formatIntervalSeconds() !== 'Chưa có' || formatIntervalSeconds(10) !== '10s') failures.push('interval formatting must not produce Chưa cós')
  if (DASHBOARD_QUALITY_REASON_UNAVAILABLE !== 'Dashboard contract không cung cấp quality reason.') failures.push('dashboard contract limitation must not be passed as quality reason')
  const points: EvidenceChartPoint[] = [
    { timestamp: '2026-08-05T00:00:00Z', value: 0, quality: 'Good' },
    { timestamp: '2026-08-05T00:05:00Z', value: null, quality: 'Missing', qualityReason: 'no accepted sample' },
    { timestamp: '2026-08-05T00:10:00Z', value: 4, quality: 'Uncertain' },
  ]
  const segments = chartSegments(points)
  if (points[0].value !== 0 || segments.length !== 2) failures.push('zero must remain numeric and Missing must create a chart gap')
  return failures
}
