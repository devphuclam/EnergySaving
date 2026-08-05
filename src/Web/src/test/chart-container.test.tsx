import { ChartContainer, ChartTextAlternative, chartSegments, isPlottableEvidencePoint, normalizedChartQuality, numericDomain, type EvidenceChartPoint } from '../components/charts/ChartContainer'

/** T028/T029 corrective source-visible checks; runtime execution remains package-policy blocked. */
export function runChartContainerChecks(): string[] {
  const failures: string[] = []
  const points: EvidenceChartPoint[] = [
    { timestamp: '2026-08-05T10:00:00Z', value: 0, quality: 'Good' },
    { timestamp: '2026-08-05T10:10:00Z', value: null, quality: 'Missing' },
    { timestamp: '2026-08-05T10:15:00Z', value: 4, quality: 'Missing', qualityReason: 'SERVER_REASON' },
    { timestamp: '2026-08-05T10:20:00Z', value: 5 },
    { timestamp: '2026-08-05T10:25:00Z', value: Number.NaN, quality: 'Good' },
    { timestamp: '2026-08-05T10:30:00Z', value: Number.POSITIVE_INFINITY, quality: 'Good' },
    { timestamp: '2026-08-05T10:35:00Z', value: 42, quality: 'Uncertain', qualityReason: 'LATE_RECEIPT' },
  ]
  if (!isPlottableEvidencePoint(points[0])) failures.push('zero + Good must remain plottable')
  if (isPlottableEvidencePoint(points[1])) failures.push('null + Missing must create a gap')
  if (isPlottableEvidencePoint(points[2])) failures.push('numeric + Missing must create a gap')
  if (isPlottableEvidencePoint(points[3]) || normalizedChartQuality(points[3]) !== 'Missing') failures.push('absent quality must fail closed to Missing')
  if (isPlottableEvidencePoint(points[4]) || isPlottableEvidencePoint(points[5])) failures.push('non-finite values must create gaps')
  if (chartSegments(points).length !== 2) failures.push('non-plottable evidence must split line segments')
  const constant = numericDomain([10, 10, 10])
  if (!(constant.min < 10 && constant.max > 10)) failures.push('constant series must receive a bounded visual domain')
  if (typeof ChartContainer !== 'function' || typeof ChartTextAlternative !== 'function') failures.push('chart components must be importable')
  const source = ChartContainer.toString() + ChartTextAlternative.toString()
  if (source.includes('value || 0')) failures.push('chart must not coerce Missing to zero')
  if (!source.includes('useId') || source.includes('title.toLowerCase')) failures.push('chart IDs must be component-unique and not title-derived')
  if (!source.includes('qualityReason')) failures.push('authoritative quality reason must reach chart text')
  return failures
}
