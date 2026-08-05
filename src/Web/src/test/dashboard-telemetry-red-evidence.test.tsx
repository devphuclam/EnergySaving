import { OperationalDashboard, dashboardExceptionItems, dashboardFreshness } from '../features/dashboard/OperationalDashboard'
import { PointCurrentRoute } from '../features/telemetry/PointCurrentRoute'
import type { OperationalDashboardSnapshot } from '../gateways/webGateways'
import { ChartContainer, chartSegments, type EvidenceChartPoint } from '../components/charts/ChartContainer'

const fixture: OperationalDashboardSnapshot = {
  state: 'ready', roleMode: 'Engineer',
  sites: { count: 1, items: [{ name: 'Site A' }] }, sources: { count: 1, items: [{ name: 'Source A' }] },
  points: { count: 2, items: [{ pointId: 'p-1' }, { pointId: 'p-2' }] }, runs: { count: 1, items: [{ runId: 'r-1' }] },
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
  const points: EvidenceChartPoint[] = [
    { timestamp: '2026-08-05T00:00:00Z', value: 0, quality: 'Good' },
    { timestamp: '2026-08-05T00:05:00Z', value: null, quality: 'Missing', qualityReason: 'no accepted sample' },
    { timestamp: '2026-08-05T00:10:00Z', value: 4, quality: 'Uncertain' },
  ]
  const segments = chartSegments(points)
  if (points[0].value !== 0 || segments.length !== 2) failures.push('zero must remain numeric and Missing must create a chart gap')
  return failures
}
