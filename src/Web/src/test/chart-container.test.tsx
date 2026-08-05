import { ChartContainer, ChartTextAlternative, chartSegments, type EvidenceChartPoint } from '../components/charts/ChartContainer'

export function runChartContainerChecks(): string[] {
  const failures: string[] = []
  const points: EvidenceChartPoint[] = [
    { timestamp: '2026-08-05T10:00:00Z', value: 0, quality: 'Good' },
    { timestamp: '2026-08-05T10:10:00Z', value: null, quality: 'Missing' },
    { timestamp: '2026-08-05T10:20:00Z', value: 42, quality: 'Uncertain', qualityReason: 'LATE_RECEIPT' },
  ]
  if (chartSegments(points).length !== 2) failures.push('Missing must break the SVG line into separate segments')
  if (chartSegments(points)[0][0].value !== 0) failures.push('numeric zero must remain a plotted point')
  if (typeof ChartContainer !== 'function' || typeof ChartTextAlternative !== 'function') failures.push('chart components must be importable')
  const source = ChartContainer.toString() + ChartTextAlternative.toString()
  if (source.includes('value || 0')) failures.push('chart must not coerce Missing to zero')
  return failures
}
