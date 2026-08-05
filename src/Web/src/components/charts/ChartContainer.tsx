import { EmptyState } from '../feedback/EmptyState'

export type ChartQuality = 'Good' | 'Uncertain' | 'Bad' | 'Missing'

export type EvidenceChartPoint = {
  timestamp: string
  value: number | null
  quality?: ChartQuality
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

export function chartSegments(points: readonly EvidenceChartPoint[]): EvidenceChartPoint[][] {
  const segments: EvidenceChartPoint[][] = []
  let segment: EvidenceChartPoint[] = []
  for (const point of points) {
    if (point.value === null || !Number.isFinite(point.value)) {
      if (segment.length > 0) segments.push(segment)
      segment = []
      continue
    }
    segment.push(point)
  }
  if (segment.length > 0) segments.push(segment)
  return segments
}

function qualityCue(quality?: ChartQuality): string {
  return quality === 'Bad' ? '×' : quality === 'Uncertain' ? '!' : quality === 'Missing' ? '—' : '•'
}

function xFor(index: number, total: number): number {
  return total <= 1 ? 24 : 24 + (index / (total - 1)) * 592
}

function yFor(value: number, min: number, max: number): number {
  return 190 - ((value - min) / Math.max(1e-9, max - min)) * 150
}

export function ChartTextAlternative({ points, metadata }: { points: readonly EvidenceChartPoint[]; metadata: EvidenceChartMetadata }) {
  return <details className="chart-alternative">
    <summary>Xem bảng dữ liệu thay thế</summary>
    <div className="table-scroll">
      <table className="data-table chart-alt-table">
        <caption>{metadata.metric ?? 'Chuỗi dữ liệu'} — bảng thay thế cho biểu đồ</caption>
        <thead><tr><th scope="col">Thời điểm</th><th scope="col">Giá trị</th><th scope="col">Chất lượng</th><th scope="col">Lý do</th></tr></thead>
        <tbody>{points.length === 0 ? <tr><td colSpan={4}>Chưa có điểm lịch sử trong hợp đồng dữ liệu hiện tại.</td></tr> : points.map((point, index) => <tr key={`${point.timestamp}-${index}`}>
          <td>{point.timestamp}</td><td>{point.value === null ? 'Missing' : `${point.value}${metadata.unit ? ` ${metadata.unit}` : ''}`}</td>
          <td>{point.quality ?? '—'}</td><td>{point.qualityReason ?? '—'}</td>
        </tr>)}</tbody>
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
  const values = points.flatMap(point => point.value !== null && Number.isFinite(point.value) ? [point.value] : [])
  const hasSeries = values.length > 0
  const min = hasSeries ? Math.min(...values) : 0
  const max = hasSeries ? Math.max(...values) : 1
  const titleId = `chart-title-${title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '') || 'series'}`
  const descriptionId = `${titleId}-description`
  return <section className="chart-shell" aria-labelledby={titleId}>
    <div className="chart-header"><div><h2 id={titleId}>{title}</h2><p id={descriptionId} className="muted">{description}</p></div><span className="badge badge-neutral">{resolvedMetadata.coverage ?? 'Coverage: chưa có'}</span></div>
    <dl className="chart-metadata" aria-label="Ngữ cảnh dữ liệu biểu đồ">
      <div><dt>Metric</dt><dd>{resolvedMetadata.metric ?? 'Chưa có'}</dd></div><div><dt>Đơn vị</dt><dd>{resolvedMetadata.unit ?? 'Chưa có'}</dd></div>
      <div><dt>Múi giờ</dt><dd>{resolvedMetadata.timezone ?? 'Asia/Ho_Chi_Minh'}</dd></div><div><dt>Cutoff</dt><dd>{resolvedMetadata.cutoff ?? 'Chưa có cutoff'}</dd></div>
      {resolvedMetadata.grain && <div><dt>Grain</dt><dd>{resolvedMetadata.grain}</dd></div>}
    </dl>
    {!hasSeries ? <EmptyState title="Chưa có chuỗi lịch sử" message={unavailableReason ?? 'Hợp đồng hiện tại không cung cấp historical series; không dựng điểm giả hoặc nối qua Missing.'} /> : <figure className="evidence-chart">
      <svg viewBox="0 0 640 220" role="img" aria-labelledby={`${titleId} ${descriptionId}`}>
        <line x1="24" x2="616" y1="190" y2="190" className="chart-axis" />
        {resolvedMetadata.threshold !== undefined && <line x1="24" x2="616" y1={yFor(resolvedMetadata.threshold, min, max)} y2={yFor(resolvedMetadata.threshold, min, max)} className="chart-threshold" strokeDasharray="6 4" />}
        {segments.map((segment, segmentIndex) => <polyline key={segmentIndex} points={segment.map(point => `${xFor(points.indexOf(point), points.length)},${yFor(point.value as number, min, max)}`).join(' ')} className="chart-line" fill="none" />)}
        {points.map((point, index) => point.value === null || !Number.isFinite(point.value) ? null : <g key={`${point.timestamp}-${index}`}>
          <circle cx={xFor(index, points.length)} cy={yFor(point.value, min, max)} r="4" className={`chart-point chart-point-${point.quality?.toLowerCase() ?? 'good'}`} />
          <title>{`${point.timestamp}: ${point.value}${resolvedMetadata.unit ? ` ${resolvedMetadata.unit}` : ''}; ${point.quality ?? 'Good'} ${qualityCue(point.quality)}`}</title>
        </g>)}
      </svg>
      <figcaption>{resolvedMetadata.thresholdLabel && `Ngưỡng: ${resolvedMetadata.thresholdLabel}. `}Điểm Missing tạo khoảng trống; không thay thế bằng zero.</figcaption>
    </figure>}
    <ChartTextAlternative points={points} metadata={resolvedMetadata} />
  </section>
}
