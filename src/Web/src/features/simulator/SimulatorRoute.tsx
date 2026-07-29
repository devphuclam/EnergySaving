import { useState } from 'react'

export function SimulatorRoute() {
  const [status, setStatus] = useState<'Stopped' | 'Running' | 'Paused'>('Stopped')
  const running = status === 'Running'
  return (
    <section className="page" aria-labelledby="simulator-title">
      <div className="page-heading"><div><p className="eyebrow">Acquisition</p><h1 id="simulator-title">Simulator control</h1><p className="lede">Run deterministic production for the active, pinned configuration.</p></div><span className={`badge ${running ? 'badge-success' : 'badge-neutral'}`}>{status}</span></div>
      <div className="card simulator-card"><div className="simulator-status"><span className={`status-dot ${running ? 'online' : ''}`} aria-hidden="true" /><div><p className="card-kicker">Run status</p><h2>{status}</h2><p className="muted">Pinned mapping · configuration v7 · next slot 0</p></div></div><div className="control-row"><button className="button button-primary" type="button" disabled={running} onClick={() => setStatus('Running')}>Start</button><button className="button button-secondary" type="button" disabled={!running} onClick={() => setStatus('Paused')}>Pause</button><button className="button button-secondary" type="button" disabled={status !== 'Paused'} onClick={() => setStatus('Running')}>Resume</button><button className="button button-danger" type="button" disabled={status === 'Stopped'} onClick={() => setStatus('Stopped')}>Stop</button></div></div>
      <div className="card-grid three-up"><article className="metric-card"><span>Generated</span><strong>{running ? '1' : '0'}</strong><small>slots reserved</small></article><article className="metric-card"><span>Accepted</span><strong>{running ? '1' : '0'}</strong><small>terminal results</small></article><article className="metric-card"><span>Rejected</span><strong>0</strong><small>terminal results</small></article></div>
      <div className="notice notice-info">Control events are delivered through the outbox and become visible in Audit after the Worker consumer path completes.</div>
    </section>
  )
}
