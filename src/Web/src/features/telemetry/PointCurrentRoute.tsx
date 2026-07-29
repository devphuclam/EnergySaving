export function PointCurrentRoute() {
  return (
    <section className="page" aria-labelledby="telemetry-title">
      <div className="page-heading"><div><p className="eyebrow">Telemetry</p><h1 id="telemetry-title">Latest & health</h1><p className="lede">Scope-filtered current values and physical source status.</p></div><span className="badge badge-success">Online</span></div>
      <div className="card-grid two-up"><article className="card latest-card"><div className="card-header"><div><p className="card-kicker">Measurement Point</p><h2>Boiler room power</h2></div><span className="badge badge-success">Good</span></div><div className="latest-value"><strong>—</strong><span>No Data</span></div><p className="muted">No accepted observation is available yet. No Data is never shown as zero.</p></article><article className="card"><p className="card-kicker">Source health</p><h2>Waiting for first sample</h2><dl className="readiness-list"><div><dt>Last received</dt><dd>—</dd></div><div><dt>Expected interval</dt><dd>60 sec</dd></div><div><dt>No-data threshold</dt><dd>300 sec</dd></div></dl></article></div>
    </section>
  )
}
