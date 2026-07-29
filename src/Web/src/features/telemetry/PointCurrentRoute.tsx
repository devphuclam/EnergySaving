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
      <div className="card-grid two-up"><article className="card latest-card"><div className="card-header"><div><p className="card-kicker">Measurement Point</p><h2>Latest observation</h2></div><span className="badge badge-neutral">{snapshot.state}</span></div><div className="latest-value"><strong>{noData ? '—' : snapshot.value}</strong><span>{noData ? 'No Data' : snapshot.unit ?? 'value'}</span></div><p className="muted">{noData ? 'No accepted observation is available yet. No Data is never shown as zero.' : `Quality: ${snapshot.quality ?? 'Unknown'}`}</p></article><article className="card"><p className="card-kicker">Source health</p><h2>{snapshot.health}</h2><dl className="readiness-list"><div><dt>Last received</dt><dd>{noData ? '—' : 'available'}</dd></div><div><dt>Expected interval</dt><dd>gateway supplied</dd></div><div><dt>No-data threshold</dt><dd>gateway supplied</dd></div></dl></article></div>
    </section>
  )
}
