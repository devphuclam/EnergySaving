import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { AuditSnapshot } from '../../gateways/webGateways'

const empty: AuditSnapshot = { state: 'loading', eventCount: 0, records: [] }

function safeAuditValue(value: unknown): string {
  if (value === undefined) return '—'
  return JSON.stringify(value, (key, nested) =>
    /password|secret|token|credential/i.test(key) ? '[REDACTED]' : nested)
    .replace(/(password|secret|token|credential)\s*[:=]\s*[^,;"\s]+/gi, '$1=[REDACTED]')
}

export function AuditRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState(empty)
  const [filter, setFilter] = useState('')

  useEffect(() => { void gateways.audit.getSnapshot().then(setSnapshot) }, [gateways.audit])

  async function nextPage() {
    if (snapshot.nextCursor) setSnapshot(await gateways.audit.getSnapshot(snapshot.nextCursor))
  }

  const records = (snapshot.records ?? []).filter(record =>
    !filter || `${record.actor ?? ''} ${record.object ?? ''} ${record.action ?? ''}`
      .toLowerCase().includes(filter.toLowerCase()))

  return (
    <section className="page" aria-labelledby="audit-title">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Evidence</p>
          <h1 id="audit-title">Audit review</h1>
          <p className="lede">Immutable configuration and control evidence for authorized reviewers.</p>
        </div>
        <span className="badge badge-neutral">AUDIT_READ</span>
      </div>
      {snapshot.state === 'forbidden'
        ? <div className="notice notice-warning" role="alert">Audit review is scope-gated. The server returned Forbidden for this scope.</div>
        : <div className="card">
          <div className="card-header">
            <div>
              <p className="card-kicker">Recent activity</p>
              <h2>{snapshot.state === 'ready' ? `${snapshot.eventCount} delivered events` : 'Audit gateway'}</h2>
            </div>
            <span className="muted">Keyset order · newest first</span>
          </div>
          <label className="field-label" htmlFor="audit-filter">Filter</label>
          <input id="audit-filter" className="text-input" value={filter}
            onChange={event => setFilter(event.target.value)} placeholder="Actor, object or action" />
          <div className="audit-records">
            {records.length === 0
              ? <div className="empty-state">
                <span className="empty-icon" aria-hidden="true">✓</span>
                <p>{snapshot.state === 'ready' ? 'No audit events in this scope yet.' : `Gateway state: ${snapshot.state}.`}</p>
                <small>Events appear after dispatch, inbox deduplication, and append complete.</small>
              </div>
              : <table className="audit-table">
                <thead><tr><th>Actor</th><th>Time</th><th>Object</th><th>Action</th><th>Summary</th><th>Before</th><th>After</th></tr></thead>
                <tbody>{records.map((record, index) =>
                  <tr key={`${record.time ?? 'event'}-${index}`}>
                    <td>{record.actor ?? '...'}</td>
                    <td>{record.time ?? '...'}</td>
                    <td>{record.object ?? '...'}</td>
                    <td>{record.action ?? '...'}</td>
                    <td>{record.summary ?? '...'}</td>
                    <td>{safeAuditValue(record.before)}</td>
                    <td>{safeAuditValue(record.after)}</td>
                  </tr>)}</tbody>
              </table>}
          </div>
          {snapshot.nextCursor &&
            <button className="button button-secondary" type="button" onClick={() => void nextPage()}>Next page</button>}
        </div>}
    </section>
  )
}
