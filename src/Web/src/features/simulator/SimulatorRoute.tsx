import { useEffect, useState } from 'react'
import { useWebGateways } from '../../gateways/GatewayContext'
import type { SimulatorSnapshot } from '../../gateways/webGateways'

const empty: SimulatorSnapshot = { state: 'loading', status: 'Stopped', generated: 0, accepted: 0, rejected: 0 }

export function SimulatorRoute() {
  const gateways = useWebGateways()
  const [snapshot, setSnapshot] = useState(empty)
  useEffect(() => { void gateways.simulator.getSnapshot().then(setSnapshot) }, [gateways.simulator])
  async function mutate(operation: 'start' | 'pause' | 'resume' | 'stop') { setSnapshot(await gateways.simulator.mutate(operation)) }
  const running = snapshot.status === 'Running'
  return (
    <section className="page" aria-labelledby="simulator-title">
      <div className="page-heading"><div><p className="eyebrow">Acquisition</p><h1 id="simulator-title">Simulator control</h1><p className="lede">Run deterministic production for the active, pinned configuration.</p></div><span className={`badge ${running ? 'badge-success' : 'badge-neutral'}`}>{snapshot.state === 'ready' ? snapshot.status : snapshot.state}</span></div>
      {snapshot.state !== 'ready' ? <div className="notice notice-warning" role="status">Simulator gateway: {snapshot.state}. Controls remain disabled until the server authorizes the run.</div> : <div className="card simulator-card"><div className="simulator-status"><span className={`status-dot ${running ? 'online' : ''}`} aria-hidden="true" /><div><p className="card-kicker">Run status</p><h2>{snapshot.status}</h2><p className="muted">Pinned configuration and mapping are supplied by the gateway.</p></div></div><div className="control-row"><button className="button button-primary" type="button" disabled={running} onClick={() => void mutate('start')}>Start</button><button className="button button-secondary" type="button" disabled={!running} onClick={() => void mutate('pause')}>Pause</button><button className="button button-secondary" type="button" disabled={snapshot.status !== 'Paused'} onClick={() => void mutate('resume')}>Resume</button><button className="button button-danger" type="button" disabled={snapshot.status === 'Stopped'} onClick={() => void mutate('stop')}>Stop</button></div></div>}
      <div className="card-grid three-up"><article className="metric-card"><span>Generated</span><strong>{snapshot.generated}</strong><small>slots reserved</small></article><article className="metric-card"><span>Accepted</span><strong>{snapshot.accepted}</strong><small>terminal results</small></article><article className="metric-card"><span>Rejected</span><strong>{snapshot.rejected}</strong><small>terminal results</small></article></div>
      <div className="notice notice-info">Control events are delivered through the outbox and become visible in Audit after the Worker consumer path completes.</div>
    </section>
  )
}
