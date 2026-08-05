import { useId } from 'react'
import { EmptyState } from '../feedback/EmptyState'

export type ChartQuality = 'Good' | 'Uncertain' | 'Bad' | 'Missing'

export type EvidenceChartPoint = {
  timestamp: string
  value: number | null
  /** Raw server quality is intentionally open-ended so unknown values fail closed. */
  quality?: string
  qualityReason?: string
}

export type EvidenceChartMetadata = {
  metric?: string
  unit?: string
  timezone?: string
  cutoff?: string
  coverage?: string
  grain?: string
  threshold?: number
  thresholdLabel?: string
}

const plottableQualities = new Set<ChartQuality>(['Good', 'Uncertain', 'Bad'])

export function normalizedChartQuality(point: EvidenceChartPoint): ChartQuality {
  return point.quality === 'Good' || point.quality === 'Uncertain' || point.quality === 'Bad' || point.quality === 'Missing'
    ? point.quality
    : 'Missing'
}

export function isPlottableEvidencePoint(point: EvidenceChartPoint): boolean {
  return typeof point.value === 'number' && Number.isFinite(point.value) && plottableQualities.has(normalizedChartQuality(point))
}

export function chartSegments(points: readonly EvidenceChartPoint[]): EvidenceChartPoint[][] {
  const segments: EvidenceChartPoint[][] = []
  let segment: EvidenceChartPoint[] = []
  for (const point of points) {
    if (!isPlottableEvidencePoint(point)) {
      if (segment.length > 0) segments.push(segment)
      segment = []
      continue
    }
    segment.push(point)
  }
  if (segment.length > 0) segments.push(segment)
  return segments
}

function qualityCue(quality: ChartQuality): string {
  return quality === 'Bad' ? '×' : quality === 'Uncertain' ? '!' : quality === 'Missing' ? '—' : '•'
}

function xFor(index: number, total: number): number {
  return total <= 1 ? 24 : 24 + (index / (total - 1)) * 592
}

function yFor(value: number, min: number, max: number): number {
  return 190 - ((value - min) / Math.max(1e-9, max - min)) * 150
}

export function numericDomain(values: readonly number[]): { min: number; max: number } {
  if (values.length === 0) return { min: 0, max: 1 }
  const minValue = Math.min(...values)
  const maxValue = Math.max(...values)
  if (minValue !== maxValue) return { min: minValue, max: maxValue }
  const padding = Math.max(Math.abs(minValue) * 0.1, 1)
  return { min: minValue - padding, max: maxValue + padding }
}

export function ChartTextAlternative({ points, metadata }: { points: readonly EvidenceChartPoint[]; metadata: EvidenceChartMetadata }) {
  return <details className="chart-alternative">
    <summary>Xem bảng dữ liệu thay thế</summary>
    <div className="table-scroll">
      <table className="data-table chart-alt-table">
        <caption>{metadata.metric ?? 'Chuỗi dữ liệu'} — bảng thay thế cho biểu đồ</caption>
        <thead><tr><th scope="col">Thời điểm</th><th scope="col">Giá trị</th><th scope="col">Chất lượng</th><th scope="col">Lý do</th></tr></thead>
        <tbody>{points.length === 0 ? <tr><td colSpan={4}>Chưa có điểm lịch sử trong hợp đồng dữ liệu hiện tại.</td></tr> : points.map((point, index) => {
          const quality = normalizedChartQuality(point)
          const value = point.value === null || !Number.isFinite(point.value) ? 'Missing' : `${point.value}${metadata.unit ? ` ${metadata.unit}` : ''}`
          return <tr key={`${point.timestamp}-${index}`}>
            <td>{point.timestamp}</td><td>{value}</td><td>{quality}</td><td>{point.qualityReason ?? '—'}</td>
          </tr>
        })}</tbody>
      </table>
    </div>
  </details>
}

export function ChartContainer({ title, description, points, metadata, unavailableReason }: {
  title: string
  description: string
  points: readonly EvidenceChartPoint[]
  metadata?: EvidenceChartMetadata
  unavailableReason?: string
}) {
  const resolvedMetadata = metadata ?? {}
  const segments = chartSegments(points)
  const values = points.flatMap(point => isPlottableEvidencePoint(point) ? [point.value as number] : [])
  const hasSeries = values.length > 0
  const domain = numericDomain(values)
  const generatedId = useId().replace(/:/g, '')
  const titleId = `chart-title-${generatedId}`
  const descriptionId = `${titleId}-description`
  return <section className="chart-shell" aria-labelledby={titleId}>
    <div className="chart-header"><div><h2 id={titleId}>{title}</h2><p id={descriptionId} className="muted">{description}</p></div><span className="badge badge-neutral">{resolvedMetadata.coverage ?? 'Coverage: chưa có'}</span></div>
    <dl className="chart-metadata" aria-label="Ngữ cảnh dữ liệu biểu đồ">
      <div><dt>Metric</dt><dd>{resolvedMetadata.metric ?? 'Chưa có'}</dd></div><div><dt>Đơn vị</dt><dd>{resolvedMetadata.unit ?? 'Chưa có'}</dd></div>
      <div><dt>Múi giờ</dt><dd>{resolvedMetadata.timezone ?? 'Chưa có'}</dd></div><div><dt>Cutoff</dt><dd>{resolvedMetadata.cutoff ?? 'Chưa có'}</dd></div>
      {resolvedMetadata.grain && <div><dt>Grain</dt><dd>{resolvedMetadata.grain}</dd></div>}
    </dl>
    {!hasSeries ? <EmptyState title="Chưa có chuỗi lịch sử" message={unavailableReason ?? 'Hợp đồng hiện tại không cung cấp historical series; không dựng điểm giả hoặc nối qua Missing.'} /> : <figure className="evidence-chart">
      <svg width="100%" viewBox="0 0 640 220" role="img" aria-labelledby={`${titleId} ${descriptionId}`}>
        <line x1="24" x2="616" y1="190" y2="190" className="chart-axis" />
        {resolvedMetadata.threshold !== undefined && <line x1="24" x2="616" y1={yFor(resolvedMetadata.threshold, domain.min, domain.max)} y2={yFor(resolvedMetadata.threshold, domain.min, domain.max)} className="chart-threshold" strokeDasharray="6 4" />}
        {segments.map((segment, segmentIndex) => <polyline key={segmentIndex} points={segment.map(point => `${xFor(points.indexOf(point), points.length)},${yFor(point.value as number, domain.min, domain.max)}`).join(' ')} className="chart-line" fill="none" />)}
        {points.map((point, index) => !isPlottableEvidencePoint(point) ? null : <g key={`${point.timestamp}-${index}`}>
          <circle cx={xFor(index, points.length)} cy={yFor(point.value as number, domain.min, domain.max)} r="4" className={`chart-point chart-point-${normalizedChartQuality(point).toLowerCase()}`} />
          <title>{`${point.timestamp}: ${point.value}${resolvedMetadata.unit ? ` ${resolvedMetadata.unit}` : ''}; ${normalizedChartQuality(point)} ${qualityCue(normalizedChartQuality(point))}${point.qualityReason ? `; ${point.qualityReason}` : ''}`}</title>
        </g>)}
      </svg>
      <figcaption>{resolvedMetadata.thresholdLabel && `Ngưỡng: ${resolvedMetadata.thresholdLabel}. `}Điểm Missing tạo khoảng trống; không thay thế bằng zero.</figcaption>
    </figure>}
    <ChartTextAlternative points={points} metadata={resolvedMetadata} />
  </section>
}
