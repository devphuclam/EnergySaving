import { OperationalStatusBadge } from './OperationalStatusBadge'

export type Freshness = 'Live' | 'Stale' | 'Degraded' | 'Unavailable'

export function FreshnessIndicator({ freshness, lastRefresh, cutoff }: { freshness: Freshness; lastRefresh?: string; cutoff?: string }) {
  const status = freshness === 'Live' ? 'Good' : freshness === 'Stale' ? 'Stale' : freshness === 'Unavailable' ? 'Unavailable' : 'Uncertain'
  return <span className="freshness-indicator" aria-label={`Độ mới: ${freshness}${lastRefresh ? `, cập nhật ${lastRefresh}` : ''}`}>
    <OperationalStatusBadge status={status} detail={lastRefresh ? `Cập nhật ${lastRefresh}` : undefined} />
    {cutoff && <small className="metadata">Cutoff {cutoff}</small>}
  </span>
}
