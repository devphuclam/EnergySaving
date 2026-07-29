import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { LatestSnapshot } from '../../gateways/webGateways'

const empty: LatestSnapshot = { state: 'loading', value: null, health: 'Loading' }

export function PointCurrentRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState(empty)
  useEffect(() => { void gateways.latest.getSnapshot().then(setSnapshot) }, [gateways.latest])
  const noData = snapshot.state === 'no-data' || snapshot.value === null
  return (
    <section className="page" aria-labelledby="telemetry-title">
      <div className="page-heading"><div><p className="eyebrow">Telemetry</p><h1 id="telemetry-title">Latest &amp; health</h1><p className="lede">Scope-filtered current values and physical source status.</p></div><span className="badge badge-success">{snapshot.health}</span></div>
      <div className="card-grid two-up"><article className="card latest-card"><div className="card-header"><div><p className="card-kicker">Measurement Point {snapshot.pointId ?? '—'}</p><h2>Latest observation</h2></div><span className="badge badge-neutral">{snapshot.state}</span></div><div className="latest-value"><strong>{noData ? '—' : snapshot.value}</strong><span>{noData ? 'No Data' : snapshot.unit ?? 'value'}</span></div><p className="muted">{noData ? 'No accepted observation is available yet. No Data is never shown as zero.' : `Quality: ${snapshot.quality ?? 'Unknown'} · Reason: ${snapshot.reason ?? '—'}`}</p><dl className="readiness-list"><div><dt>Source timestamp</dt><dd>{snapshot.sourceTimestamp ?? '—'}</dd></div><div><dt>Received timestamp</dt><dd>{snapshot.receivedTimestamp ?? '—'}</dd></div></dl></article><article className="card"><p className="card-kicker">Source health / run</p><h2>{snapshot.health}</h2><dl className="readiness-list"><div><dt>Run status</dt><dd>{snapshot.runStatus ?? '—'}</dd></div><div><dt>Generated</dt><dd>{snapshot.generated ?? 0}</dd></div><div><dt>Accepted / Rejected</dt><dd>{snapshot.accepted ?? 0} / {snapshot.rejected ?? 0}</dd></div></dl></article></div>
    </section>
  )
}
