import { OperationalStatusBadge } from './OperationalStatusBadge'

export type DataQuality = 'Good' | 'Uncertain' | 'Bad' | 'Missing'

export function DataQualityIndicator({ quality, reason }: { quality: DataQuality; reason?: string }) {
  return <span className="status-indicator" aria-label={`Chất lượng dữ liệu: ${quality}${reason ? `, ${reason}` : ''}`}>
    <OperationalStatusBadge status={quality} detail={reason} />
  </span>
}
