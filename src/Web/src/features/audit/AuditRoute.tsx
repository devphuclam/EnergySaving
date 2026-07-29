import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { AuditSnapshot } from '../../gateways/webGateways'

const empty: AuditSnapshot = { state: 'loading', eventCount: 0 }

export function AuditRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState(empty)
  useEffect(() => { void gateways.audit.getSnapshot().then(setSnapshot) }, [gateways.audit])
  return (
    <section className="page" aria-labelledby="audit-title">
      <div className="page-heading"><div><p className="eyebrow">Evidence</p><h1 id="audit-title">Audit review</h1><p className="lede">Immutable configuration and control evidence for authorized reviewers.</p></div><span className="badge badge-neutral">AUDIT_READ</span></div>
      {snapshot.state === 'forbidden' ? <div className="notice notice-warning" role="alert">Audit review is scope-gated. The server returned Forbidden for this scope.</div> : <div className="card"><div className="card-header"><div><p className="card-kicker">Recent activity</p><h2>{snapshot.state === 'ready' ? `${snapshot.eventCount} delivered events` : 'Audit gateway'}</h2></div><span className="muted">Keyset order · newest first</span></div><div className="empty-state"><span className="empty-icon" aria-hidden="true">✓</span><p>{snapshot.state === 'ready' && snapshot.eventCount > 0 ? 'Events are available in the authorized scope.' : snapshot.state === 'ready' ? 'No audit events in this scope yet.' : `Gateway state: ${snapshot.state}.`}</p><small>Events appear after dispatch, inbox deduplication, and append complete.</small></div></div>}
    </section>
  )
}
